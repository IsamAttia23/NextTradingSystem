"use strict";

(() => {
    function initialize() {
        for (const form of document.querySelectorAll("[data-currency-settings]")) {
            if (form.dataset.bound) {
                continue;
            }
            form.dataset.bound = "true";

            const fieldset = form.querySelector("fieldset");
            const rate = form.elements.namedItem("UsdToSdg");
            const version = form.elements.namedItem("RowVersion");
            const message = form.querySelector("[data-currency-message]");
            const initialNotice = form.querySelector("[data-currency-initial]");
            const reload = form.querySelector("[data-currency-reload]");

            function showSetting(setting) {
                rate.value = setting.usdToSdg > 0 ? String(setting.usdToSdg) : "";
                version.value = setting.rowVersion;
                initialNotice.hidden = Boolean(setting.rowVersion);
            }

            async function load() {
                fieldset.disabled = true;
                reload.disabled = true;
                message.textContent = "";
                try {
                    const response = await window.NextHttp.request(form.action, { cache: "no-store" });
                    if (!response.ok) {
                        message.textContent = await window.NextHttp.problemMessage(response, form.dataset);
                        return;
                    }
                    showSetting(await response.json());
                    fieldset.disabled = false;
                } catch {
                    message.textContent = form.dataset.error;
                } finally {
                    reload.disabled = false;
                }
            }

            form.addEventListener("submit", async (event) => {
                event.preventDefault();
                if (form.getAttribute("aria-busy") === "true" || fieldset.disabled || !form.reportValidity()) {
                    return;
                }
                const body = new FormData(form);
                window.NextHttp.setBusy(form, true);
                message.textContent = "";
                try {
                    const response = await window.NextHttp.request(form.action, { method: "POST", body });
                    if (!response.ok) {
                        message.textContent = await window.NextHttp.problemMessage(response, form.dataset);
                        return;
                    }
                    showSetting(await response.json());
                    message.textContent = form.dataset.saved;
                    window.NextNotifications?.show(form.dataset.saved, "success");
                } catch {
                    message.textContent = form.dataset.error;
                } finally {
                    window.NextHttp.setBusy(form, false);
                }
            });

            reload.addEventListener("click", () => {
                if (form.getAttribute("aria-busy") !== "true") {
                    void load();
                }
            });
            void load();
        }
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }
    document.addEventListener("next:navigated", initialize);
})();
