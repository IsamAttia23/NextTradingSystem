"use strict";
(() => {
    document.addEventListener("click", async event => {
        const button = event.target.closest("[data-user-activation]");
        if (!button || button.disabled) return;
        const active = button.dataset.active === "true";
        const messages = document.getElementById("workspaceModal").dataset;
        const token = button.closest(".content-card").querySelector('[data-user-activation-token] input').value;
        const body = new FormData();
        body.set("IsActive", String(!active));
        body.set("ExpectedIsActive", String(active));
        body.set("__RequestVerificationToken", token);
        button.disabled = true;
        button.setAttribute("aria-busy", "true");
        try {
            const response = await window.NextHttp.request(button.dataset.url, { method: "POST", body });
            if (!response.ok) {
                window.NextNotifications.show(await window.NextHttp.problemMessage(response, messages), "error");
                if (response.status === 409) await window.NextNavigation.refresh();
                return;
            }
            button.dataset.active = String(!active);
            button.setAttribute("aria-checked", String(!active));
            button.querySelector("[data-activation-label]").textContent = active ? button.dataset.inactiveLabel : button.dataset.activeLabel;
            window.NextNotifications.show(active ? button.dataset.deactivated : button.dataset.activated, "success");
        } catch {
            window.NextNotifications.show(messages.error, "error");
            // The server may have committed before the connection was lost.
            await window.NextNavigation.refresh();
        } finally {
            button.disabled = false;
            button.removeAttribute("aria-busy");
        }
    });
})();
