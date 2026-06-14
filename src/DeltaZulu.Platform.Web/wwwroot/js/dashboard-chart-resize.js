window.huntingDashboardCharts = (() => {
    const observers = new Map();

    function observe(rootId) {
        try {
            const root = document.getElementById(rootId);
            if (!root) {
                return;
            }

            dispose(rootId);

            const resize = () => {
                window.requestAnimationFrame(() => {
                    try {
                        normalizeChartElements(root);
                        resizeCharts(root);
                    }
                    catch {
                        // Best-effort resize only. Do not surface chart resize
                        // failures to Blazor Server as JSInterop exceptions.
                    }
                });
            };

            if (typeof ResizeObserver === "undefined") {
                scheduleResizeFallback(resize);
                return;
            }

            const observer = new ResizeObserver(resize);
            observer.observe(root);

            const surface = root.querySelector(".dashboard-chart-surface");
            if (surface) {
                observer.observe(surface);
            }

            const widget = root.closest(".dashboard-widget-host");
            if (widget) {
                observer.observe(widget);
            }

            observers.set(rootId, observer);
            scheduleResizeFallback(resize);
        }
        catch {
            // Keep this API non-throwing. Blazor calls it from OnAfterRenderAsync.
        }
    }

    function scheduleResizeFallback(resize) {
        window.setTimeout(resize, 0);
        window.setTimeout(resize, 50);
        window.setTimeout(resize, 150);
        window.setTimeout(resize, 500);
    }

    function normalizeChartElements(root) {
        const surface = root.querySelector(".dashboard-chart-surface");
        if (!surface) {
            return;
        }

        const width = Math.max(1, surface.clientWidth);
        const height = Math.max(1, surface.clientHeight);
        const widthPx = `${width}px`;
        const heightPx = `${height}px`;

        const candidates = [
            surface.firstElementChild,
            ...surface.querySelectorAll("[_echarts_instance_]")
        ].filter(Boolean);

        for (const element of candidates) {
            element.style.width = widthPx;
            element.style.maxWidth = widthPx;
            element.style.height = heightPx;
            element.style.maxHeight = heightPx;
        }

        for (const canvas of surface.querySelectorAll("canvas")) {
            canvas.style.maxWidth = widthPx;
            canvas.style.maxHeight = heightPx;
        }
    }

    function resizeCharts(root) {
        if (!window.echarts || !window.echarts.getInstanceByDom) {
            return;
        }

        const candidates = [
            ...root.querySelectorAll("[_echarts_instance_]"),
            ...root.querySelectorAll(".dashboard-chart-surface > div")
        ];

        for (const element of candidates) {
            const chart = window.echarts.getInstanceByDom(element);
            if (chart) {
                chart.resize();
            }
        }
    }

    function dispose(rootId) {
        const observer = observers.get(rootId);
        if (observer) {
            observer.disconnect();
            observers.delete(rootId);
        }
    }

    return {
        observe,
        dispose
    };
})();