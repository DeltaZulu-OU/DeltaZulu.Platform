window.huntingDashboardGridLayout = (() => {
    const registrations = new Map();
    const columnCount = 12;
    const rowHeight = 96;

    function initialize(gridId, dotNetReference) {
        const gridShell = document.getElementById(gridId);
        if (!gridShell) {
            return;
        }

        dispose(gridId);

        const registration = {
            gridShell,
            dotNetReference,
            cleanups: []
        };

        const pointerDown = event => {
            if (!isEditMode(gridShell)) {
                return;
            }

            const resizeEdge = event.target.closest(".dashboard-widget-resize-edge");
            const dragSurface = event.target.closest(".dashboard-widget-drag-surface");
            const dragHandle = event.target.closest(".dashboard-widget-drag-handle");
            const canDragFromSurface = dragSurface && !isDragIgnoredTarget(event.target);

            if (!resizeEdge && !dragHandle && !canDragFromSurface) {
                return;
            }

            const widget = event.target.closest("[data-dashboard-widget-id]");
            const gridItem = findGridItem(widget);
            const grid = gridShell.querySelector(".dashboard-coordinate-grid");
            if (!widget || !gridItem || !grid || !grid.contains(gridItem)) {
                return;
            }

            event.preventDefault();
            event.stopPropagation();

            const operation = resizeEdge
                ? `resize-${resizeEdge.dataset.resizeEdge || "bottom"}`
                : "move";

            startOperation(registration, grid, gridItem, widget, operation, event);
        };

        const dragStart = event => {
            if (event.target.closest(".dashboard-widget-resize-edge")
                || event.target.closest(".dashboard-widget-drag-handle")
                || (event.target.closest(".dashboard-widget-drag-surface") && !isDragIgnoredTarget(event.target))) {
                event.preventDefault();
                event.stopPropagation();
            }
        };

        gridShell.addEventListener("pointerdown", pointerDown);
        gridShell.addEventListener("dragstart", dragStart);
        registration.cleanups.push(() => gridShell.removeEventListener("pointerdown", pointerDown));
        registration.cleanups.push(() => gridShell.removeEventListener("dragstart", dragStart));

        registrations.set(gridId, registration);
    }

    function isEditMode(gridShell) {
        return gridShell.dataset.dashboardEditMode === "true";
    }

    function isDragIgnoredTarget(target) {
        return Boolean(target.closest(
            "[data-dashboard-drag-ignore], button, a, input, textarea, select, option, label, .mud-button-root, .mud-menu, .mud-popover"));
    }

    function findGridItem(widget) {
        if (!widget) {
            return null;
        }

        return widget.closest("[data-dashboard-layout-item]")
            || widget.closest(".dashboard-layout-item")
            || widget;
    }

    function startOperation(registration, grid, gridItem, widget, operation, event) {
        const gridMetrics = getGridMetrics(grid);
        const start = {
            pointerX: event.clientX,
            pointerY: event.clientY,
            x: readInt(gridItem, "layoutX", readInt(widget, "layoutX", 0)),
            y: readInt(gridItem, "layoutY", readInt(widget, "layoutY", 0)),
            width: readInt(gridItem, "layoutWidth", readInt(widget, "layoutWidth", 4)),
            height: readInt(gridItem, "layoutHeight", readInt(widget, "layoutHeight", 3)),
            minWidth: readInt(gridItem, "layoutMinWidth", readInt(widget, "layoutMinWidth", 1)),
            minHeight: readInt(gridItem, "layoutMinHeight", readInt(widget, "layoutMinHeight", 1))
        };

        const blockedLayouts = getBlockedLayouts(grid, gridItem);
        const startLayout = {
            x: start.x,
            y: start.y,
            width: start.width,
            height: start.height
        };
        let latest = createLayoutResult(gridItem, widget, startLayout, startLayout);

        widget.classList.add("dashboard-widget-layout-active");
        gridItem.classList.add("dashboard-layout-item-active");

        if (event.target.setPointerCapture) {
            try {
                event.target.setPointerCapture(event.pointerId);
            } catch {
                // Pointer capture is best-effort; document-level listeners keep dragging usable.
            }
        }

        const move = moveEvent => {
            const deltaColumns = Math.round((moveEvent.clientX - start.pointerX) / gridMetrics.columnStep);
            const deltaRows = Math.round((moveEvent.clientY - start.pointerY) / gridMetrics.rowStep);
            const candidate = calculateLayout(operation, start, deltaColumns, deltaRows);
            const resolved = operation === "move"
                ? resolveMoveLayouts(gridItem, widget, candidate, startLayout, blockedLayouts)
                : createLayoutResult(gridItem, widget, findNearestValidLayout(start, candidate, blockedLayouts), startLayout);

            if (!sameLayoutResult(resolved, latest)) {
                latest = resolved;
                applyLayoutResult(latest);
            }
        };

        const up = () => {
            document.removeEventListener("pointermove", move);
            document.removeEventListener("pointerup", up);
            document.removeEventListener("pointercancel", up);

            widget.classList.remove("dashboard-widget-layout-active");
            gridItem.classList.remove("dashboard-layout-item-active");

            const layoutChanges = latest.layouts
                .filter(isChangedLayout)
                .map(toInteropLayoutChange)
                .filter(change => change.widgetId);

            if (layoutChanges.length > 0) {
                registration.dotNetReference.invokeMethodAsync(
                    "UpdateWidgetLayoutsAsync",
                    layoutChanges);
            }

            resizeDashboardCharts();
        };

        document.addEventListener("pointermove", move);
        document.addEventListener("pointerup", up);
        document.addEventListener("pointercancel", up);
    }

    function calculateLayout(operation, start, deltaColumns, deltaRows) {
        switch (operation) {
            case "resize-right":
                return {
                    x: start.x,
                    y: start.y,
                    width: clamp(start.width + deltaColumns, start.minWidth, columnCount - start.x),
                    height: start.height
                };

            case "resize-left": {
                const maxX = start.x + start.width - start.minWidth;
                const x = clamp(start.x + deltaColumns, 0, maxX);
                return {
                    x,
                    y: start.y,
                    width: start.width + start.x - x,
                    height: start.height
                };
            }

            case "resize-bottom":
                return {
                    x: start.x,
                    y: start.y,
                    width: start.width,
                    height: Math.max(start.minHeight, start.height + deltaRows)
                };

            case "resize-top": {
                const maxY = start.y + start.height - start.minHeight;
                const y = clamp(start.y + deltaRows, 0, maxY);
                return {
                    x: start.x,
                    y,
                    width: start.width,
                    height: start.height + start.y - y
                };
            }

            case "move":
            default:
                return {
                    x: clamp(start.x + deltaColumns, 0, columnCount - start.width),
                    y: Math.max(0, start.y + deltaRows),
                    width: start.width,
                    height: start.height
                };
        }
    }

    function getBlockedLayouts(grid, activeGridItem) {
        return [...grid.querySelectorAll("[data-dashboard-layout-item]")]
            .filter(item => item !== activeGridItem)
            .map(item => {
                const widget = item.querySelector("[data-dashboard-widget-id]");

                return {
                    gridItem: item,
                    widget,
                    widgetId: widget?.dataset.dashboardWidgetId || "",
                    x: readInt(item, "layoutX", 0),
                    y: readInt(item, "layoutY", 0),
                    width: readInt(item, "layoutWidth", 1),
                    height: readInt(item, "layoutHeight", 1),
                    originalX: readInt(item, "layoutX", 0),
                    originalY: readInt(item, "layoutY", 0),
                    originalWidth: readInt(item, "layoutWidth", 1),
                    originalHeight: readInt(item, "layoutHeight", 1)
                };
            })
            .filter(isPositiveLayout);
    }

    function findNearestValidLayout(start, candidate, blockedLayouts) {
        if (isValidLayout(candidate, blockedLayouts)) {
            return candidate;
        }

        let latest = {
            x: start.x,
            y: start.y,
            width: start.width,
            height: start.height
        };

        const steps = Math.max(
            Math.abs(candidate.x - start.x),
            Math.abs(candidate.y - start.y),
            Math.abs(candidate.width - start.width),
            Math.abs(candidate.height - start.height));

        if (steps <= 0) {
            return latest;
        }

        for (let step = 1; step <= steps; step++) {
            const proposed = {
                x: Math.round(start.x + ((candidate.x - start.x) * step / steps)),
                y: Math.round(start.y + ((candidate.y - start.y) * step / steps)),
                width: Math.round(start.width + ((candidate.width - start.width) * step / steps)),
                height: Math.round(start.height + ((candidate.height - start.height) * step / steps))
            };

            if (sameLayout(proposed, latest)) {
                continue;
            }

            if (!isValidLayout(proposed, blockedLayouts)) {
                break;
            }

            latest = proposed;
        }

        return latest;
    }

    function resolveMoveLayouts(activeGridItem, activeWidget, candidate, originalActiveLayout, blockedLayouts) {
        const active = createLayoutItem(
            activeGridItem,
            activeWidget,
            activeWidget?.dataset.dashboardWidgetId || "",
            candidate,
            originalActiveLayout);
        const resolved = [active];
        const orderedBlocked = blockedLayouts
            .map(blocked => createLayoutItem(blocked.gridItem, blocked.widget, blocked.widgetId, blocked, {
                x: blocked.originalX,
                y: blocked.originalY,
                width: blocked.originalWidth,
                height: blocked.originalHeight
            }))
            .sort(compareLayoutPosition);

        for (const blocked of orderedBlocked) {
            const resolvedBlocked = moveLayoutBelowOverlaps(blocked, resolved);
            resolved.push(resolvedBlocked);
        }

        return { layouts: resolved };
    }

    function moveLayoutBelowOverlaps(layout, placedLayouts) {
        const resolved = { ...layout };
        let guard = 0;

        while (guard < 1000) {
            const overlap = placedLayouts.find(placed => rectanglesOverlap(resolved, placed));
            if (!overlap) {
                return resolved;
            }

            resolved.y = Math.max(resolved.y + 1, overlap.y + overlap.height);
            guard++;
        }

        return resolved;
    }

    function createLayoutResult(gridItem, widget, layout, originalLayout) {
        const item = createLayoutItem(gridItem, widget, widget?.dataset.dashboardWidgetId || "", layout, originalLayout);

        return { layouts: [item] };
    }

    function createLayoutItem(gridItem, widget, widgetId, layout, originalLayout) {
        return {
            gridItem,
            widget,
            widgetId,
            x: layout.x,
            y: layout.y,
            width: layout.width,
            height: layout.height,
            originalX: originalLayout.x,
            originalY: originalLayout.y,
            originalWidth: originalLayout.width,
            originalHeight: originalLayout.height
        };
    }

    function compareLayoutPosition(first, second) {
        if (first.y !== second.y) {
            return first.y - second.y;
        }

        if (first.x !== second.x) {
            return first.x - second.x;
        }

        return first.widgetId.localeCompare(second.widgetId);
    }

    function sameLayoutResult(first, second) {
        if (first.layouts.length !== second.layouts.length) {
            return false;
        }

        return first.layouts.every((layout, index) => {
            const other = second.layouts[index];
            return other
                && layout.widgetId === other.widgetId
                && sameLayout(layout, other);
        });
    }

    function isChangedLayout(layout) {
        return layout.x !== layout.originalX
            || layout.y !== layout.originalY
            || layout.width !== layout.originalWidth
            || layout.height !== layout.originalHeight;
    }

    function applyLayoutResult(result) {
        for (const layout of result.layouts) {
            if (layout.gridItem && layout.widget) {
                applyLayout(layout.gridItem, layout.widget, layout);
            }
        }
    }

    function toInteropLayoutChange(layout) {
        return {
            widgetId: layout.widgetId,
            x: layout.x,
            y: layout.y,
            width: layout.width,
            height: layout.height
        };
    }

    function isValidLayout(layout, blockedLayouts) {
        return isPositiveLayout(layout)
            && layout.x >= 0
            && layout.y >= 0
            && layout.x + layout.width <= columnCount
            && !blockedLayouts.some(blocked => rectanglesOverlap(layout, blocked));
    }

    function isPositiveLayout(layout) {
        return layout.width > 0 && layout.height > 0;
    }

    function rectanglesOverlap(first, second) {
        return first.x < second.x + second.width
            && first.x + first.width > second.x
            && first.y < second.y + second.height
            && first.y + first.height > second.y;
    }

    function sameLayout(first, second) {
        return first.x === second.x
            && first.y === second.y
            && first.width === second.width
            && first.height === second.height;
    }

    function getGridMetrics(grid) {
        const styles = window.getComputedStyle(grid);
        const columnGap = parseFloat(styles.columnGap || "0") || 0;
        const rowGap = parseFloat(styles.rowGap || "0") || 0;
        const usableWidth = grid.clientWidth - (columnGap * (columnCount - 1));
        const columnWidth = Math.max(1, usableWidth / columnCount);

        return {
            columnStep: columnWidth + columnGap,
            rowStep: rowHeight + rowGap
        };
    }

    function applyLayout(gridItem, widget, layout) {
        gridItem.style.gridColumn = `${layout.x + 1} / span ${layout.width}`;
        gridItem.style.gridRow = `${layout.y + 1} / span ${layout.height}`;
        gridItem.style.minHeight = `${layout.height * rowHeight}px`;

        setLayoutDataset(gridItem, layout);
        setLayoutDataset(widget, layout);

        resizeDashboardCharts();
    }

    function resizeDashboardCharts() {
        if (window.echarts && window.echarts.getInstanceByDom) {
            for (const element of document.querySelectorAll("[_echarts_instance_]")) {
                const chart = window.echarts.getInstanceByDom(element);
                if (chart) {
                    chart.resize();
                }
            }
        }
    }

    function setLayoutDataset(element, layout) {
        element.dataset.layoutX = layout.x.toString();
        element.dataset.layoutY = layout.y.toString();
        element.dataset.layoutWidth = layout.width.toString();
        element.dataset.layoutHeight = layout.height.toString();
    }

    function readInt(element, name, fallback) {
        if (!element) {
            return fallback;
        }

        const value = Number.parseInt(element.dataset[name], 10);
        return Number.isFinite(value) ? value : fallback;
    }

    function clamp(value, min, max) {
        return Math.min(Math.max(value, min), max);
    }

    function dispose(gridId) {
        const registration = registrations.get(gridId);
        if (!registration) {
            return;
        }

        for (const cleanup of registration.cleanups) {
            cleanup();
        }

        registrations.delete(gridId);
    }

    return {
        initialize,
        dispose
    };
})();