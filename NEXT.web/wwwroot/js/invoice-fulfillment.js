"use strict";
document.addEventListener("next:formloaded", ({ detail }) => {
    const form = detail.form;
    if (!form.matches("[data-invoice-fulfillment]")) return;
    const target = form.elements.namedItem("TargetStatus");
    function update() {
        const shipped = target.value === "Shipped";
        const title = shipped ? form.dataset.ship
            : form.dataset.currentStatus === "Confirmed" ? form.dataset.direct : form.dataset.deliver;
        form.dataset.title = title;
        form.querySelector("[data-fulfillment-submit]").textContent = title;
        form.querySelector("[data-fulfillment-target]").textContent = shipped ? form.dataset.shipped : form.dataset.delivered;
        const heading = form.closest(".modal")?.querySelector(".modal-title");
        if (heading) heading.textContent = title;
    }
    target.addEventListener("change", update);
    update();
});
