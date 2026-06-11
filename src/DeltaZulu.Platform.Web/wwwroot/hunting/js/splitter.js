window.huntingLayout = window.huntingLayout || {};

window.huntingLayout.initVerticalSplitter = function (containerId, topPanelId, bottomPanelId, splitterId) {
    const container = document.getElementById(containerId);
    const topPanel = document.getElementById(topPanelId);
    const bottomPanel = document.getElementById(bottomPanelId);
    const splitter = document.getElementById(splitterId);

    if (!container || !topPanel || !bottomPanel || !splitter || splitter.dataset.initialized === "true") {
        return;
    }

    splitter.dataset.initialized = "true";

    const minTopHeight = 180;
    const minBottomHeight = 180;

    const onPointerMove = (event) => {
        const rect = container.getBoundingClientRect();
        const splitterHeight = splitter.offsetHeight;
        const maxTopHeight = Math.max(minTopHeight, rect.height - splitterHeight - minBottomHeight);
        const nextTopHeight = Math.min(Math.max(event.clientY - rect.top, minTopHeight), maxTopHeight);

        topPanel.style.flex = "0 0 auto";
        topPanel.style.height = `${nextTopHeight}px`;
        bottomPanel.style.flex = "1 1 auto";
        window.huntingMonaco?.layout?.("kql-editor");
        document.body.classList.add("hunt-splitter-active");
    };

    const onPointerUp = () => {
        document.removeEventListener("pointermove", onPointerMove);
        document.removeEventListener("pointerup", onPointerUp);
        window.huntingMonaco?.layout?.("kql-editor");
        document.body.classList.remove("hunt-splitter-active");
    };

    splitter.addEventListener("pointerdown", (event) => {
        event.preventDefault();
        splitter.setPointerCapture(event.pointerId);
        document.addEventListener("pointermove", onPointerMove);
        document.addEventListener("pointerup", onPointerUp);
    });
};