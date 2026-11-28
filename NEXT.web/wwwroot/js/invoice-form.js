"use strict";
(() => {
    const searchRequests = new WeakMap();
    const matches = new WeakMap();
    const formats = new Map();
    function money(amount) {
        const language = document.documentElement.lang || "en";
        if (!formats.has(language)) {
            formats.set(language, new Intl.NumberFormat(language,
                { minimumFractionDigits: 2, maximumFractionDigits: 2 }));
        }
        return formats.get(language).format(amount) + " SDG";
    }
    const cents = amount => Math.round((amount + Number.EPSILON) * 100);
    const rows = form => [...form.querySelectorAll(".invoice-line")];
    const pickerInput = picker => picker.querySelector("[data-invoice-search]");
    const options = picker => picker.querySelector("[data-invoice-options]");
    const pickers = "[data-invoice-picker]";

    function hideOptions(picker) {
        options(picker).hidden = true;
        const input = pickerInput(picker);
        input.setAttribute("aria-expanded", "false");
        input.removeAttribute("aria-activedescendant");
    }

    function closeSearch(picker) {
        const request = searchRequests.get(picker);
        if (request) {
            clearTimeout(request.timer);
            searchRequests.delete(picker);
        }
        hideOptions(picker);
    }

    function scheduleSearch(picker) {
        closeSearch(picker);
        const form = picker.closest("[data-invoice-form]");
        const input = pickerInput(picker);
        const term = input.value.trim();
        const feedback = picker.querySelector("[data-invoice-search-status]");
        feedback.textContent = "";
        const endpoint = picker.dataset.invoicePicker === "customer" ? form.dataset.customersUrl : form.dataset.productsUrl;
        const url = new URL(endpoint, location.href);
        url.searchParams.set("search", term);
        // Let an in-flight read finish and ignore stale results instead of cancelling its database query.
        const request = { timer: setTimeout(async () => {
            const isCurrent = () => picker.isConnected && searchRequests.get(picker) === request &&
                document.activeElement === input && input.value.trim() === term;
            try {
                const response = await window.NextHttp.request(url);
                if (!isCurrent()) return;
                if (!response.ok) {
                    const messages = form.closest(".modal")?.dataset;
                    throw new Error(messages ? await window.NextHttp.problemMessage(response, messages) : form.dataset.searchError);
                }
                const found = await response.json();
                if (!isCurrent()) return;
                const list = options(picker);
                list.replaceChildren(...found.map((item, index) => {
                    const button = document.createElement("button");
                    button.type = "button";
                    button.className = "reference-option";
                    button.id = list.id + "-" + index;
                    button.tabIndex = -1;
                    button.setAttribute("role", "option");
                    button.setAttribute("aria-selected", "false");
                    button.dataset.invoiceOption = "";
                    const context = item.barcode || item.phone;
                    button.textContent = context ? item.name + " · " + context : item.name;
                    matches.set(button, item);
                    return button;
                }));
                list.hidden = found.length === 0;
                input.setAttribute("aria-expanded", String(found.length > 0));
                feedback.textContent = found.length ? "" : form.dataset.emptyResults;
            } catch (error) {
                if (isCurrent() && error.name !== "AbortError") {
                    feedback.textContent = form.dataset.searchError;
                }
            }
        }, 230) };
        searchRequests.set(picker, request);
    }

    function choose(picker, item) {
        closeSearch(picker);
        pickerInput(picker).value = item.name;
        picker.querySelector("[data-invoice-id]").value = item.id;
        const quote = picker.querySelector("[data-quoted-price]");
        if (quote) {
            quote.value = String(item.unitPrice);
            picker.dataset.available = String(item.availableQuantity);
        }
        const form = picker.closest("[data-invoice-form]");
        picker.querySelector("[data-invoice-search-status]").textContent = quote
            ? form.dataset.availableLabel + " " + item.availableQuantity : "";
        if (quote && mergeProductRow(form, picker.closest(".invoice-line"))) return;
        calculate(form);
        pickerInput(picker).focus();
    }

    function mergeProductRow(form, row) {
        const id = row.querySelector("[data-invoice-id]").value;
        if (!id) return false;
        const previous = rows(form).find(other => other !== row && other.querySelector("[data-invoice-id]").value === id);
        if (!previous) return false;
        const quantity = row.querySelector("[data-invoice-quantity]");
        const existingQuantity = previous.querySelector("[data-invoice-quantity]");
        if (quantity.value === "" && !quantity.validity.badInput) quantity.value = "1";
        if (!quantity.validity.valid || !existingQuantity.validity.valid || !(existingQuantity.valueAsNumber > 0)) return false;
        existingQuantity.value = String(Math.round((existingQuantity.valueAsNumber + quantity.valueAsNumber) * 1000) / 1000);
        closeSearch(row.querySelector(pickers));
        row.remove();
        indexRows(form);
        previous.querySelector("[data-invoice-search-status]").textContent = form.dataset.productMerged;
        calculate(form);
        existingQuantity.focus();
        existingQuantity.dispatchEvent(new Event("input", { bubbles: true }));
        return true;
    }

    function indexRows(form) {
        rows(form).forEach((row, index) => {
            const picker = row.querySelector(pickers);
            closeSearch(picker);
            const input = pickerInput(picker);
            input.id = "invoice-product-" + index;
            input.name = "Lines[" + index + "].Search";
            options(picker).id = input.id + "-options";
            input.setAttribute("aria-controls", options(picker).id);
            const label = row.querySelector("[data-product-label]");
            label.htmlFor = input.id;
            label.id = input.id + "-label";
            options(picker).setAttribute("aria-labelledby", label.id);
            row.querySelector("[data-invoice-id]").name = "Lines[" + index + "].ProductId";
            row.querySelector("[data-quoted-price]").name = "Lines[" + index + "].QuotedUnitPrice";
            row.querySelector("[data-manual-name]").name = "Lines[" + index + "].ManualName";
            row.querySelector("[data-manual-price]").name = "Lines[" + index + "].QuotedUnitPrice";
            row.querySelector("[data-invoice-quantity]").name = "Lines[" + index + "].Quantity";
        });
    }

    function appendRow(form, manual = false) {
        form.querySelector("[data-invoice-lines]").append(form.querySelector("[data-invoice-line-template]").content.cloneNode(true));
        const row = rows(form).at(-1);
        row.dataset.manual = String(manual);
        row.querySelector("[data-invoice-picker]").hidden = manual;
        row.querySelector("[data-invoice-search]").disabled = manual;
        row.querySelector("[data-quoted-price]").disabled = manual;
        row.querySelector(".invoice-manual-name").hidden = !manual;
        row.querySelector(".invoice-manual-price").hidden = !manual;
        row.querySelector(".invoice-catalog-price").hidden = manual;
        row.querySelector("[data-manual-name]").disabled = !manual;
        row.querySelector("[data-manual-price]").disabled = !manual;
        indexRows(form);
        calculate(form);
    }

    function calculate(form) {
        const lines = rows(form);
        const quantities = new Map();
        let subtotal = 0;
        let incomplete = false;
        let count = 0;
        let lastComplete = false;
        for (const row of lines) {
            const picker = row.querySelector(pickers);
            const id = picker.querySelector("[data-invoice-id]").value;
            const manual = row.dataset.manual === "true";
            const manualPrice = row.querySelector("[data-manual-price]");
            const name = row.querySelector("[data-manual-name]").value.trim();
            const quote = manual ? manualPrice.value : picker.querySelector("[data-quoted-price]").value;
            const quantity = row.querySelector("[data-invoice-quantity]");
            const touched = Boolean(id || name || quote || pickerInput(picker).value.trim() || quantity.value || quantity.validity.badInput);
            const complete = Boolean((manual ? name && manualPrice.validity.valid : id) && quote !== "" && quantity.validity.valid && quantity.valueAsNumber > 0);
            const lineTotal = complete ? cents(quantity.valueAsNumber * Number(quote)) : 0;
            row.querySelector("[data-invoice-price]").textContent = quote === "" ? "—" : money(Number(quote));
            row.querySelector("[data-invoice-line-total]").textContent = complete ? money(lineTotal / 100) : "—";
            row.querySelector("[data-remove-invoice-line]").hidden = lines.length === 1;
            incomplete ||= touched && !complete;
            lastComplete = complete;
            if (complete) {
                subtotal += lineTotal;
                count++;
                const current = quantities.get(id)?.quantity ?? 0;
                if (!manual) quantities.set(id, { quantity: current + quantity.valueAsNumber, available: Number(picker.dataset.available) });
            }
        }
        const discountKind = form.querySelector("[data-invoice-discount]").value;
        const discountInput = form.querySelector("[data-invoice-discount-value]");
        discountInput.disabled = !discountKind;
        form.querySelector("[data-invoice-discount-field]").hidden = !discountKind;
        discountInput.max = discountKind === "Percentage" ? "100" : String(subtotal / 100);
        const value = discountInput.value === "" ? 0 : discountInput.valueAsNumber;
        const discount = discountKind === "Percentage" ? cents(subtotal / 100 * value / 100)
            : discountKind ? cents(value) : 0;
        const shortage = form.dataset.allowOverselling !== "true" && [...quantities.values()].some(item => item.quantity - item.available > 0.000001);
        form.querySelector("[data-invoice-subtotal]").textContent = money(subtotal / 100);
        form.querySelector("[data-invoice-discount-amount]").textContent = money(discount / 100);
        form.querySelector("[data-invoice-total]").textContent = money((subtotal - discount) / 100);
        form.querySelector("[data-add-invoice-line]").disabled = incomplete || !lastComplete;
        form.querySelector("[data-save-invoice]").disabled = incomplete || shortage || count === 0 ||
            (discountKind !== "" && !discountInput.validity.valid);
        form.querySelector("[data-invoice-stock-warning]").textContent = shortage ? form.dataset.stockError : "";
    }

    document.addEventListener("next:formloaded", ({ detail }) => {
        const form = detail.form;
        if (!form.matches("[data-invoice-form]")) return;
        const initial = JSON.parse(form.querySelector("[data-invoice-initial-lines]")?.textContent || "[]");
        if (initial.length) {
            for (const line of initial) {
                appendRow(form, Boolean(line.manualName));
                const row = rows(form).at(-1);
                row.querySelector("[data-invoice-quantity]").value = String(line.quantity);
                if (line.manualName) {
                    row.querySelector("[data-manual-name]").value = line.manualName;
                    row.querySelector("[data-manual-price]").value = String(line.unitPrice);
                }
                if (line.product) choose(row.querySelector(pickers), line.product);
            }
            calculate(form);
        } else appendRow(form);

        form.addEventListener("input", event => {
            if (event.target.matches("[data-invoice-search]")) {
                const picker = event.target.closest(pickers);
                picker.querySelector("[data-invoice-id]").value = "";
                const quote = picker.querySelector("[data-quoted-price]");
                if (quote) quote.value = "";
                scheduleSearch(picker);
            }
            calculate(form);
        });
        form.addEventListener("change", event => {
            if (event.target.matches("[data-invoice-quantity]") && mergeProductRow(form, event.target.closest(".invoice-line"))) return;
            calculate(form);
        });
        form.addEventListener("focusin", event => {
            if (event.target.matches("[data-invoice-search]")) {
                scheduleSearch(event.target.closest(pickers));
            }
        });
        form.addEventListener("focusout", event => {
            const picker = event.target.closest(pickers);
            if (picker && !picker.contains(event.relatedTarget)) closeSearch(picker);
        });
        form.addEventListener("mousedown", event => {
            if (event.target.closest("[data-invoice-option]")) event.preventDefault();
        });
        form.addEventListener("click", event => {
            const choice = event.target.closest("[data-invoice-option]");
            if (choice) choose(choice.closest(pickers), matches.get(choice));
            else if (event.target.closest("[data-add-manual-line]")) {
                const last = rows(form).at(-1);
                if (rows(form).length === 1 && !last.querySelector("[data-invoice-id]").value && !pickerInput(last.querySelector(pickers)).value && !last.querySelector("[data-invoice-quantity]").value) last.remove();
                appendRow(form, true);
                rows(form).at(-1).querySelector("[data-manual-name]").focus();
            } else if (event.target.closest("[data-add-invoice-line]")) {
                appendRow(form);
                pickerInput(rows(form).at(-1)).focus();
            } else if (event.target.closest("[data-remove-invoice-line]")) {
                event.target.closest(".invoice-line").remove();
                indexRows(form);
                calculate(form);
            } else if (event.target.matches("[data-invoice-search]")) {
                scheduleSearch(event.target.closest(pickers));
            }
        });
        form.addEventListener("keydown", event => {
            if (!event.target.matches("[data-invoice-search]")) return;
            const picker = event.target.closest(pickers);
            const list = options(picker);
            if (list.hidden) return;
            if (event.key === "Escape") {
                closeSearch(picker);
                event.stopPropagation();
                return;
            }
            const choices = [...list.querySelectorAll("[data-invoice-option]")];
            if (choices.length === 0) return;
            const index = choices.findIndex(choice => choice.getAttribute("aria-selected") === "true");
            if (event.key === "Enter" && index >= 0) {
                event.preventDefault();
                choose(picker, matches.get(choices[index]));
            } else if (event.key === "ArrowDown" || event.key === "ArrowUp") {
                event.preventDefault();
                const offset = event.key === "ArrowDown" ? 1 : -1;
                const position = index < 0 && offset < 0 ? choices.length - 1
                    : Math.max(0, Math.min(choices.length - 1, index + offset));
                choices.forEach((choice, i) => {
                    choice.setAttribute("aria-selected", String(i === position));
                    choice.classList.toggle("is-focused", i === position);
                });
                event.target.setAttribute("aria-activedescendant", choices[position].id);
                choices[position].scrollIntoView({ block: "nearest" });
            }
        });
    });
})();
