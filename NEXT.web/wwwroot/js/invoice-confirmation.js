"use strict";
document.addEventListener("next:formloaded", ({ detail }) => {
    const form = detail.form;
    if (!form.matches("[data-invoice-confirmation], [data-invoice-payment]")) return;
    const amount = form.querySelector("[data-confirm-amount]");
    const method = form.querySelector("[data-confirm-method]");
    const format = new Intl.NumberFormat(document.documentElement.lang || "en",
        { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const total = Number(amount.max);
    const paidBefore = Number(form.dataset.paidBefore || 0);
    function update() {
        amount.setCustomValidity("");
        const paid = amount.valueAsNumber;
        if (Number.isFinite(paid) && Math.abs(Math.round(paid * 100) - paid * 100) > 0.000001)
            amount.setCustomValidity(form.dataset.paymentError);
        const valid = amount.validity.valid;
        method.disabled = valid && paid === 0;
        form.querySelector("[data-confirm-remaining]").textContent = valid ? format.format(total - paid) + " SDG" : "—";
        form.querySelector("[data-confirm-payment-status]").textContent = !valid ? "—"
            : paid === total ? form.dataset.paid : paid + paidBefore === 0 ? form.dataset.unpaid : form.dataset.partial;
    }
    amount.addEventListener("input", update);
    update();
});
