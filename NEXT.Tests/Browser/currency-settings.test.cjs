const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const source = fs.readFileSync(path.join(__dirname, "../../NEXT.web/wwwroot/js/currency-settings.js"), "utf8");
const flush = () => new Promise((resolve) => setImmediate(resolve));

function setup(responses) {
    const events = new Map();
    const formEvents = new Map();
    const reloadEvents = new Map();
    const calls = [];
    const notices = [];
    const attributes = new Map();
    const fieldset = { disabled: true };
    const rate = { value: "" };
    const version = { value: "" };
    const message = { textContent: "" };
    const initial = { hidden: true };
    const reload = { disabled: false, addEventListener: (name, callback) => reloadEvents.set(name, callback) };
    const form = {
        action: "/Currency",
        dataset: { error: "Network error", saved: "Saved", conflict: "Reload before saving" },
        elements: { namedItem: (name) => name === "UsdToSdg" ? rate : version },
        querySelector: (selector) => ({
            fieldset,
            "[data-currency-message]": message,
            "[data-currency-initial]": initial,
            "[data-currency-reload]": reload
        })[selector],
        addEventListener: (name, callback) => formEvents.set(name, callback),
        getAttribute: (name) => attributes.get(name),
        reportValidity: () => true
    };
    let forms = [form];
    const document = {
        readyState: "complete",
        querySelectorAll: () => forms,
        addEventListener: (name, callback) => events.set(name, callback)
    };
    const window = {
        NextHttp: {
            request: async (url, options) => {
                calls.push({ url, options });
                const response = responses.shift();
                if (response instanceof Error) {
                    throw response;
                }
                return response;
            },
            problemMessage: async (response, messages) => response.status === 409 ? messages.conflict : messages.error,
            setBusy: (_, busy) => attributes.set("aria-busy", String(busy))
        },
        NextNotifications: { show: (...args) => notices.push(args) }
    };
    class FormData {
        constructor() {
            this.values = { UsdToSdg: rate.value, RowVersion: version.value, __RequestVerificationToken: "csrf" };
        }
    }
    vm.runInNewContext(source, { document, window, FormData });
    return {
        form, fieldset, rate, version, message, initial, reload, calls, notices,
        submit: () => formEvents.get("submit")({ preventDefault() {} }),
        reloadSetting: () => reloadEvents.get("click")(),
        navigate: () => events.get("next:navigated")(),
        removeForm: () => { forms = []; }
    };
}

function response(rate, version) {
    return { ok: true, json: async () => ({ usdToSdg: rate, rowVersion: version }) };
}

test("Currency settings load and save rate, version and antiforgery data", async () => {
    const ui = setup([response(10, "version-1"), response(20, "version-2")]);
    await flush();
    assert.equal(ui.fieldset.disabled, false);
    assert.equal(ui.rate.value, "10");
    assert.equal(ui.version.value, "version-1");
    assert.equal(ui.initial.hidden, true);
    ui.rate.value = "20";
    await ui.submit();
    assert.equal(ui.calls[1].options.method, "POST");
    assert.equal(ui.calls[1].options.body.values.RowVersion, "version-1");
    assert.equal(ui.calls[1].options.body.values.__RequestVerificationToken, "csrf");
    assert.equal(ui.version.value, "version-2");
    assert.equal(ui.message.textContent, "Saved");
    assert.equal(ui.notices.length, 1);
});

test("Currency setup shows first-save notice and avoids duplicate SPA bindings", async () => {
    const ui = setup([response(0, "")]);
    await flush();
    assert.equal(ui.rate.value, "");
    assert.equal(ui.initial.hidden, false);
    ui.navigate();
    ui.removeForm();
    ui.navigate();
    assert.equal(ui.calls.length, 1);
});

test("A failed load leaves saving disabled and supports retry", async () => {
    const ui = setup([new Error("offline"), response(10, "reloaded")]);
    await flush();
    assert.equal(ui.fieldset.disabled, true);
    assert.equal(ui.message.textContent, "Network error");
    await ui.submit();
    assert.equal(ui.calls.length, 1);
    ui.reloadSetting();
    await flush();
    assert.equal(ui.fieldset.disabled, false);
    assert.equal(ui.version.value, "reloaded");
});

test("A stale save preserves the entered value until the user reloads", async () => {
    const ui = setup([response(10, "old"), { ok: false, status: 409 }, response(30, "current")]);
    await flush();
    ui.rate.value = "20";
    await ui.submit();
    assert.equal(ui.rate.value, "20");
    assert.equal(ui.version.value, "old");
    assert.equal(ui.message.textContent, "Reload before saving");
    ui.reloadSetting();
    await flush();
    assert.equal(ui.rate.value, "30");
    assert.equal(ui.version.value, "current");
});
