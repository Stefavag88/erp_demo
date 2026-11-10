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
function showSection(selected) {
    for (const id of ["profiles", "fields", "customers", "activity"]) {
        $(id).hidden = id !== selected;
        $("nav-" + id).setAttribute("aria-current", id === selected ? "page" : "false");
    }
}
function updateFieldType() {
    $("initial-options").hidden = $("field-type").value !== "MULTI_SELECT";
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
    $("customer-list").replaceChildren();
    for (const customer of customers) {
        const option = element("option", customer.name);
        option.value = customer.customerId;
        $("customer").append(option);
        const item = element("li"), open = action("Open profile", async () => {
            $("customer").value = customer.customerId;
            showSection("profiles");
            await loadProfile();
        });
        item.className = "definition";
        item.append(element("h3", customer.name), open);
        $("customer-list").append(item);
    }
    if (selected)
        $("customer").value = selected;
    await loadProfile();
}
async function history(field) {
    const customer = $("customer").value, request = ++historyRequest;
    showSection("activity");
    $("history-title").textContent = field.label + " · " + ($("customer").selectedOptions?.[0]?.textContent ?? customer);
    $("history").replaceChildren(element("p", "Loading changes…"));
    const rows = await api(`/api/customers/${customer}/custom-fields/${field.fieldId}/history`);
    if (request !== historyRequest || $("customer").value !== customer)
        return;
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
    $("history-title").textContent = "Choose History beside a profile field, or in the global field library.";
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
        $("profile").append(element("p", "No active fields yet. Open Global fields to create one for every customer."));
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
        }), action("History", () => history(field)));
        if (field.fieldType === "MULTI_SELECT") {
            const hint = element("p", "Hold Ctrl (Windows) or Command (Mac) to select multiple options, then Save. To clear a saved value, deselect all options and save again.");
            hint.className = "hint";
            container.append(hint);
        }
        $("profile").append(container);
    }
    const all = await api("/api/custom-fields?includeInactive=true");
    if (request !== profileRequest || $("customer").value !== customer)
        return;
    renderDefinitions(all);
}
function renderDefinitions(fields) {
    $("field-library").replaceChildren();
    if (!fields.length)
        $("field-library").append(element("li", "No field definitions yet. Use the form to create your first field."));
    for (const field of fields) {
        const item = element("li"), actions = element("div");
        item.className = "definition" + (field.isActive ? "" : " inactive");
        actions.className = "actions";
        item.append(element("h3", field.label), element("small", `${field.fieldKey} · ${field.fieldType === "TEXT" ? "Text" : "Multi-select"} · ${field.isActive ? "Active" : "Inactive"}`));
        actions.append(action(field.isActive ? "Deactivate field" : "Reactivate field", async () => {
            await api(`/api/custom-fields/${field.fieldId}/active`, "PATCH", { isActive: !field.isActive });
            await loadProfile();
            status(field.isActive ? "Field deactivated. Saved values and history are retained." : "Field reactivated for every customer.");
        }), action("History for selected customer", () => history(field)));
        item.append(actions);
        if (field.fieldType === "MULTI_SELECT") {
            const details = element("details"), input = element("input"), add = element("div");
            details.append(element("summary", `Manage options (${field.options.length})`));
            for (const option of field.options) {
                const row = element("div");
                row.className = "option-row";
                const toggle = action(option.isActive ? "Deactivate" : "Reactivate", async () => {
                    await api(`/api/custom-fields/${field.fieldId}/options/${option.optionId}/active`, "PATCH", { isActive: !option.isActive });
                    await loadProfile();
                    status("Option updated for every customer. Existing history is retained.");
                });
                toggle.setAttribute("aria-label", `${option.isActive ? "Deactivate" : "Reactivate"} option: ${option.label}`);
                row.append(element("span", option.label + (option.isActive ? "" : " (inactive)")), toggle);
                details.append(row);
            }
            input.placeholder = "New option label";
            input.maxLength = 200;
            input.setAttribute("aria-label", `New option for ${field.label}`);
            input.disabled = !field.isActive;
            const addButton = action("Add option", async () => {
                await api(`/api/custom-fields/${field.fieldId}/options`, "POST", { label: input.value, sortOrder: field.options.length + 1 });
                await loadProfile();
                status("Option added for every customer.");
            });
            addButton.disabled = !field.isActive;
            add.className = "option-add";
            add.append(input, addButton);
            details.append(add);
            if (!field.isActive)
                details.append(element("small", "Reactivate this field before adding options."));
            item.append(details);
        }
        $("field-library").append(item);
    }
}
for (const id of ["profiles", "fields", "customers", "activity"])
    $("nav-" + id).addEventListener("click", () => showSection(id));
$("field-type").addEventListener("change", updateFieldType);
updateFieldType();
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
        updateFieldType();
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
        showSection("profiles");
        status("Customer created with all global fields available.");
    }
    catch (error) {
        status(error.message);
    }
});
loadCustomers().catch(error => status(error.message));
