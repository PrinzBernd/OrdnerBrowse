let cleanup = [];
let attachedDialog = null;

function addCleanup(callback) {
    cleanup.push(callback);
}

function clamp(value, minimum, maximum) {
    return Math.min(maximum, Math.max(minimum, value));
}

function bindPointerDrag(handle, onStart, onMove) {
    const pointerDown = event => {
        if (event.button !== 0) return;
        if (event.target.closest("button, a, input, select, textarea")) return;

        event.preventDefault();
        handle.setPointerCapture(event.pointerId);
        const state = onStart(event);

        const pointerMove = moveEvent => {
            if (moveEvent.pointerId !== event.pointerId) return;
            onMove(moveEvent, state);
        };

        const pointerUp = upEvent => {
            if (upEvent.pointerId !== event.pointerId) return;
            if (handle.hasPointerCapture(event.pointerId)) {
                handle.releasePointerCapture(event.pointerId);
            }
            handle.removeEventListener("pointermove", pointerMove);
            handle.removeEventListener("pointerup", pointerUp);
            handle.removeEventListener("pointercancel", pointerUp);
        };

        handle.addEventListener("pointermove", pointerMove);
        handle.addEventListener("pointerup", pointerUp);
        handle.addEventListener("pointercancel", pointerUp);
    };

    handle.addEventListener("pointerdown", pointerDown);
    addCleanup(() => handle.removeEventListener("pointerdown", pointerDown));
}

function keepDialogVisible(dialog) {
    const rect = dialog.getBoundingClientRect();
    const margin = 12;
    const left = clamp(rect.left, margin, Math.max(margin, window.innerWidth - rect.width - margin));
    const top = clamp(rect.top, margin, Math.max(margin, window.innerHeight - rect.height - margin));
    dialog.style.left = `${left}px`;
    dialog.style.top = `${top}px`;
}

