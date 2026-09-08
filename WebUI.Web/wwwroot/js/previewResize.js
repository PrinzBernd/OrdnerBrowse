let current = null;

function sameElements(layout, frame, image, handle) {
    return current &&
        current.layout === layout &&
        current.frame === frame &&
        current.image === image &&
        current.handle === handle;
}

export function attach(layout, frame, image, handle, minimumHeight, maximumHeight, minimumSideWidth, gapWidth) {
    if (!layout || !frame || !image || !handle) return;

    const minHeight = Number.isFinite(minimumHeight) ? minimumHeight : 170;
    const maxHeight = Number.isFinite(maximumHeight) ? maximumHeight : 460;
    const minSideWidth = Number.isFinite(minimumSideWidth) ? minimumSideWidth : 220;
    const gap = Number.isFinite(gapWidth) ? gapWidth : 16;

    // Blazor rendert regelmäßig neu. Solange dieselben DOM-Elemente vorhanden sind,
    // bleiben die Ereignisse verbunden und werden nicht unnötig getrennt.
    if (sameElements(layout, frame, image, handle)) {
        current.minHeight = minHeight;
        current.maxHeight = maxHeight;
        current.minSideWidth = minSideWidth;
        current.gap = gap;
        current.scheduleLayoutUpdate();
        return;
    }

    disconnect();

    let startY = 0;
    let startHeight = 0;
    let activePointerId = null;
    let animationFrame = 0;

    const state = {
        layout,
        frame,
        image,
        handle,
        minHeight,
        maxHeight,
        minSideWidth,
        gap,
        scheduleLayoutUpdate: null,
        cleanup: null
    };

    const updateLayout = () => {
        animationFrame = 0;

        if (!layout.isConnected || !frame.isConnected || !handle.isConnected) return;

        if (!layout.classList.contains("has-side-details")) {
            layout.classList.remove("details-below");
            layout.style.removeProperty("--preview-column-width");
            return;
        }

        const frameStyle = getComputedStyle(frame);
        const horizontalPadding =
            (parseFloat(frameStyle.paddingLeft) || 0) +
            (parseFloat(frameStyle.paddingRight) || 0);
        const verticalPadding =
            (parseFloat(frameStyle.paddingTop) || 0) +
            (parseFloat(frameStyle.paddingBottom) || 0);
        const handleHeight = handle.getBoundingClientRect().height || 16;
        const usableImageHeight = Math.max(1, frame.clientHeight - verticalPadding - handleHeight);
        const ratio = image.naturalWidth > 0 && image.naturalHeight > 0
            ? image.naturalWidth / image.naturalHeight
            : 0.72;
        const requiredPreviewWidth = Math.max(190, Math.ceil(usableImageHeight * ratio + horizontalPadding));
        const availableWidth = layout.clientWidth;
        const requiredTotalWidth = requiredPreviewWidth + state.minSideWidth + state.gap;
        const stack = availableWidth < requiredTotalWidth;

        layout.classList.toggle("details-below", stack);
        if (stack) {
            layout.style.removeProperty("--preview-column-width");
            return;
        }

        // Die Vorschau erhält standardmäßig etwa 65 %, darf aber nie kleiner als
        // die für das aktuelle Seitenverhältnis tatsächlich benötigte Breite sein.
        const preferredPreviewWidth = Math.round(availableWidth * 0.65);
        const maximumPreviewWidth = Math.max(190, availableWidth - state.minSideWidth - state.gap);
        const previewWidth = Math.min(
            maximumPreviewWidth,
            Math.max(requiredPreviewWidth, preferredPreviewWidth));
        layout.style.setProperty("--preview-column-width", `${previewWidth}px`);
    };

    const scheduleLayoutUpdate = () => {
        if (animationFrame) return;
        animationFrame = requestAnimationFrame(updateLayout);
    };
    state.scheduleLayoutUpdate = scheduleLayoutUpdate;

    const finishDrag = event => {
        if (activePointerId === null || event.pointerId !== activePointerId) return;
        const pointerId = activePointerId;
        activePointerId = null;
        document.body.classList.remove("preview-resizing");
        if (handle.hasPointerCapture?.(pointerId)) {
            try { handle.releasePointerCapture(pointerId); } catch { }
        }
        scheduleLayoutUpdate();
        event.preventDefault();
    };

    const onPointerMove = event => {
        if (activePointerId === null || event.pointerId !== activePointerId) return;
        const nextHeight = Math.min(
            state.maxHeight,
            Math.max(state.minHeight, startHeight + event.clientY - startY));
        frame.style.height = `${Math.round(nextHeight)}px`;
        scheduleLayoutUpdate();
        event.preventDefault();
    };

    const onPointerDown = event => {
        if (event.button !== 0 || activePointerId !== null) return;
        activePointerId = event.pointerId;
        startY = event.clientY;
        startHeight = frame.getBoundingClientRect().height;
        document.body.classList.add("preview-resizing");
        try { handle.setPointerCapture(event.pointerId); } catch { }
        event.preventDefault();
        event.stopPropagation();
    };

    const onLostPointerCapture = event => finishDrag(event);
    const onImageLoad = () => scheduleLayoutUpdate();
    const observer = new ResizeObserver(scheduleLayoutUpdate);
    observer.observe(layout);
    observer.observe(frame);

    handle.addEventListener("pointerdown", onPointerDown);
    handle.addEventListener("pointermove", onPointerMove, { passive: false });
    handle.addEventListener("pointerup", finishDrag, { passive: false });
    handle.addEventListener("pointercancel", finishDrag, { passive: false });
    handle.addEventListener("lostpointercapture", onLostPointerCapture, { passive: false });
    image.addEventListener("load", onImageLoad);

    scheduleLayoutUpdate();

    state.cleanup = () => {
        if (animationFrame) cancelAnimationFrame(animationFrame);
        observer.disconnect();
        handle.removeEventListener("pointerdown", onPointerDown);
        handle.removeEventListener("pointermove", onPointerMove);
        handle.removeEventListener("pointerup", finishDrag);
        handle.removeEventListener("pointercancel", finishDrag);
        handle.removeEventListener("lostpointercapture", onLostPointerCapture);
        image.removeEventListener("load", onImageLoad);
        layout.classList.remove("details-below");
        layout.style.removeProperty("--preview-column-width");
        document.body.classList.remove("preview-resizing");
    };

    current = state;
}

export function disconnect() {
    current?.cleanup?.();
    current = null;
}
