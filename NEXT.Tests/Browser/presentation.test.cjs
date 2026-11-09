const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const source = (name) =>
    fs.readFileSync(path.join(__dirname, "../../NEXT.web/wwwroot/js", name), "utf8");

test("Inline profile saves clear passwords and follow the sign-in redirect without opening a modal", async () => {
    const handlers = new Map();
    const notices = [];
    const navigation = [];
    const busy = [];
    const passwords = [{ value: "old-secret" }, { value: "new-secret" }, { value: "new-secret" }];
    const form = {
        action: "/Account/Profile",
        matches: () => true,
        reportValidity: () => true,
        querySelectorAll: (selector) => (selector.includes("password") ? passwords : []),
    };
    const modalElement = { dataset: {}, addEventListener() {} };
    const document = {
        getElementById: (id) => (id === "workspaceModal" ? modalElement : {}),
        addEventListener: (event, handler) => handlers.set(event, handler),
    };
    vm.runInNewContext(source("site.js"), {
        document,
        FormData: class {},
        bootstrap: {
            Modal: {
                getOrCreateInstance: () => ({
                    hide() {
                        throw new Error("Unexpected modal");
                    },
                }),
            },
        },
        window: {
            NextHttp: {
                setBusy: (_, value) => busy.push(value),
                request: async () => ({
                    ok: true,
                    json: async () => ({
                        notification: { message: "Saved", kind: "success" },
                        redirectUrl: "/Account/Login",
                    }),
                }),
            },
            NextNavigation: {
                navigate: async (url) => {
                    navigation.push(url);
                    return true;
                },
            },
            NextNotifications: { show: (...args) => notices.push(args) },
        },
    });
    let prevented = false;
    await handlers.get("submit")({
        target: { closest: () => form },
        preventDefault() {
            prevented = true;
        },
    });
    assert.ok(prevented);
    assert.deepEqual(
        passwords.map((input) => input.value),
        ["", "", ""],
    );
    assert.deepEqual(navigation, ["/Account/Login"]);
    assert.deepEqual(notices, [["Saved", "success"]]);
    assert.deepEqual(busy, [true, false]);
});

test("Inline profile errors stay in the form and do not refresh the page", async () => {
    const handlers = new Map();
    const notices = [];
    const busy = [];
    const panel = {
        hidden: true,
        focus() {
            this.focused = true;
        },
    };
    const form = {
        action: "/Account/Profile",
        matches: () => true,
        reportValidity: () => true,
        querySelectorAll: () => [],
        querySelector: () => panel,
    };
    const document = {
        getElementById: (id) =>
            id === "workspaceModal" ? { dataset: {}, addEventListener() {} } : {},
        addEventListener: (event, handler) => handlers.set(event, handler),
    };
    vm.runInNewContext(source("site.js"), {
        document,
        FormData: class {},
        bootstrap: { Modal: { getOrCreateInstance: () => ({}) } },
        window: {
            NextHttp: {
                setBusy: (_, value) => busy.push(value),
                request: async () => ({ ok: false }),
                problemMessage: async () => "Password mismatch",
            },
            NextNavigation: {
                refresh() {
                    throw new Error("Unexpected refresh");
                },
            },
            NextNotifications: { show: (...args) => notices.push(args) },
        },
    });
    await handlers.get("submit")({ target: { closest: () => form }, preventDefault() {} });
    assert.equal(panel.hidden, false);
    assert.equal(panel.focused, true);
    assert.equal(panel.textContent, "Password mismatch");
    assert.deepEqual(notices, [["Password mismatch", "error"]]);
    assert.deepEqual(busy, [true, false]);
});

test("HTTP requests keep caller headers and include the Ajax contract", async () => {
    let actual;
    const window = {};
    vm.runInNewContext(source("http.js"), {
        window,
        Headers,
        fetch: async (url, options) => {
            actual = { url, options };
            return "response";
        },
    });
    assert.equal(
        await window.NextHttp.request("/save", { method: "POST", headers: { "X-Test": "value" } }),
        "response",
    );
    assert.equal(actual.options.credentials, "same-origin");
    assert.equal(actual.options.method, "POST");
    assert.equal(actual.options.headers.get("X-Test"), "value");
    assert.equal(actual.options.headers.get("X-Requested-With"), "XMLHttpRequest");
});

