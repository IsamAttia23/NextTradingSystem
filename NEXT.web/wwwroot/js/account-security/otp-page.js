import { initializeOtpForm } from "./otp-form.js";
import { navigateLocally } from "./api.js";

let activeForm;
let dispose;
function initializePage() {
    const form = document.querySelector("[data-otp-form]");
    if (form === activeForm) return;
    dispose?.();
    activeForm = form;
    dispose = form ? initializeOtpForm(form, navigateLocally) : null;
}
initializePage();
document.addEventListener("next:navigated", initializePage);
