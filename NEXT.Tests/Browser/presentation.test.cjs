const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const source = (name) =>
    fs.readFileSync(path.join(__dirname, "../../NEXT.web/wwwroot/js", name), "utf8");

test("SPA culture changes update retained accessible names without resetting live controls", async () => {
    const element = (html = "", attrs = {}) => {
        const attributes = new Map(Object.entries(attrs));
        return {
            innerHTML: html, childNodes: [{ html }], dataset: {}, scrollTop: 0,
            get attributes() { return [...attributes].map(([name, value]) => ({ name, value })); },
            getAttribute: name => attributes.get(name),
            setAttribute: (name, value) => attributes.set(name, value),
            removeAttribute: name => attributes.delete(name),
            replaceChildren(...nodes) { this.childNodes = nodes; this.innerHTML = nodes.map(node => node.html).join(""); },
            querySelector: () => null, querySelectorAll: () => [], focus() {},
            classList: { contains: () => false, toggle() {} }
        };
    };
    const page = (language, labels) => {
        const nodes = new Map([
            [".app-shell", element()], ["#main", element("Main")],
            [".skip-link", element(labels.skip)], [".breadcrumb-label", element("Breadcrumb")],
            [".app-footer", element("Footer")],
            [".mobile-menu", element("Icon", { "aria-label": labels.menu, "aria-expanded": "false" })],
            ["#notifications", element("Notice", { "aria-label": labels.notices, "data-close": labels.close })],
            ["#workspaceModal", element("Modal", { "aria-hidden": "true", "data-loading": labels.loading })],
            ["#workspaceModal .btn-close", element("", { "aria-label": labels.close })]
        ]);
        const actions = element();
        const topbar = element();
        topbar.querySelector = selector => selector === ".mobile-menu" ? nodes.get(selector)
            : selector === ".topbar-actions" ? actions : null;
        nodes.set(".topbar", topbar);
        return {
            nodes, documentElement: { lang: language, dir: language === "ar" ? "rtl" : "ltr" },
            body: element(), querySelector: selector => nodes.get(selector) ?? null,
            getElementById: id => nodes.get("#" + id) ?? null,
            activeElement: null, importNode: node => ({ ...node }), addEventListener() {}, dispatchEvent() {}
        };
    };
    const arabic = { skip: "الانتقال إلى المحتوى", menu: "القائمة الرئيسية", notices: "الإشعارات", close: "إغلاق", loading: "جار التحميل" };
    const english = { skip: "Skip to content", menu: "Main navigation", notices: "Notifications", close: "Close", loading: "Loading" };
    const document = page("ar", arabic);
    const modal = document.nodes.get("#workspaceModal");
    const menu = document.nodes.get(".mobile-menu");
    menu.setAttribute("aria-expanded", "true");
    modal.setAttribute("aria-hidden", "false");
    let responseDocument;
    const history = { state: {}, replaceState() {}, pushState() {} };
    const window = {
        history, fetch() {}, DOMParser: class {}, addEventListener() {},
        NextHttp: { request: async () => ({ status: 200, url: "http://localhost/Products",
            headers: { get: () => "text/html" }, text: async () => "HTML" }) }
    };
    vm.runInNewContext(source("navigation.js"), {
        document, window, history, location: new URL("http://localhost/Products"), URL, AbortController,
        DOMParser: class { parseFromString() { return responseDocument; } },
        CustomEvent: class {}
    });
    for (const [language, labels] of [["en", english], ["ar", arabic]]) {
        responseDocument = page(language, labels);
        assert.equal(await window.NextNavigation.navigate("/Products", { history: false }), true);
        assert.equal(document.documentElement.lang, language);
        assert.equal(document.documentElement.dir, language === "ar" ? "rtl" : "ltr");
        assert.equal(document.nodes.get(".skip-link").innerHTML, labels.skip);
        assert.equal(menu.getAttribute("aria-label"), labels.menu);
        assert.equal(document.nodes.get("#notifications").getAttribute("aria-label"), labels.notices);
        assert.equal(document.nodes.get("#workspaceModal .btn-close").getAttribute("aria-label"), labels.close);
        assert.equal(modal.getAttribute("data-loading"), labels.loading);
        assert.equal(menu.getAttribute("aria-expanded"), "true");
        assert.equal(modal.getAttribute("aria-hidden"), "false");
        assert.strictEqual(document.nodes.get("#workspaceModal"), modal);
    }
});

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
