const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const source = fs.readFileSync(path.join(__dirname, "../../NEXT.web/wwwroot/js/product-pricing.js"), "utf8");

function setup() {
    const events = new Map();
    const inputs = Object.fromEntries([
        ["CostPrice", 200], ["RetailPrice", 25], ["WholesalePrice", 15], ["VipPrice", 10]
    ].map(([name, value]) => [name, { name, valueAsNumber: value, validity: { valid: true } }]));
    const outputs = ["RetailPrice", "WholesalePrice", "VipPrice"].map((name) => ({
        dataset: { pricePreview: name }, textContent: ""
    }));
    const form = {
        elements: { namedItem: (name) => inputs[name] },
        querySelectorAll: () => outputs
    };
    const document = {
        readyState: "complete",
        documentElement: { lang: "en" },
        querySelectorAll: () => [form],
        addEventListener: (name, handler) => events.set(name, handler)
    };
    vm.runInNewContext(source, { document, Intl });
    return {
        inputs, outputs, events,
        change: (name, value) => {
            inputs[name].valueAsNumber = value;
            events.get("input")({ target: { name, closest: () => form } });
        }
    };
}

test("Product preview calculates all three percentages and follows cost edits", () => {
    const ui = setup();
    assert.deepEqual(ui.outputs.map((output) => output.textContent), ["250.00", "230.00", "220.00"]);
    ui.change("CostPrice", 400);
    assert.deepEqual(ui.outputs.map((output) => output.textContent), ["500.00", "460.00", "440.00"]);
    ui.change("RetailPrice", 150);
    assert.equal(ui.outputs[0].textContent, "1,000.00");
});

test("Product preview rejects missing or negative numbers and initializes loaded modal forms", () => {
    const ui = setup();
    ui.change("RetailPrice", -1);
    assert.equal(ui.outputs[0].textContent, "—");
    ui.change("CostPrice", NaN);
    assert.ok(ui.outputs.every((output) => output.textContent === "—"));
    ui.inputs.CostPrice.valueAsNumber = 100;
    ui.inputs.RetailPrice.valueAsNumber = 20;
    ui.events.get("next:formloaded")();
    assert.equal(ui.outputs[0].textContent, "120.00");
});
