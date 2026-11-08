"use strict";
(() => {
    document.addEventListener("click", event => {
        const button = event.target.closest("[data-password-toggle]");
        if (!button) return;
        const input = document.getElementById(button.getAttribute("aria-controls"));
        const component = button.closest("[data-password-input]");
        if (!input || !component) return;
        const show = input.type === "password";
        const start = input.selectionStart;
        const end = input.selectionEnd;
        input.type = show ? "text" : "password";
        button.setAttribute("aria-pressed", String(show));
        const label = show ? component.dataset.hideLabel : component.dataset.showLabel;
        button.setAttribute("aria-label", label);
        button.querySelector("[data-toggle-label]").textContent = label;
        button.querySelector("[data-eye-open]").hidden = show;
        button.querySelector("[data-eye-closed]").hidden = !show;
        try { input.setSelectionRange(start, end); } catch { }
    });

    for (const eventName of ["keydown", "keyup"]) {
        document.addEventListener(eventName, event => {
            const component = event.target.closest("[data-password-input]");
            if (!component || event.target.tagName !== "INPUT") return;
            const warning = document.getElementById(event.target.getAttribute("aria-describedby"));
            if (warning) warning.hidden = !event.getModifierState("CapsLock");
        });
    }
    document.addEventListener("focusout", event => {
        if (!event.target.matches("[data-password-input] input")) return;
        const warning = document.getElementById(event.target.getAttribute("aria-describedby"));
        if (warning) warning.hidden = true;
    });
})();
