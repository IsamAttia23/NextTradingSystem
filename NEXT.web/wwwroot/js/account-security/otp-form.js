import { postForm } from "./api.js";

export function initializeOtpForm(form, onVerified) {
    const input = form.elements.Code;
    const boxes = [...form.querySelectorAll(".otp-box")];
    const group = form.querySelector(".otp-boxes");
    const message = form.querySelector("[data-otp-message]");
    const button = form.querySelector("[data-otp-submit]");
    const resendButton = form.querySelector("[data-otp-resend]");
    const countdown = form.querySelector("[data-resend-countdown]");
    let expiresAt = Date.parse(form.dataset.expiresAt);
    let resendAt = Date.parse(form.dataset.resendAt);
    let attempts = 0;
    let busy = false;
    let destination = null;

    input.addEventListener("input", updateDigits);
    for (const event of ["focus", "click", "keyup"]) input.addEventListener(event, paintDigits);
    form.addEventListener("submit", verify);
    resendButton.addEventListener("click", resend);
    paintDigits();
    updateClock();
    const timer = setInterval(updateClock, 1000);
    const dispose = () => clearInterval(timer);
    window.addEventListener("pagehide", dispose, { once: true });
    return () => { dispose(); window.removeEventListener("pagehide", dispose); };

    function updateDigits() {
        input.value = normalizeDigits(input.value).slice(0, 5);
        if (!destination) showState("idle", "");
        paintDigits();
    }

    function paintDigits() {
        boxes.forEach((box, index) => {
            box.textContent = input.value[index] ?? "";
            box.dataset.current = String(index === Math.min(input.selectionStart ?? input.value.length, 4));
        });
        refreshButtons();
    }

    async function verify(event) {
        event.preventDefault();
        if (busy) return;
        if (destination) { onVerified(destination); return; }
        if (!form.reportValidity() || input.value.length !== 5 || attempts >= 3 || Date.now() >= expiresAt) return;
        setBusy(true);
        try {
            const result = await postForm(form);
            if (!result.succeeded) {
                if (result.kind === "invalid") attempts++;
                showState(result.kind === "invalid" ? "error" : "unavailable",
                    attempts >= 3 ? form.dataset.attemptsEnded : result.message);
                if (result.kind === "invalid") shake();
                return;
            }
            destination = result.redirectUrl;
            showState("success", result.message || form.dataset.success);
            button.textContent = form.dataset.continue;
        } catch (error) {
            showState("unavailable", error.message);
        } finally { setBusy(false); }
    }

    async function resend() {
        if (busy || destination || Date.now() < resendAt) return;
        setBusy(true);
        try {
            const result = await postForm(form, form.dataset.resendAction);
            if (!result.succeeded) { showState("unavailable", result.message); return; }
            expiresAt = Date.parse(result.expiresAt);
            resendAt = Date.parse(result.resendAt);
            attempts = 0;
            input.value = "";
            showState("idle", result.message);
            paintDigits();
            updateClock();
            input.focus();
        } catch (error) { showState("unavailable", error.message); }
        finally { setBusy(false); }
    }

    function showState(state, text) {
        form.dataset.state = state;
        message.textContent = text;
        message.setAttribute("role", state === "error" ? "alert" : "status");
        input.setAttribute("aria-invalid", String(state === "error"));
    }

    function setBusy(value) {
        busy = value;
        form.setAttribute("aria-busy", String(value));
        input.readOnly = value || Boolean(destination);
        refreshButtons();
    }

    function refreshButtons() {
        button.disabled = busy || (!destination && (input.value.length !== 5 || attempts >= 3 || Date.now() >= expiresAt));
        resendButton.disabled = busy || Boolean(destination) || Date.now() < resendAt;
    }

    function updateClock() {
        const remaining = Math.max(0, Math.ceil((expiresAt - Date.now()) / 1000));
        const resendRemaining = Math.max(0, Math.ceil((resendAt - Date.now()) / 1000));
        form.querySelector("[data-otp-expiry]").textContent = formatTime(remaining);
        countdown.textContent = resendRemaining ? formatTime(resendRemaining) : "";
        if (!remaining && !destination && form.dataset.state !== "expired") showState("expired", form.dataset.expired);
        refreshButtons();
    }

    function shake() {
        if (matchMedia("(prefers-reduced-motion: reduce)").matches) return;
        group.animate([
            { transform: "translateX(0)" }, { transform: "translateX(-6px)" },
            { transform: "translateX(6px)" }, { transform: "translateX(-4px)" }, { transform: "translateX(0)" }
        ], { duration: 320, iterations: 1 });
    }
}

export function normalizeDigits(value) {
    return value.replace(/[٠-٩]/g, digit => digit.charCodeAt(0) - 1632)
        .replace(/[۰-۹]/g, digit => digit.charCodeAt(0) - 1776).replace(/[^0-9]/g, "");
}

function formatTime(seconds) {
    return `${String(Math.floor(seconds / 60)).padStart(2, "0")}:${String(seconds % 60).padStart(2, "0")}`;
}
