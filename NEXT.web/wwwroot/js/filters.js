"use strict";
(() => {
    const active = new Map();
    const reducedMotion = window.matchMedia?.("(prefers-reduced-motion: reduce)");

    function finish(panel, state) {
        if (active.get(panel) !== state) {
            return;
        }
        active.delete(panel);
        panel.open = state.expanding;
        state.animations.forEach((animation) => animation.cancel());
        panel.removeAttribute("data-filter-animating");
        panel.removeAttribute("data-filter-closing");
    }

    // Delegation also covers filters inserted by the existing partial-page navigation.
    document.addEventListener("click", (event) => {
        if (event.defaultPrevented || event.button !== 0) {
            return;
        }
        const summary = event.target.closest?.("summary");
        const panel = summary?.parentElement;
        if (!panel?.matches("details[data-filter-panel]")) {
            return;
        }
        const body = panel.querySelector(".filter-panel-body");
        if (!body) {
            return;
        }

        event.preventDefault();
        const previous = active.get(panel);
        const expanding = !(previous?.expanding ?? panel.open);
        const fromHeight = panel.getBoundingClientRect().height;
        const fromOpacity = panel.open ? window.getComputedStyle(body).opacity : "0";
        previous?.animations.forEach((animation) => animation.cancel());
        active.delete(panel);

        if (reducedMotion?.matches || !panel.animate || !body.animate) {
            panel.open = expanding;
            panel.removeAttribute("data-filter-animating");
            panel.removeAttribute("data-filter-closing");
            return;
        }

        // Measure both natural sizes, then keep details open until the closing animation ends.
        panel.open = false;
        const collapsedHeight = panel.getBoundingClientRect().height;
        panel.open = true;
        const expandedHeight = panel.getBoundingClientRect().height;
        panel.setAttribute("data-filter-animating", "");
        panel.toggleAttribute("data-filter-closing", !expanding);
        const options = { duration: 220, easing: "ease-in-out", fill: "both" };
        const height = panel.animate(
            [
                { height: `${fromHeight}px` },
                { height: `${expanding ? expandedHeight : collapsedHeight}px` },
            ],
            options,
        );
        const opacity = body.animate(
            [{ opacity: fromOpacity }, { opacity: expanding ? "1" : "0" }],
            options,
        );
        const state = { expanding, animations: [height, opacity] };
        active.set(panel, state);
        height.onfinish = () => finish(panel, state);
    });

    // Restore natural sizing on resize and release animations for pages that were replaced.
    window.addEventListener("resize", () => active.forEach((state, panel) => finish(panel, state)));
    document.addEventListener("next:navigated", () => {
        active.forEach((state, panel) => {
            if (!panel.isConnected) {
                finish(panel, state);
            }
        });
    });
    reducedMotion?.addEventListener?.("change", () => {
        if (reducedMotion.matches) {
            active.forEach((state, panel) => finish(panel, state));
        }
    });
})();
