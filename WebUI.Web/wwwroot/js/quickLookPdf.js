import * as pdfjsLib from "../lib/pdfjs/build/pdf.mjs";

pdfjsLib.GlobalWorkerOptions.workerSrc = new URL(
    "../lib/pdfjs/build/pdf.worker.mjs",
    import.meta.url).toString();

const cMapUrl = new URL("../lib/pdfjs/cmaps/", import.meta.url).toString();
const standardFontDataUrl = new URL("../lib/pdfjs/standard_fonts/", import.meta.url).toString();
const wasmUrl = new URL("../lib/pdfjs/wasm/", import.meta.url).toString();

let attachedContainer = null;
let attachedUrl = null;
let resizeObserver = null;
let resizeTimer = null;
let loadingTask = null;
let pdfDocument = null;
let renderGeneration = 0;
let renderTasks = [];
let lastWidth = 0;

function cancelRenderTasks() {
    for (const task of renderTasks.splice(0)) {
        try {
            task.cancel();
        } catch {
            // Ein bereits abgeschlossener RenderTask benötigt keine weitere Aktion.
        }
    }
}

function setState(message, isError = false) {
    if (!attachedContainer) return;
    attachedContainer.replaceChildren();
    const state = document.createElement("div");
    state.className = `quick-look-pdf-state${isError ? " is-error" : ""}`;
    state.textContent = message;
    attachedContainer.appendChild(state);
}

function getScrollRatio() {
    if (!attachedContainer) return 0;
    const maximum = attachedContainer.scrollHeight - attachedContainer.clientHeight;
    if (maximum <= 0) return 0;
    return attachedContainer.scrollTop / maximum;
}

function restoreScrollRatio(ratio) {
    if (!attachedContainer) return;
    const maximum = attachedContainer.scrollHeight - attachedContainer.clientHeight;
    attachedContainer.scrollTop = maximum > 0 ? maximum * ratio : 0;
}

function scheduleRender() {
    if (!attachedContainer || !pdfDocument) return;
    if (resizeTimer !== null) window.clearTimeout(resizeTimer);
    resizeTimer = window.setTimeout(() => {
        resizeTimer = null;
        void renderAllPages();
    }, 120);
}

async function renderAllPages() {
    if (!attachedContainer || !pdfDocument) return;

    const width = attachedContainer.clientWidth;
    const height = attachedContainer.clientHeight;
    if (width <= 0 || height <= 0) return;
    if (width === lastWidth && attachedContainer.childElementCount > 0) return;

    lastWidth = width;
    const generation = ++renderGeneration;
    const scrollRatio = getScrollRatio();
    cancelRenderTasks();
    attachedContainer.replaceChildren();

    const availableWidth = Math.max(1, width - 2);
    const outputScale = Math.max(1, window.devicePixelRatio || 1);

    try {
        for (let pageNumber = 1; pageNumber <= pdfDocument.numPages; pageNumber++) {
            if (generation !== renderGeneration) return;

            const page = await pdfDocument.getPage(pageNumber);
            const baseViewport = page.getViewport({ scale: 1 });
            const scale = availableWidth / baseViewport.width;
            const viewport = page.getViewport({ scale });

            const pageShell = document.createElement("div");
            pageShell.className = "quick-look-pdf-page";
            pageShell.dataset.pageNumber = String(pageNumber);

            const canvas = document.createElement("canvas");
            canvas.setAttribute("aria-label", `PDF-Seite ${pageNumber}`);
            canvas.style.width = `${viewport.width}px`;
            canvas.style.height = `${viewport.height}px`;
            canvas.width = Math.max(1, Math.floor(viewport.width * outputScale));
            canvas.height = Math.max(1, Math.floor(viewport.height * outputScale));
            pageShell.appendChild(canvas);
            attachedContainer.appendChild(pageShell);

            const context = canvas.getContext("2d", { alpha: false });
            if (!context) throw new Error("Canvas-Kontext ist nicht verfügbar.");

            const transform = outputScale === 1
                ? null
                : [outputScale, 0, 0, outputScale, 0, 0];
            const task = page.render({
                canvasContext: context,
                transform,
                viewport
            });
            renderTasks.push(task);
            await task.promise;
            renderTasks = renderTasks.filter(item => item !== task);
        }

        if (generation === renderGeneration) {
            restoreScrollRatio(scrollRatio);
        }
    } catch (error) {
        if (generation !== renderGeneration) return;
        if (error?.name === "RenderingCancelledException") return;
        console.error("Quick-Look-PDF konnte nicht gerendert werden.", error);
        setState("Dokumentvorschau konnte nicht dargestellt werden.", true);
    }
}

async function loadDocument(url) {
    setState("Dokumentvorschau wird geladen …");
    loadingTask = pdfjsLib.getDocument({
        url,
        cMapUrl,
        cMapPacked: true,
        standardFontDataUrl,
        wasmUrl
    });

    try {
        pdfDocument = await loadingTask.promise;
        lastWidth = 0;
        await renderAllPages();
    } catch (error) {
        if (attachedUrl !== url) return;
        console.error("Quick-Look-PDF konnte nicht geladen werden.", error);
        setState("Dokumentvorschau konnte nicht geladen werden.", true);
    } finally {
        loadingTask = null;
    }
}

export function attach(container, url) {
    if (!container || !url) return;

    if (attachedContainer === container && attachedUrl === url) {
        scheduleRender();
        return;
    }

    disconnect();
    attachedContainer = container;
    attachedUrl = url;

    if (typeof ResizeObserver !== "undefined") {
        resizeObserver = new ResizeObserver(() => scheduleRender());
        resizeObserver.observe(container);
    }

    void loadDocument(url);
}

export function disconnect() {
    renderGeneration++;
    cancelRenderTasks();

    if (resizeTimer !== null) {
        window.clearTimeout(resizeTimer);
        resizeTimer = null;
    }

    resizeObserver?.disconnect();
    resizeObserver = null;

    if (loadingTask) {
        try {
            loadingTask.destroy();
        } catch {
            // Ein bereits abgeschlossener Ladevorgang benötigt keine weitere Aktion.
        }
        loadingTask = null;
    }

    if (pdfDocument) {
        try {
            void pdfDocument.destroy();
        } catch {
            // Beim Komponentenabbau ist keine weitere Aktion erforderlich.
        }
        pdfDocument = null;
    }

    if (attachedContainer) attachedContainer.replaceChildren();
    attachedContainer = null;
    attachedUrl = null;
    lastWidth = 0;
}
