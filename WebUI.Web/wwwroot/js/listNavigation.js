export function scrollTo(elementId) {
    const element = document.getElementById(elementId);
    if (!element) return;
    element.scrollIntoView({ block: "nearest", inline: "nearest", behavior: "auto" });
    element.focus({ preventScroll: true });
}

function focusColumnElement(column) {
    if (!column) return false;

    if (column.classList.contains("collapsed")) {
        const rail = column.querySelector(".collapsed-rail");
        const target = rail || column;
        target.focus({ preventScroll: true });
        target.scrollIntoView({ block: "nearest", inline: "nearest", behavior: "auto" });
        return true;
    }

    const selected = column.querySelector(".row.selected, .document-row-shell.selected .document-row");
    const firstRow = column.querySelector(".row");
    const target = selected || firstRow || column;
    target.focus({ preventScroll: true });
    target.scrollIntoView({ block: "nearest", inline: "nearest", behavior: "auto" });
    return true;
}

export function focusColumn(columnName) {
    return focusColumnElement(document.querySelector(`[data-column="${columnName}"]`));
}

export function focusAdjacentColumn(currentColumnName, direction) {
    const columns = Array.from(document.querySelectorAll("[data-column]"));
    const currentIndex = columns.findIndex(column => column.dataset.column === currentColumnName);
    if (currentIndex < 0) return false;

    const targetIndex = currentIndex + direction;
    if (targetIndex < 0 || targetIndex >= columns.length) return false;
    return focusColumnElement(columns[targetIndex]);
}
