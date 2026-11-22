"use strict";
(() => {
    let printing = false;
    async function print() {
        if (printing) return;
        printing = true;
        try {
            await document.fonts.ready;
            window.print();
        } finally { printing = false; }
    }
    document.querySelector("[data-print-invoice]")?.addEventListener("click", print);
    if (document.body.dataset.autoPrint === "true") print();
})();
