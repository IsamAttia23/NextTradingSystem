const initialized = new WeakSet();
function initializePage() {
    const form = document.querySelector("[data-password-change-form]");
    if (!form || initialized.has(form)) return;
    initialized.add(form);
    const password = form.elements.NewPassword;
    const confirmation = form.elements.ConfirmPassword;
    const save = form.querySelector("[data-password-save]");
    for (const input of [password, confirmation]) input.addEventListener("input", validate);
    confirmation.addEventListener("change", () => {
        if (confirmation.validity.customError) {
            window.NextNotifications?.show(form.dataset.mismatch, "error");
        }
    });
    form.addEventListener("submit", event => {
        if (!validate()) event.preventDefault();
    });
    validate();

    function validate() {
        const value = password.value;
        const rules = {
            length: value.length >= password.minLength && value.length <= password.maxLength,
            case: /[a-z]/.test(value) && /[A-Z]/.test(value),
            digit: /[0-9]/.test(value),
            symbol: /[^A-Za-z0-9]/.test(value)
        };
        form.querySelectorAll("[data-rule]").forEach(rule => rule.dataset.valid = String(rules[rule.dataset.rule]));
        const match = value === confirmation.value;
        const valid = Object.values(rules).every(Boolean) && match;
        confirmation.setCustomValidity(confirmation.value && !match ? form.dataset.mismatch : "");
        confirmation.setAttribute("aria-invalid", String(Boolean(confirmation.value) && !match));
        save.disabled = !valid;
        return valid;
    }
}
initializePage();
document.addEventListener("next:navigated", initializePage);
