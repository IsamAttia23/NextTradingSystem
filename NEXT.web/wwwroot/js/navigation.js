"use strict";
(() => {
    if (!window.fetch || !window.DOMParser || !window.history?.pushState) return;
    let pendingGet, sequence = 0;
    // A document-scoped DOM marker helps verify that navigation preserves the live shell.
    const shell = document.querySelector(".app-shell");
    if (shell) shell.dataset.shellInstance = globalThis.crypto?.randomUUID?.() ?? Date.now().toString(36);
    const main = () => document.getElementById("main");
    const notice = (message, kind = "error") => window.NextNotifications?.show(message, kind);
    const messages = () => document.getElementById("workspaceModal")?.dataset ?? {};
    function rememberScroll() {
        history.replaceState({...history.state, nextNavigation: true, mainScroll: main()?.scrollTop ?? 0}, "", location.href);
    }
    function syncAttributes(target, source) {
        for (const attribute of [...target.attributes]) if (attribute.name.startsWith("data-")) target.removeAttribute(attribute.name);
        for (const attribute of source.attributes) if (attribute.name.startsWith("data-")) target.setAttribute(attribute.name, attribute.value);
    }
    function updatePart(selector, documentSource) {
        const old = document.querySelector(selector), fresh = documentSource.querySelector(selector);
        if (old && fresh && old.innerHTML !== fresh.innerHTML) old.replaceChildren(...[...fresh.childNodes].map(n => document.importNode(n, true)));
    }
    function updateHeader(source) {
        updatePart(".breadcrumb-label", source);
        const topbar = document.querySelector(".topbar"), next = source.querySelector(".topbar");
        const oldMenu = topbar.querySelector(".mobile-menu"), newMenu = next.querySelector(".mobile-menu");
        if (oldMenu && !newMenu) oldMenu.remove();
        else if (!oldMenu && newMenu) topbar.prepend(document.importNode(newMenu, true));
        const actions = topbar.querySelector(".topbar-actions"), nextActions = next.querySelector(".topbar-actions");
        for (const oldForm of [...actions.querySelectorAll("form")])
            if (![...nextActions.querySelectorAll("form")].some(f => f.getAttribute("action") === oldForm.getAttribute("action"))) oldForm.remove();
        for (const fresh of nextActions.querySelectorAll("form")) {
            const old = [...actions.querySelectorAll("form")].find(f => f.getAttribute("action") === fresh.getAttribute("action"));
            if (old) old.replaceChildren(...[...fresh.childNodes].map(n => document.importNode(n, true)));
            else actions.append(document.importNode(fresh, true));
        }
        const profile = actions.querySelector(".header-profile"), nextProfile = nextActions.querySelector(".header-profile");
        if (profile && nextProfile) updatePart(".topbar .header-profile", source);
        else if (profile) profile.remove();
        else if (nextProfile) actions.append(document.importNode(nextProfile, true));
    }
    function render(source, options) {
        const freshMain = source.getElementById("main");
        if (!freshMain || !source.querySelector(".app-shell")) throw new Error("Unexpected response");
        const oldMain = main(), focusedName = document.activeElement?.name;
        const scroll = options.keepScroll ? oldMain.scrollTop : options.scroll ?? 0;
        const sidebarScroll = document.querySelector(".sidebar-scroll")?.scrollTop ?? 0;
        freshMain.querySelectorAll("script").forEach(script => script.remove());
        oldMain.replaceChildren(...[...freshMain.childNodes].map(n => document.importNode(n, true)));
        const oldSidebar = document.querySelector(".sidebar"), newSidebar = source.querySelector(".sidebar");
        if (oldSidebar && newSidebar) {
            updatePart(".sidebar-header", source); updatePart(".sidebar-scroll", source); updatePart(".sidebar-footer", source);
            document.querySelector(".sidebar-scroll").scrollTop = sidebarScroll;
        } else if (oldSidebar) oldSidebar.remove();
        else if (newSidebar) document.querySelector(".app-shell").prepend(document.importNode(newSidebar, true));
        updateHeader(source); updatePart(".app-footer", source);
        const oldDrawer = document.getElementById("mobileNavigation"), newDrawer = source.getElementById("mobileNavigation");
        if (oldDrawer && newDrawer) {
            updatePart("#mobileNavigation .offcanvas-header", source); updatePart("#mobileNavigation .offcanvas-body", source); updatePart("#mobileNavigation .sidebar-footer", source);
        } else if (oldDrawer) { bootstrap.Offcanvas.getInstance(oldDrawer)?.hide(); oldDrawer.remove(); }
        else if (newDrawer) document.body.append(document.importNode(newDrawer, true));
        for (const id of ["notifications", "workspaceModal"]) {
            const old = document.getElementById(id), fresh = source.getElementById(id);
            if (old && fresh) syncAttributes(old, fresh);
        }
        document.documentElement.lang = source.documentElement.lang;
        document.documentElement.dir = source.documentElement.dir;
        document.body.classList.toggle("authentication-page", source.body.classList.contains("authentication-page"));
        const style = document.querySelector('link[rel="stylesheet"][href*="bootstrap"]');
        const nextStyle = source.querySelector('link[rel="stylesheet"][href*="bootstrap"]');
        if (style && nextStyle) {
            const href = new URL(nextStyle.getAttribute("href"), location.href).href;
            if (style.href !== href) style.href = href;
        }
        document.title = source.title;
        oldMain.scrollTop = scroll;
        if (options.keepFocus && focusedName) {
            const input = [...oldMain.querySelectorAll("input, select, textarea")].find(e => e.name === focusedName);
            input?.focus({preventScroll: true});
        } else if (!options.keepScroll) oldMain.focus({preventScroll: true});
        const initialError = source.getElementById("notifications")?.dataset.initialError;
        if (initialError) notice(initialError);
        document.dispatchEvent(new CustomEvent("next:navigated", {detail: {url: location.href}}));
    }
    async function navigate(url, options = {}) {
        const target = new URL(url, location.href);
        if (target.origin !== location.origin) return false;
        const token = ++sequence;
        pendingGet?.abort();
        const method = options.method ?? "GET";
        const controller = method === "GET" ? new AbortController() : null;
        pendingGet = controller;
        if (!options.fromHistory) rememberScroll();
        main()?.setAttribute("aria-busy", "true");
        try {
            const response = await fetch(target.href, {method, body: options.body, signal: controller?.signal, credentials: "same-origin", cache: "no-store", headers: {"X-Requested-With": "XMLHttpRequest"}});
            if (token !== sequence) return false;
            if (response.status === 401) {
                notice(messages().expired);
                return navigate(new URL((messages().loginUrl ?? "/Account/Login") + "?returnUrl=" + encodeURIComponent(target.pathname + target.search), location.origin).href, {replace: true});
            }
            if (!response.headers.get("content-type")?.includes("text/html")) {
                const problem = await response.json().catch(() => null);
                notice(problem?.errors ? Object.values(problem.errors).flat().join("\n") : response.status === 403 ? messages().forbidden : messages().error);
                return false;
            }
            if (new URL(response.url).origin !== location.origin) throw new Error("Unexpected response origin");
            const html = new DOMParser().parseFromString(await response.text(), "text/html");
            if (token !== sequence) return false;
            if (!html.getElementById("main") || !html.querySelector(".app-shell")) throw new Error("Unexpected response");
            if (options.history !== false && !options.fromHistory)
                history[options.replace ? "replaceState" : "pushState"]({nextNavigation: true, mainScroll: options.keepScroll ? main().scrollTop : 0}, "", response.url);
            render(html, options);
            return true;
        } catch (error) {
            if (error.name !== "AbortError" && token === sequence) notice(messages().error);
            return false;
        } finally {
            if (token === sequence) { pendingGet = null; main()?.removeAttribute("aria-busy"); }
        }
    }
    document.addEventListener("click", event => {
        if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        const link = event.target.closest("a[href]");
        if (!link || link.hasAttribute("download") || link.target && link.target !== "_self" || link.matches("[data-native-navigation], [data-modal-url]") || link.getAttribute("href").startsWith("#")) return;
        const url = new URL(link.href, location.href);
        if (url.origin !== location.origin || !/^https?:$/.test(url.protocol)) return;
        event.preventDefault();
        const drawer = document.getElementById("mobileNavigation");
        if (drawer?.classList.contains("show")) bootstrap.Offcanvas.getInstance(drawer)?.hide();
        void navigate(url.href);
    });
    document.addEventListener("submit", async event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || event.defaultPrevented || form.matches("[data-modal-form], [data-native-navigation]") || form.target && form.target !== "_self") return;
        const url = new URL(form.action || location.href, location.href);
        if (url.origin !== location.origin) return;
        event.preventDefault();
        if (form.dataset.navigationBusy || !form.reportValidity()) return;
        const method = form.method.toUpperCase(), body = new FormData(form);
        if (event.submitter?.name) body.append(event.submitter.name, event.submitter.value);
        if (method === "GET") {
            url.search = new URLSearchParams([...body].filter(([, value]) => typeof value === "string" && value !== ""));
            await navigate(url.href, {keepFocus: true});
        } else {
            form.dataset.navigationBusy = "true";
            const buttons = [...form.querySelectorAll('button[type="submit"], button:not([type])')];
            buttons.forEach(button => button.disabled = true);
            try { await navigate(url.href, {method, body, replace: true, keepFocus: true}); }
            finally { delete form.dataset.navigationBusy; buttons.forEach(button => button.disabled = false); }
        }
    });
    document.addEventListener("change", event => {
        if (event.target.matches("[data-page-size]")) event.target.form.requestSubmit();
    });
    window.addEventListener("popstate", event => { void navigate(location.href, {fromHistory: true, scroll: event.state?.mainScroll ?? 0}); });
    if ("scrollRestoration" in history) history.scrollRestoration = "manual";
    history.replaceState({...history.state, nextNavigation: true, mainScroll: main()?.scrollTop ?? 0}, "", location.href);
    window.NextNavigation = Object.freeze({navigate, refresh: () => navigate(location.href, {history: false, keepScroll: true, keepFocus: true})});
})();