test("ProblemDetails retain localized guidance and validation descriptions", async () => {
    const window = {};
    vm.runInNewContext(source("http.js"), { window, Headers });
    const messages = {
        validation: "Invalid",
        expired: "Expired",
        forbidden: "Forbidden",
        conflict: "Conflict",
        error: "Error",
    };
    assert.equal(
        await window.NextHttp.problemMessage(
            {
                status: 422,
                json: async () => ({ errors: { Name: ["Required"], Code: ["Duplicate"] } }),
            },
            messages,
        ),
        "Invalid\nRequired\nDuplicate",
    );
    assert.equal(
        await window.NextHttp.problemMessage(
            {
                status: 401,
                json: async () => {
                    throw new Error();
                },
            },
            messages,
        ),
        "Expired",
    );
    assert.equal(
        await window.NextHttp.problemMessage(
            { status: 422, json: async () => ({ userMessage: "Linked records prevent deletion" }) },
            messages,
        ),
        "Linked records prevent deletion",
    );
    assert.equal(
        await window.NextHttp.problemMessage(
            { status: 500, json: async () => ({ title: "private database details" }) },
            messages,
        ),
        "Error",
    );
    assert.equal(window.NextHttp.messageFor(403, messages), "Forbidden");
    assert.equal(window.NextHttp.messageFor(409, messages), "Conflict");
});

test("Submitting a form restores existing disabled states even after repeated busy calls", () => {
    const window = {};
    vm.runInNewContext(source("http.js"), { window, Headers });
    const buttons = [{ disabled: false }, { disabled: true }];
    const attrs = new Map();
    const form = {
        querySelectorAll: () => buttons,
        setAttribute: (k, v) => attrs.set(k, v),
        removeAttribute: (k) => attrs.delete(k),
    };
    window.NextHttp.setBusy(form, true);
    window.NextHttp.setBusy(form, true);
    assert.ok(buttons.every((x) => x.disabled));
    window.NextHttp.setBusy(form, false);
    assert.equal(buttons[0].disabled, false);
    assert.equal(buttons[1].disabled, true);
    assert.equal(attrs.has("aria-busy"), false);
});

test("Image picker validates the server-supplied limit before creating previews", () => {
    const events = new Map();
    const notices = [];
    const revoked = [];
    let created = 0;
    const preview = { src: "", dataset: { placeholder: "placeholder", originalSrc: "saved-photo" } };
    const filename = {};
    const picker = {
        querySelector: (selector) => (selector.includes("preview") ? preview : filename),
    };
    const input = {
        value: "image",
        files: [{ name: "valid.png", size: 10 }],
        dataset: { maxBytes: "20" },
        closest: () => picker,
    };
    const document = {
        addEventListener: (name, fn) => events.set(name, fn),
        getElementById: () => ({ dataset: { imageSize: "Size error" } }),
    };
    vm.runInNewContext(source("profile-image-picker.js"), {
        document,
        window: {
            addEventListener() {},
            NextNotifications: { show: (...args) => notices.push(args) },
        },
        URL: {
            createObjectURL: () => "preview-" + ++created,
            revokeObjectURL: (url) => revoked.push(url),
        },
    });
    const change = () => events.get("change")({ target: { closest: () => input } });
    change();
    assert.equal(preview.src, "preview-1");
    input.files = [{ name: "too-large.png", size: 21 }];
    change();
    assert.equal(input.value, "");
    assert.equal(preview.src, "saved-photo");
    assert.equal(filename.textContent, "");
    assert.equal(created, 1);
    assert.deepEqual(revoked, ["preview-1"]);
    assert.deepEqual(notices, [["Size error", "error"]]);
    input.files = [];
    change();
    assert.equal(preview.src, "saved-photo");
});
