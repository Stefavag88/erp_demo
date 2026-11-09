"use strict";
const $ = id => document.getElementById(id);
let currentFields = [];
let profileRequest = 0;
let historyRequest = 0;
function element(tag, text) {
    const node = document.createElement(tag);
    if (text !== undefined)
        node.textContent = text;
    return node;
}
function status(message) {
    $("status").textContent = message;
}
async function api(path, method = "GET", body) {
    const response = await fetch(path, {
        method,
        headers: body === undefined ? {} : {
            "Content-Type": "application/json"
        },
        body: body === undefined ? undefined : JSON.stringify(body)
    });
    const text = await response.text();
    let data;
    try {
        data = text ? JSON.parse(text) : null;
    }
    catch {
        data = null;
    }
    if (!response.ok)
        throw new Error(response.status === 409 ? "This value changed elsewhere. Refresh the profile before saving again." : (data?.detail || `Request failed (${response.status}). Run the local demo in Development.`));
    return data;
}
function action(label, callback) {
    const button = element("button", label);
    button.type = "button";
    button.addEventListener("click", () => callback().catch(error => status(error.message)));
    return button;
}
async function loadCustomers(selected) {
    const customers = await api("/api/customers");
    $("customer").replaceChildren();
    for (const customer of customers) {
        const option = element("option", customer.name);
        option.value = customer.customerId;
        $("customer").append(option);
    }
    if (selected)
        $("customer").value = selected;
    await loadProfile();
}
async function history(field) {
    const customer = $("customer").value, request = ++historyRequest;
    const rows = await api(`/api/customers/${customer}/custom-fields/${field.fieldId}/history`);
    if (request !== historyRequest || $("customer").value !== customer)
        return;
    $("history-title").textContent = field.label;
    $("history").replaceChildren();
    if (!rows.length) {
        $("history").append(element("p", "No values have been assigned."));
        return;
    }
    const labels = new Map(field.options.map(option => [option.optionId, option.label]));
    const value = (text, ids) => ids === null ? (text ?? "Unset") : ids.length ? ids.map(id => labels.get(id) ?? `Option ${id}`).join(", ") : "No selections";
    const table = element("table"), head = element("tr");
    for (const label of ["Version", "Previous", "New", "UTC time", "Actor"])
        head.append(element("th", label));
    table.append(head);
    for (const row of rows) {
        const tr = element("tr");
        for (const cell of [row.valueVersion, value(row.oldTextValue, row.oldSelectedOptionIds), value(row.newTextValue, row.newSelectedOptionIds), row.changedAtUtc, row.changedByUserId])
            tr.append(element("td", String(cell)));
        table.append(tr);
    }
    $("history").append(table);
}
async function loadProfile() {
    const customer = $("customer").value, request = ++profileRequest;
    ++historyRequest;
    $("history").replaceChildren();
    $("profile").replaceChildren(element("p", "Loading profile…"));
    if (!customer) {
        $("profile").replaceChildren();
        return;
    }
    const fields = await api(`/api/customers/${customer}/custom-fields`);
    if (request !== profileRequest || $("customer").value !== customer)
        return;
    currentFields = fields;
    $("profile").replaceChildren();
    if (!currentFields.length)
        $("profile").append(element("p", "No active fields. Create one below; it will appear on every profile."));
    for (const row of currentFields) {
        const field = row.definition, state = row.value, container = element("div");
        container.className = "field";
        container.append(element("h3", field.label), element("small", `Key: ${field.fieldKey} · Version: ${state.valueVersion ?? "never set"}`));
        const control = element(field.fieldType === "TEXT" ? "textarea" : "select");
        control.setAttribute("aria-label", field.label);
        if (field.fieldType === "TEXT") {
            control.maxLength = 2000;
            control.value = state.textValue ?? "";
        }
        else {
            control.multiple = true;
            for (const option of field.options) {
                const selected = (state.selectedOptionIds ?? []).includes(option.optionId);
                if (!option.isActive && !selected)
                    continue;
                const item = element("option", option.label + (option.isActive ? "" : " (inactive; may be retained or removed)"));
                item.value = String(option.optionId);
                item.selected = selected;
                control.append(item);
            }
        }
        container.append(control, action("Save", async () => {
            if (request !== profileRequest || $("customer").value !== customer)
                throw new Error("The selected customer changed. Refresh the profile before saving.");
            const result = await api(`/api/customers/${customer}/custom-fields/${field.fieldId}`, "PUT", {
                textValue: field.fieldType === "TEXT" ? control.value : null,
                selectedOptionIds: field.fieldType === "MULTI_SELECT" ? Array.from(control.selectedOptions, option => Number(option.value)) : null,
                expectedVersion: state.valueVersion
            });
            if (request !== profileRequest || $("customer").value !== customer)
                return;
            status(result.changed ? "Value saved. History updated." : "No change; no new history was created.");
            await loadProfile();
        }), action("History", () => history(field)), action("Deactivate field", async () => {
            await api(`/api/custom-fields/${field.fieldId}/active`, "PATCH", {
                isActive: false
            });
            await loadProfile();
        }));
        if (field.fieldType === "MULTI_SELECT") {
            const input = element("input");
            input.placeholder = "New option label";
            input.maxLength = 200;
            input.setAttribute("aria-label", `New option for ${field.label}`);
            container.append(input, action("Add option", async () => {
                await api(`/api/custom-fields/${field.fieldId}/options`, "POST", {
                    label: input.value,
                    sortOrder: field.options.length + 1
                });
                await loadProfile();
            }));
            for (const option of field.options)
                container.append(action(`${option.isActive ? "Deactivate" : "Reactivate"} option: ${option.label}`, async () => {
                    await api(`/api/custom-fields/${field.fieldId}/options/${option.optionId}/active`, "PATCH", {
                        isActive: !option.isActive
                    });
                    await loadProfile();
                }));
        }
        $("profile").append(container);
    }
    const all = await api("/api/custom-fields?includeInactive=true");
    if (request !== profileRequest || $("customer").value !== customer)
        return;
    $("inactive").replaceChildren();
    for (const field of all.filter(field => !field.isActive))
        $("inactive").append(action(`Reactivate ${field.label}`, async () => {
            await api(`/api/custom-fields/${field.fieldId}/active`, "PATCH", {
                isActive: true
            });
            await loadProfile();
        }), action(`History: ${field.label}`, () => history(field)));
}
$("customer").addEventListener("change", () => {
    $("history").replaceChildren();
    loadProfile().catch(error => status(error.message));
});
$("refresh").addEventListener("click", () => loadProfile().catch(error => status(error.message)));
$("create-field").addEventListener("submit", async (event) => {
    event.preventDefault();
    const formElement = event.currentTarget;
    const form = new FormData(formElement), type = form.get("fieldType");
    const options = type === "MULTI_SELECT" ? String(form.get("options")).split(/\r?\n/).filter(label => label.trim()).map((label, index) => ({
        label,
        sortOrder: index + 1
    })) : [];
    try {
        await api("/api/custom-fields", "POST", {
            fieldKey: form.get("fieldKey"),
            label: form.get("label"),
            fieldType: type,
            options
        });
        status("Field created for every customer.");
        formElement.reset();
        await loadProfile();
    }
    catch (error) {
        status(error.message);
    }
});
$("create-customer").addEventListener("submit", async (event) => {
    event.preventDefault();
    const form = event.currentTarget;
    try {
        const customer = await api("/api/customers", "POST", {
            name: new FormData(form).get("name")
        });
        form.reset();
        await loadCustomers(customer.customerId);
        status("Customer created with all global fields available.");
    }
    catch (error) {
        status(error.message);
    }
});
loadCustomers().catch(error => status(error.message));
