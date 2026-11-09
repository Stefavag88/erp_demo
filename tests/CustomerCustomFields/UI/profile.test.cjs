const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../../../src/CustomerCustomFields/ERP.CustomerCustomFields.Api/wwwroot/app.js'), 'utf8');
function harness() {
    class Node {
        constructor(tag) {
            this.tag = tag;
            this.children = [];
            this._value = '';
            this.listeners = {};
        }
        set value(value) {
            this._value = this.tag === 'input' ? String(value).replace(/[\r\n]/g, '') : value;
        }
        get value() {
            return this._value;
        }
        append(...items) {
            this.children.push(...items);
        }
        replaceChildren(...items) {
            this.children = items;
        }
        setAttribute() {
        }
        addEventListener(event, callback) {
            this.listeners[event] = callback;
        }
    }
    const nodes = {};
    const node = id => nodes[id] ??= new Node(id);
    const pending = new Map();
    const writes = [];
    const response = data => ({
        ok: true,
        text: async () => JSON.stringify(data)
    });
    const context = {
        document: {
            getElementById: node,
            createElement: tag => new Node(tag)
        },
        fetch(url, options) {
            if (options.method === 'PUT') {
                writes.push({
                    url,
                    body: JSON.parse(options.body)
                });
                return Promise.resolve(response({
                    changed: false
                }));
            }
            if (url.includes('includeInactive'))
                return Promise.resolve(response([]));
            return new Promise(resolve => pending.set(url, resolve));
        }
    };
    vm.createContext(context);
    vm.runInContext(source.replace('loadCustomers().catch(error => status(error.message));', ''), context);
    function respond(customer, text) {
        const resolve = pending.get(`/api/customers/${customer}/custom-fields`);
        assert.ok(resolve, 'Expected a pending profile request');
        resolve(response([{
                definition: {
                    fieldId: 1,
                    fieldKey: 'ref',
                    label: 'Reference',
                    fieldType: 'TEXT',
                    options: []
                },
                value: {
                    textValue: text,
                    valueVersion: 1
                }
            }]));
    }
    return {
        context,
        node,
        pending,
        writes,
        respond,
        response
    };
}
const flush = () => new Promise(resolve => setImmediate(resolve));
test('a stale profile cannot replace the selected customer or route Save to another customer', async () => {
    const h = harness();
    h.node('customer').value = 'A';
    const a = h.context.loadProfile();
    h.node('customer').value = 'B';
    const b = h.context.loadProfile();
    h.respond('B', 'B value');
    await b;
    h.respond('A', 'A value');
    await a;
    const field = h.node('profile').children[0];
    assert.equal(field.children[0].textContent, 'Reference');
    assert.equal(field.children.find(n => n.tag === 'input' || n.tag === 'textarea').value, 'B value');
    const saving = field.children.find(n => n.textContent === 'Save').listeners.click();
    await flush();
    assert.equal(h.writes[0].url, '/api/customers/B/custom-fields/1');
    h.respond('B', 'B value');
    await saving;
});
test('a pending customer switch removes old profile controls immediately', async () => {
    const h = harness();
    h.node('customer').value = 'A';
    const a = h.context.loadProfile();
    h.respond('A', 'A value');
    await a;
    h.node('customer').value = 'B';
    const b = h.context.loadProfile();
    assert.ok(!JSON.stringify(h.node('profile').children).includes('A value'));
    h.respond('B', 'B value');
    await b;
});
test('history responses cannot render a previous customer after switching', async () => {
    const h = harness();
    h.node('customer').value = 'A';
    const pendingHistory = h.context.history({
        fieldId: 1,
        label: 'Reference',
        options: []
    });
    h.node('customer').value = 'B';
    h.pending.get('/api/customers/A/custom-fields/1/history')(h.response([{
            valueVersion: 1,
            oldTextValue: null,
            newTextValue: 'A secret',
            oldSelectedOptionIds: null,
            newSelectedOptionIds: null,
            changedAtUtc: '2026-11-09T00:00:00Z',
            changedByUserId: 1
        }]));
    await pendingHistory;
    assert.ok(!JSON.stringify(h.node('history').children).includes('A secret'));
});
test('opening and saving multiline text preserves the original value', async () => {
    const h = harness();
    h.node('customer').value = 'A';
    const load = h.context.loadProfile();
    h.respond('A', 'first\nsecond');
    await load;
    const field = h.node('profile').children[0];
    const saving = field.children.find(n => n.textContent === 'Save').listeners.click();
    await flush();
    assert.equal(h.writes[0].body.textValue, 'first\nsecond');
    h.respond('A', 'first\nsecond');
    await saving;
});
