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
            const reload = form.querySelector("[data-currency-reload]");
            let initialNoticeShown = false;

            function showSetting(setting) {
                rate.value = setting.usdToSdg > 0 ? String(setting.usdToSdg) : "";
                version.value = setting.rowVersion;
                if (!setting.rowVersion && !initialNoticeShown) {
                    initialNoticeShown = true;
                    window.NextNotifications?.show(form.dataset.initial, "warning");
                }
            }

            async function load() {
                fieldset.disabled = true;
                reload.disabled = true;
                try {
                    const response = await window.NextHttp.request(form.action, { cache: "no-store" });
                    if (!response.ok) {
                        window.NextNotifications?.show(await window.NextHttp.problemMessage(response, form.dataset), "error");
                        return;
                    }
                    showSetting(await response.json());
                    fieldset.disabled = false;
                } catch {
                    window.NextNotifications?.show(form.dataset.error, "error");
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
                try {
                    const response = await window.NextHttp.request(form.action, { method: "POST", body });
                    if (!response.ok) {
                        window.NextNotifications?.show(await window.NextHttp.problemMessage(response, form.dataset), "error");
                        return;
                    }
                    showSetting(await response.json());
                    window.NextNotifications?.show(form.dataset.saved, "success");
                } catch {
                    window.NextNotifications?.show(form.dataset.error, "error");
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
