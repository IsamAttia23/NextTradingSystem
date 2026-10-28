"use strict";
(() => {
    // One delegated component works for every dynamically loaded modal.
    const previews = new Map();
    function release(input) {
        const url = previews.get(input);
        if (url) URL.revokeObjectURL(url);
        previews.delete(input);
    }
    document.addEventListener("change", event => {
        const input = event.target.closest("[data-profile-upload]");
        const picker = input?.closest("[data-profile-picker]");
        if (!picker) return;
        const preview = picker.querySelector("[data-profile-preview]");
        const filename = picker.querySelector("[data-profile-filename]");
        const file = input.files[0];
        release(input);
        filename.textContent = file?.name ?? "";
        preview.onerror = null;
        if (!file) { preview.src = preview.dataset.placeholder; return; }
        const url = URL.createObjectURL(file);
        previews.set(input, url);
        // Some server-supported formats cannot be previewed by the browser.
        // Keep the selected file for server validation; display the silhouette instead.
        preview.onerror = () => {
            if (previews.get(input) !== url) return;
            preview.onerror = null;
            preview.src = preview.dataset.placeholder;
            release(input);
        };
        preview.src = url;
    });
    document.addEventListener("hidden.bs.modal", () => {
        for (const input of previews.keys()) release(input);
    });
    window.addEventListener("pagehide", () => {
        for (const input of previews.keys()) release(input);
    });
})();
