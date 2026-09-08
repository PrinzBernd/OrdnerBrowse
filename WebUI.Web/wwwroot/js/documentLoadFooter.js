let documentsColumn = null;
let resizeObserver = null;
let mutationObserver = null;
let scheduledFrame = 0;

const hiddenClass = "document-load-footer-hidden-for-height";

function scheduleEvaluation() {
    if (scheduledFrame !== 0) {
        return;
    }

    scheduledFrame = window.requestAnimationFrame(() => {
        scheduledFrame = 0;
        evaluateFooterVisibility();
    });
}

function getDirectChild(element, selector) {
    if (!element) {
        return null;
    }

    return Array.from(element.children).find(child => child.matches(selector)) ?? null;
}

function evaluateFooterVisibility() {
    if (!documentsColumn) {
        return;
    }

    const header = getDirectChild(documentsColumn, "header");
    const search = getDirectChild(documentsColumn, ".document-search-shell");
    const list = getDirectChild(documentsColumn, ".documents-scroll");
    const footer = getDirectChild(documentsColumn, ".document-load-footer");

    if (!footer || !list) {
        return;
    }

    const firstDocumentRow = list.querySelector(".document-row-shell");

    if (!firstDocumentRow) {
        footer.classList.remove(hiddenClass);
        return;
    }

    const wasHidden = footer.classList.contains(hiddenClass);

    // Für die Messung wird die Zeile innerhalb desselben Browser-Bildaufbaus
    // kurz in ihre natürliche Größe gesetzt. Dadurch entsteht kein sichtbares
    // Flackern.
    footer.classList.remove(hiddenClass);

    const columnHeight = documentsColumn.clientHeight;
    const headerHeight = header?.getBoundingClientRect().height ?? 0;
    const searchHeight = search?.getBoundingClientRect().height ?? 0;
    const footerHeight = footer.getBoundingClientRect().height;
    const documentRowHeight = firstDocumentRow.getBoundingClientRect().height;

    const remainingListHeight =
        columnHeight - headerHeight - searchHeight - footerHeight;

    // Die Statuszeile bleibt sichtbar, solange darunter mindestens eine
    // vollständige Dokumentzeile Platz hat.
    const shouldHide =
        remainingListHeight + 0.5 < documentRowHeight;

    footer.classList.toggle(hiddenClass, shouldHide);

    if (wasHidden !== shouldHide) {
        scheduleEvaluation();
    }
}

export function attach(columnElement) {
    disconnect();

    documentsColumn = columnElement;

    resizeObserver = new ResizeObserver(scheduleEvaluation);
    resizeObserver.observe(documentsColumn);

    mutationObserver = new MutationObserver(scheduleEvaluation);
    mutationObserver.observe(documentsColumn, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: ["class"]
    });

    window.addEventListener("resize", scheduleEvaluation, { passive: true });
    scheduleEvaluation();
}

export function disconnect() {
    if (scheduledFrame !== 0) {
        window.cancelAnimationFrame(scheduledFrame);
        scheduledFrame = 0;
    }

    window.removeEventListener("resize", scheduleEvaluation);

    resizeObserver?.disconnect();
    mutationObserver?.disconnect();

    resizeObserver = null;
    mutationObserver = null;

    if (documentsColumn) {
        const footer = getDirectChild(
            documentsColumn,
            ".document-load-footer"
        );

        footer?.classList.remove(hiddenClass);
    }

    documentsColumn = null;
}
