"use strict";
(() => {
    const pickerSelector = "[data-reference-picker]";
    const optionsFor = picker => [...picker.querySelectorAll("[data-reference-option]")];
    const controlFor = picker => picker.querySelector("[data-reference-control]");

    function close(picker) {
        picker.querySelector(".reference-options").hidden = true;
        const control = controlFor(picker);
        control.setAttribute("aria-expanded", "false");
        control.removeAttribute("aria-activedescendant");
        optionsFor(picker).forEach(option => option.classList.remove("is-focused"));
    }

    function open(picker, filter = "") {
        document.querySelectorAll(pickerSelector).forEach(other => {
            if (other !== picker) close(other);
        });
        const search = filter.trim().toLocaleLowerCase();
        const options = optionsFor(picker);
        options.forEach(option => {
            option.hidden = search !== "" && !option.dataset.label.toLocaleLowerCase().includes(search);
            option.classList.remove("is-focused");
        });
        picker.querySelector("[data-reference-empty]").hidden = options.some(option => !option.hidden);
        picker.querySelector(".reference-options").hidden = false;
        controlFor(picker).setAttribute("aria-expanded", "true");
        controlFor(picker).removeAttribute("aria-activedescendant");
    }

    function select(picker, option) {
        const value = picker.querySelector("[data-reference-value]");
        const control = controlFor(picker);
        value.value = option.dataset.value;
        if (control.hasAttribute("data-reference-search")) {
            control.value = option.dataset.label;
        } else {
            const display = picker.querySelector("[data-reference-display]");
            display.replaceChildren(...[...option.childNodes].map(node => node.cloneNode(true)));
        }
        optionsFor(picker).forEach(item => item.setAttribute("aria-selected", String(item === option)));
        close(picker);
        control.focus();
        value.dispatchEvent(new Event("change", { bubbles: true }));
    }

    document.addEventListener("click", event => {
        const picker = event.target.closest(pickerSelector);
        document.querySelectorAll(pickerSelector).forEach(other => {
            if (other !== picker) close(other);
        });
        if (!picker) return;
        const option = event.target.closest("[data-reference-option]");
        if (option) {
            select(picker, option);
        } else if (event.target.closest("[data-reference-control]")) {
            const control = controlFor(picker);
            if (control.hasAttribute("data-reference-search") || control.getAttribute("aria-expanded") !== "true") {
                open(picker);
            } else {
                close(picker);
            }
        }
    });

    document.addEventListener("input", event => {
        const control = event.target.closest("[data-reference-search]");
        if (!control) return;
        const picker = control.closest(pickerSelector);
        picker.querySelector("[data-reference-value]").value = "";
        optionsFor(picker).forEach(option => option.setAttribute("aria-selected", "false"));
        open(picker, control.value);
    });

    document.addEventListener("keydown", event => {
        const control = event.target.closest("[data-reference-control]");
        if (!control) return;
        const picker = control.closest(pickerSelector);
        if (event.key === "Escape" || event.key === "Tab") {
            close(picker);
            return;
        }
        if (!["ArrowDown", "ArrowUp", "Enter", " "].includes(event.key)) return;
        if (event.key === " " && control.hasAttribute("data-reference-search")) return;
        const expanded = control.getAttribute("aria-expanded") === "true";
        if (event.key === "Enter" && !expanded) return;
        event.preventDefault();
        if (!expanded) open(picker);
        const visible = optionsFor(picker).filter(option => !option.hidden);
        const index = visible.findIndex(option => option.classList.contains("is-focused"));
        if (event.key === "Enter" || event.key === " ") {
            if (index >= 0) select(picker, visible[index]);
            return;
        }
        const next = index < 0 ? (event.key === "ArrowUp" ? visible.length - 1 : 0)
            : Math.max(0, Math.min(visible.length - 1, index + (event.key === "ArrowDown" ? 1 : -1)));
        visible.forEach((option, i) => option.classList.toggle("is-focused", i === next));
        if (visible[next]) {
            control.setAttribute("aria-activedescendant", visible[next].id);
            visible[next].scrollIntoView({ block: "nearest" });
        }
    });

    document.addEventListener("focusout", event => {
        const picker = event.target.closest(pickerSelector);
        if (!picker || picker.contains(event.relatedTarget)) return;
        close(picker);
        const control = controlFor(picker);
        if (control.hasAttribute("data-reference-search") && !picker.querySelector("[data-reference-value]").value) {
            control.value = "";
        }
    });

    document.addEventListener("change", event => {
        const form = event.target.closest("form");
        if (!form || !event.target.name) return;
        form.querySelectorAll("[data-visible-when-field]").forEach(field => {
            if (field.dataset.visibleWhenField !== event.target.name) return;
            field.hidden = event.target.value !== field.dataset.visibleWhenValue;
            field.querySelectorAll("input, select, textarea").forEach(input => {
                input.disabled = field.hidden;
                if (field.hidden) input.value = "";
            });
        });
    });
})();
