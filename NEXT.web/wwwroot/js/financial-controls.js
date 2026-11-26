"use strict";
document.querySelector("[data-reconciliation-preview]")?.addEventListener("submit", function (event) {
    event.preventDefault();
    const trigger=document.createElement("button"); trigger.type="button"; trigger.hidden=true;
    trigger.dataset.modalUrl=this.action+"?"+new URLSearchParams(new FormData(this)).toString();
    document.body.appendChild(trigger); trigger.click(); trigger.remove();
});
