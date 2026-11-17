"use strict";

(() => {
    const roundPrice = new Intl.NumberFormat("en-US", { useGrouping: false, maximumFractionDigits: 2 });
    function update(form) {
        const costInput = form.elements.namedItem("CostPrice");
        if (!(costInput instanceof HTMLInputElement)) return;
        const cost = costInput.valueAsNumber;
        const formatter = new Intl.NumberFormat(document.documentElement.lang || "en", {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
        const capacity = form.elements.namedItem("UnitsPerPackage")?.valueAsNumber;
        const packaging = form.elements.namedItem("PackagingUnitId")?.value;
        for (const output of form.querySelectorAll("[data-price-preview]")) {
            const input = form.elements.namedItem(output.dataset.pricePreview);
            if (!(input instanceof HTMLInputElement)) continue;
            let percent = 0;
            if (input !== costInput) {
                percent = input.valueAsNumber;
            }
            let amount = cost * (1 + percent / 100);
            if (Object.hasOwn(output.dataset, "packagePrice")) {
                const row = output.closest("[data-package-price-row]");
                row.hidden = !packaging;
                amount = Number.isInteger(capacity) && capacity > 0 ? Number(roundPrice.format(amount)) * capacity : NaN;
            }
            if (costInput.validity.valid && input.validity.valid &&
                cost >= 0 && percent >= 0 && Number.isFinite(amount)) {
                output.textContent = formatter.format(amount);
            } else {
                output.textContent = "—";
            }
        }
    }

    function initialize() {
        for (const form of document.querySelectorAll('[data-product-pricing="true"]')) {
            update(form);
        }
    }

    document.addEventListener("input", (event) => {
        const form = event.target.closest('[data-product-pricing="true"]');
        if (form && ["CostPrice", "RetailPrice", "WholesalePrice", "VipPrice", "UnitsPerPackage", "PackagingUnitId"].includes(event.target.name)) {
            update(form);
        }
    });
    document.addEventListener("change", (event) => {
        if (event.target.name === "PackagingUnitId") {
            const form = event.target.closest('[data-product-pricing="true"]');
            if (form) update(form);
        }
    });
    document.addEventListener("next:formloaded", initialize);
    document.addEventListener("next:navigated", initialize);
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }
})();
