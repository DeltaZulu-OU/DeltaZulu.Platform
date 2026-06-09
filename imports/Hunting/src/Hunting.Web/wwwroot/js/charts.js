window.huntingCharts = window.huntingCharts || {
    getElementSize: (element) => {
        if (!element) return { width: 0, height: 0 };
        const rect = element.getBoundingClientRect();
        return {
            width: Number.isFinite(rect.width) ? rect.width : 0,
            height: Number.isFinite(rect.height) ? rect.height : 0
        };
    }
};
