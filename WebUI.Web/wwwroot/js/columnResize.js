let cleanup = null;

export function attach(
    container,
    minimumAreaWidth = 150,
    minimumCorrespondentWidth = 150,
    minimumDocumentTypeWidth = 150,
    minimumDocumentWidth = 210) {
    disconnect();

    if (!container) return;

    const handles = Array.from(container.querySelectorAll('[data-column-resizer]'));
    const columns = Array.from(container.querySelectorAll(':scope > .column'));
    if (handles.length !== 3 || columns.length !== 4) return;

    let active = null;

    const rememberOpenWidth = column => {
        if (column.classList.contains('collapsed')) return;
        const width = Math.round(column.getBoundingClientRect().width);
        if (width > 0) column.dataset.openWidth = String(width);
    };

    const documentColumn = columns[3];

    const updateDocumentColumnForCollapsedColumns = () => {
        const hasCollapsedNavigationColumn = columns
            .slice(0, 3)
            .some(column => column.classList.contains('collapsed'));

        if (hasCollapsedNavigationColumn) {
            // Eine zuvor per Ziehgriff festgelegte Pixelbreite darf den frei
            // gewordenen Platz nicht blockieren. Während Navigationsspalten
            // eingeklappt sind, wächst die Dokumentenspalte daher flexibel.
            if (documentColumn.style.flex && !documentColumn.dataset.openFlexBeforeCollapse) {
                documentColumn.dataset.openFlexBeforeCollapse = documentColumn.style.flex;
            }
            documentColumn.style.removeProperty('flex');
        } else if (documentColumn.dataset.openFlexBeforeCollapse) {
            documentColumn.style.setProperty(
                'flex',
                documentColumn.dataset.openFlexBeforeCollapse,
                'important');
            delete documentColumn.dataset.openFlexBeforeCollapse;
        }
    };

    const applyCollapseState = column => {
        if (column.classList.contains('collapsed')) {
            // Inline-flex aus dem Ziehen würde die CSS-Breite der eingeklappten
            // Spalte überstimmen. Deshalb wird sie während des Einklappens entfernt.
            if (column.style.flex) column.dataset.openFlex = column.style.flex;
            column.style.removeProperty('flex');
        } else if (column.dataset.openFlex) {
            column.style.setProperty('flex', column.dataset.openFlex, 'important');
            delete column.dataset.openFlex;
        } else if (column.dataset.openWidth) {
            column.style.setProperty('flex', `0 0 ${column.dataset.openWidth}px`, 'important');
        }

        updateDocumentColumnForCollapsedColumns();
    };

    const observers = columns.slice(0, 3).map(column => {
        const observer = new MutationObserver(() => applyCollapseState(column));
        observer.observe(column, { attributes: true, attributeFilter: ['class'] });
        return observer;
    });

    updateDocumentColumnForCollapsedColumns();

    const stop = () => {
        if (!active) return;
        active.handle.classList.remove('is-dragging');
        container.classList.remove('is-resizing');
        rememberOpenWidth(active.left);
        rememberOpenWidth(active.right);
        active = null;
    };

    const move = event => {
        if (!active || event.pointerId !== active.pointerId) return;

        const delta = event.clientX - active.startX;
        const combined = active.leftWidth + active.rightWidth;
        const minimumWidths = [
            minimumAreaWidth,
            minimumCorrespondentWidth,
            minimumDocumentTypeWidth,
            minimumDocumentWidth
        ];
        const leftMinimum = minimumWidths[active.leftIndex];
        const rightMinimum = minimumWidths[active.rightIndex];
        const leftWidth = Math.max(leftMinimum, Math.min(combined - rightMinimum, active.leftWidth + delta));
        const rightWidth = combined - leftWidth;

        active.left.style.setProperty('flex', `0 0 ${Math.round(leftWidth)}px`, 'important');
        active.right.style.setProperty('flex', `0 0 ${Math.round(rightWidth)}px`, 'important');
        active.left.dataset.openWidth = String(Math.round(leftWidth));
        active.right.dataset.openWidth = String(Math.round(rightWidth));
        event.preventDefault();
    };

    const starts = handles.map((handle, index) => {
        const start = event => {
            if (event.button !== 0) return;

            const left = columns[index];
            const right = columns[index + 1];
            if (left.classList.contains('collapsed') || right.classList.contains('collapsed')) return;

            const leftRect = left.getBoundingClientRect();
            const rightRect = right.getBoundingClientRect();

            active = {
                pointerId: event.pointerId,
                startX: event.clientX,
                left,
                right,
                leftIndex: index,
                rightIndex: index + 1,
                leftWidth: leftRect.width,
                rightWidth: rightRect.width,
                handle
            };

            handle.setPointerCapture?.(event.pointerId);
            handle.classList.add('is-dragging');
            container.classList.add('is-resizing');
            event.preventDefault();
        };

        handle.addEventListener('pointerdown', start);
        return [handle, start];
    });

    window.addEventListener('pointermove', move, { passive: false });
    window.addEventListener('pointerup', stop);
    window.addEventListener('pointercancel', stop);
    window.addEventListener('blur', stop);

    cleanup = () => {
        stop();
        observers.forEach(observer => observer.disconnect());
        for (const [handle, start] of starts) handle.removeEventListener('pointerdown', start);
        window.removeEventListener('pointermove', move);
        window.removeEventListener('pointerup', stop);
        window.removeEventListener('pointercancel', stop);
        window.removeEventListener('blur', stop);
    };
}

export function disconnect() {
    cleanup?.();
    cleanup = null;
}