export function attach(dialog, content, divider, dotNetReference, minDialogWidth, minDialogHeight, minInfoWidth, maxInfoWidth) {
    if (!dialog || !content || !divider) return;

    // Blazor kann nach einer normalen Taste erneut rendern. Derselbe Dialog darf dabei
    // weder neu gebunden noch auf seine Standardgröße zurückgesetzt werden.
    if (attachedDialog === dialog) return;

    disconnect();
    attachedDialog = dialog;

    const margin = 18;
    const defaultWidth = Math.min(900, Math.max(minDialogWidth, window.innerWidth * 0.56));
    const defaultHeight = Math.min(920, Math.max(minDialogHeight, window.innerHeight * 0.90));
    const explorer = document.querySelector(".explorer");
    const titleAnchor = document.querySelector("[data-quick-look-anchor]");
    const explorerRect = explorer?.getBoundingClientRect();
    const titleRect = titleAnchor?.getBoundingClientRect();
    const defaultLeft = titleRect ? titleRect.left : (explorerRect ? explorerRect.left : margin);
    const defaultTop = explorerRect ? explorerRect.top : margin;

    dialog.style.width = `${Math.min(defaultWidth, window.innerWidth - margin * 2)}px`;
    dialog.style.height = `${Math.min(defaultHeight, window.innerHeight - margin * 2)}px`;
    dialog.style.left = `${defaultLeft}px`;
    dialog.style.top = `${defaultTop}px`;
    const layoutBreakpoint = 520;
    const minInfoHeight = 140;
    const minPreviewHeight = 180;

    content.style.setProperty("--quick-look-info-width", "340px");
    content.style.setProperty("--quick-look-info-height", "220px");

    const updateLayoutMode = () => {
        const useRows = dialog.getBoundingClientRect().width <= layoutBreakpoint;
        content.dataset.quickLookLayout = useRows ? "rows" : "columns";

        if (useRows) {
            const contentHeight = content.getBoundingClientRect().height;
            const currentInfoHeight = parseFloat(getComputedStyle(content).getPropertyValue("--quick-look-info-height")) || 220;
            const maximumInfoHeight = Math.max(minInfoHeight, contentHeight - minPreviewHeight);
            if (currentInfoHeight > maximumInfoHeight) {
                content.style.setProperty("--quick-look-info-height", `${maximumInfoHeight}px`);
            }
        } else {
            const dialogWidth = dialog.getBoundingClientRect().width;
            const currentInfoWidth = parseFloat(getComputedStyle(content).getPropertyValue("--quick-look-info-width")) || 340;
            const maximumInfoWidth = Math.max(minInfoWidth, Math.min(maxInfoWidth, dialogWidth - 300));
            if (currentInfoWidth > maximumInfoWidth) {
                content.style.setProperty("--quick-look-info-width", `${maximumInfoWidth}px`);
            }
        }
    };

    updateLayoutMode();

    const header = dialog.querySelector("[data-quick-look-drag-handle]");
    if (header) {
        bindPointerDrag(
            header,
            event => {
                const rect = dialog.getBoundingClientRect();
                return { startX: event.clientX, startY: event.clientY, left: rect.left, top: rect.top };
            },
            (event, state) => {
                const rect = dialog.getBoundingClientRect();
                const left = clamp(state.left + event.clientX - state.startX, margin, Math.max(margin, window.innerWidth - rect.width - margin));
                const top = clamp(state.top + event.clientY - state.startY, margin, Math.max(margin, window.innerHeight - rect.height - margin));
                dialog.style.left = `${left}px`;
                dialog.style.top = `${top}px`;
            });
    }

    bindPointerDrag(
        divider,
        event => ({
            layout: content.dataset.quickLookLayout || "columns",
            startX: event.clientX,
            startY: event.clientY,
            startInfoWidth: parseFloat(getComputedStyle(content).getPropertyValue("--quick-look-info-width")) || 340,
            startInfoHeight: parseFloat(getComputedStyle(content).getPropertyValue("--quick-look-info-height")) || 220
        }),
        (event, state) => {
            if (state.layout === "rows") {
                const contentHeight = content.getBoundingClientRect().height;
                const allowedMaximum = Math.max(minInfoHeight, contentHeight - minPreviewHeight);
                const infoHeight = clamp(
                    state.startInfoHeight - (event.clientY - state.startY),
                    minInfoHeight,
                    allowedMaximum);
                content.style.setProperty("--quick-look-info-height", `${infoHeight}px`);
                return;
            }

            const dialogWidth = dialog.getBoundingClientRect().width;
            const allowedMaximum = Math.min(maxInfoWidth, dialogWidth - 300);
            const infoWidth = clamp(
                state.startInfoWidth - (event.clientX - state.startX),
                minInfoWidth,
                Math.max(minInfoWidth, allowedMaximum));
            content.style.setProperty("--quick-look-info-width", `${infoWidth}px`);
        });

    for (const handle of dialog.querySelectorAll("[data-quick-look-resize]")) {
        const direction = handle.dataset.quickLookResize || "";
        bindPointerDrag(
            handle,
            event => {
                const rect = dialog.getBoundingClientRect();
                return {
                    startX: event.clientX,
                    startY: event.clientY,
                    left: rect.left,
                    top: rect.top,
                    width: rect.width,
                    height: rect.height
                };
            },
            (event, state) => {
                const dx = event.clientX - state.startX;
                const dy = event.clientY - state.startY;
                let left = state.left;
                let top = state.top;
                let width = state.width;
                let height = state.height;

                if (direction.includes("e")) width = state.width + dx;
                if (direction.includes("s")) height = state.height + dy;
                if (direction.includes("w")) {
                    width = state.width - dx;
                    left = state.left + dx;
                }
                if (direction.includes("n")) {
                    height = state.height - dy;
                    top = state.top + dy;
                }

                const maxWidth = window.innerWidth - margin * 2;
                const maxHeight = window.innerHeight - margin * 2;
                const clampedWidth = clamp(width, minDialogWidth, maxWidth);
                const clampedHeight = clamp(height, minDialogHeight, maxHeight);

                if (direction.includes("w")) left = state.left + (state.width - clampedWidth);
                if (direction.includes("n")) top = state.top + (state.height - clampedHeight);

                left = clamp(left, margin, window.innerWidth - clampedWidth - margin);
                top = clamp(top, margin, window.innerHeight - clampedHeight - margin);

                dialog.style.left = `${left}px`;
                dialog.style.top = `${top}px`;
                dialog.style.width = `${clampedWidth}px`;
                dialog.style.height = `${clampedHeight}px`;

                const currentInfoWidth = parseFloat(getComputedStyle(content).getPropertyValue("--quick-look-info-width")) || 340;
                const maximumInfoWidth = Math.max(minInfoWidth, clampedWidth - 300);
                if (currentInfoWidth > maximumInfoWidth) {
                    content.style.setProperty("--quick-look-info-width", `${maximumInfoWidth}px`);
                }

                updateLayoutMode();
            });
    }

    const outsidePointerDown = event => {
        if (dialog.contains(event.target)) return;
        if (!dotNetReference) return;

        // Der Klick darf das darunterliegende Bedienelement weiterhin erreichen.
        // Deshalb wird weder preventDefault noch stopPropagation verwendet.
        void dotNetReference.invokeMethodAsync("CloseQuickLookFromOutsideClick");
    };

    document.addEventListener("pointerdown", outsidePointerDown, true);
    addCleanup(() => document.removeEventListener("pointerdown", outsidePointerDown, true));

    const resizeListener = () => {
        keepDialogVisible(dialog);
        updateLayoutMode();
    };
    window.addEventListener("resize", resizeListener);
    addCleanup(() => window.removeEventListener("resize", resizeListener));

    if (typeof ResizeObserver !== "undefined") {
        const resizeObserver = new ResizeObserver(() => updateLayoutMode());
        resizeObserver.observe(dialog);
        addCleanup(() => resizeObserver.disconnect());
    }
}

export function disconnect() {
    for (const callback of cleanup.splice(0)) callback();
    attachedDialog = null;
}
