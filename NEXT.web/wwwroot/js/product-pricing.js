"use strict";

(() => {
    function update(form) {
        const costInput = form.elements.namedItem("CostPrice");
        const cost = costInput.valueAsNumber;
        const formatter = new Intl.NumberFormat(document.documentElement.lang || "en", {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
        for (const output of form.querySelectorAll("[data-price-preview]")) {
            const input = form.elements.namedItem(output.dataset.pricePreview);
            const percent = input.valueAsNumber;
            const amount = cost * (1 + percent / 100);
            output.textContent = costInput.validity.valid && input.validity.valid &&
                cost >= 0 && percent >= 0 && Number.isFinite(amount)
                ? formatter.format(amount)
                : "—";
        }
    }

    function initialize() {
        for (const form of document.querySelectorAll("[data-product-pricing]")) {
            update(form);
        }
    }

    document.addEventListener("input", (event) => {
        const form = event.target.closest("[data-product-pricing]");
        if (form && ["CostPrice", "RetailPrice", "WholesalePrice", "VipPrice"].includes(event.target.name)) {
            update(form);
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
