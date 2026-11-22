"use strict";
(() => {
    const modalElement = document.getElementById("workspaceModal");
    if (!modalElement) {
        return;
    }
    const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
    const body = document.getElementById("modalBody");
    const title = document.getElementById("modalTitle");
    const discard = document.getElementById("modalDiscard");
    let request;
    let trigger;
    let saving = false;
    let filterMode = false;
    let allowClose = false;
    let initialValues;
    let lastFocus;
    const messageFor = (status) => window.NextHttp.messageFor(status, modalElement.dataset);
    const firstControl = "input:not([type=hidden]):not([data-checklist-option]),select,textarea,[data-bs-toggle=dropdown],button[type=submit]";
    document.addEventListener("change", (event) => {
        const checklist = event.target.closest("[data-checklist]");
        if (!checklist || !body.contains(checklist)) {
            return;
        }
        const toggle = checklist.querySelector("[data-bs-toggle=dropdown]");
        const count = checklist.querySelectorAll("input:checked").length;
        toggle.querySelector("[data-checklist-summary]").textContent =
            count === 0 ? toggle.dataset.choose : toggle.dataset.count.replace("{0}", count);
    });
    function showError(message) {
        window.NextNotifications?.show(message, "error");
    }

    function formValues(form) {
        const values = [...new FormData(form)].map(([name, value]) =>
            [name, typeof value === "string" ? value : (value.name ? [value.name, value.size, value.lastModified] : null)],
        );
        const searchInputs = [...form.querySelectorAll("[data-reference-search]")].map(input => input.value);
        return JSON.stringify([values, searchInputs]);
    }

    function keepEditing() {
        discard.hidden = true;
        body.hidden = false;
        title.textContent = body.querySelector("[data-modal-form]").dataset.title;
        modalElement.removeAttribute("aria-describedby");
        modal.handleUpdate();
        if (lastFocus?.isConnected) {
            lastFocus.focus();
        } else {
            body.querySelector(firstControl)?.focus();
        }
    }

    function closeModal() {
        allowClose = true;
        modal.hide();
    }

    discard.addEventListener("click", event => {
        if (event.target.closest("[data-keep-editing]")) {
            keepEditing();
        } else if (event.target.closest("[data-discard-changes]")) {
            closeModal();
        }
    });

    document.addEventListener("next:navigated", () => {
        if (filterMode) {
            closeModal();
        }
    });

    document.addEventListener("click", async (event) => {
        const button = event.target.closest("[data-modal-url], [data-modal-template]");
        if (!button || saving) {
            return;
        }
        event.preventDefault();
        trigger = button;
        request?.abort();
        const template = document.getElementById(button.dataset.modalTemplate ?? "");
        filterMode = template instanceof HTMLTemplateElement;
        modalElement.classList.toggle("filter-modal", filterMode);
        initialValues = undefined;
        if (filterMode) {
            body.replaceChildren(template.content.cloneNode(true));
            title.textContent = button.dataset.title;
            modal.show();
            return;
        }

        const loading = new AbortController();
        request = loading;
        body.replaceChildren();
        try {
            const url = new URL(button.dataset.modalUrl, location.href);
            if (url.origin !== location.origin) {
                throw new Error("Origin");
            }
            const response = await window.NextHttp.request(url, { signal: loading.signal });
            if (loading.signal.aborted) {
                return;
            }
            if (!response.ok || !response.headers.get("content-type")?.includes("text/html")) {
                body.replaceChildren();
                showError(messageFor(response.status));
                return;
            }
            // Only server-rendered, Razor-encoded same-origin partial views enter this container.
            const content = await response.text();
            if (loading.signal.aborted) {
                return;
            }
            body.innerHTML = content;
            const contentRoot = body.querySelector("[data-modal-form], [data-modal-view]");
            if (!contentRoot) {
                body.replaceChildren();
                showError(modalElement.dataset.error);
                return;
            }
            title.textContent = contentRoot.dataset.title;
            if (contentRoot.matches("[data-modal-form]")) {
                document.dispatchEvent(new CustomEvent("next:formloaded", { detail: { form: contentRoot } }));
                initialValues = formValues(contentRoot);
            }
            modal.show();
            if (modalElement.classList.contains("show")) {
                contentRoot.querySelector(firstControl + ",a[href],button")?.focus();
            }
        } catch (error) {
            if (!loading.signal.aborted && error.name !== "AbortError") {
                body.replaceChildren();
                showError(modalElement.dataset.error);
            }
        }
    });
    modalElement.addEventListener("shown.bs.modal", () =>
        body.querySelector(firstControl + ",[data-modal-view] a[href],[data-modal-view] button")?.focus(),
    );
    modalElement.addEventListener("hide.bs.modal", (event) => {
        if (saving) {
            event.preventDefault();
            return;
        }
        if (allowClose) {
            return;
        }
        if (!discard.hidden) {
            event.preventDefault();
            keepEditing();
            return;
        }
        const form = body.querySelector("[data-modal-form]");
        if (form && initialValues !== undefined && formValues(form) !== initialValues) {
            event.preventDefault();
            lastFocus = document.activeElement;
            body.hidden = true;
            discard.hidden = false;
            title.textContent = modalElement.dataset.discardTitle;
            modalElement.setAttribute("aria-describedby", "modalDiscardMessage");
            modal.handleUpdate();
            discard.querySelector("[data-keep-editing]").focus();
        }
    });
    modalElement.addEventListener("hidden.bs.modal", () => {
        request?.abort();
        const nextFilter = filterMode ? document.querySelector("[data-modal-template]") : null;
        filterMode = false;
        allowClose = false;
        initialValues = undefined;
        lastFocus = undefined;
        modalElement.classList.remove("filter-modal");
        modalElement.removeAttribute("aria-describedby");
        body.hidden = false;
        discard.hidden = true;
        body.replaceChildren();
        if (trigger?.isConnected) {
            trigger.focus();
        } else {
            (nextFilter ?? document.getElementById("main"))?.focus({ preventScroll: true });
        }
    });
    document.addEventListener("submit", async (event) => {
        const form = event.target.closest("[data-modal-form]");
        if (!form || !body.contains(form)) {
            return;
        }
        event.preventDefault();
        if (saving || !form.reportValidity()) {
            return;
        }
        saving = true;
        const buttons = [...modalElement.querySelectorAll("button")];
        window.NextHttp.setBusy(form, true, buttons);
        try {
            const response = await window.NextHttp.request(form.action, {
                method: "POST",
                body: new FormData(form),
            });
            if (!response.ok) {
                showError(await window.NextHttp.problemMessage(response, modalElement.dataset));
                if (response.status === 409 && form.querySelector('[name="RoleSelection"]')) {
                    saving = false;
                    closeModal();
                    await window.NextNavigation?.refresh();
                }
                return;
            }
            const result = await response.json().catch(() => null);
            // Clear secrets even if refreshing the successfully saved page fails.
            form.querySelectorAll('input[type="password"]').forEach((input) => (input.value = ""));
            const updated = result?.redirectUrl
                ? await window.NextNavigation?.navigate(result.redirectUrl, { replace: true })
                : await window.NextNavigation?.refresh();
            saving = false;
            closeModal();
            window.NextNotifications?.show(
                result?.notification?.message ?? modalElement.dataset.success,
                result?.notification?.kind ?? "info",
            );
            // A refresh can fail after the mutation succeeds; do not invite a duplicate submission.
            if (updated === false) {
                window.NextNotifications?.show(modalElement.dataset.refreshError, "warning");
            }
        } catch {
            showError(modalElement.dataset.error);
        } finally {
            saving = false;
            window.NextHttp.setBusy(form, false);
        }
    });
})();
