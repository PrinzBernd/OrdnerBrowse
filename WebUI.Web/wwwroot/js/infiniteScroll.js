let scrollContainer;
let dotNetReference;
let scrollHandler;
let requestRunning = false;

function nearBottom() {
    if (!scrollContainer) {
        return false;
    }

    const remaining =
        scrollContainer.scrollHeight
        - scrollContainer.scrollTop
        - scrollContainer.clientHeight;

    return remaining < 500;
}

async function checkAndLoad() {
    if (!dotNetReference || requestRunning || !nearBottom()) {
        return;
    }

    requestRunning = true;

    try {
        await dotNetReference.invokeMethodAsync(
            "LoadMoreDocumentsFromScrollAsync");
    } finally {
        requestRunning = false;
    }
}

export function attach(element, reference) {
    if (!element) {
        return;
    }

    disconnect();

    scrollContainer = element;
    dotNetReference = reference;

    scrollHandler = () => {
        void checkAndLoad();
    };

    scrollContainer.addEventListener(
        "scroll",
        scrollHandler,
        { passive: true });

    requestAnimationFrame(() => {
        void checkAndLoad();
    });
}

export function disconnect() {
    if (scrollContainer && scrollHandler) {
        scrollContainer.removeEventListener(
            "scroll",
            scrollHandler);
    }

    scrollContainer = undefined;
    dotNetReference = undefined;
    scrollHandler = undefined;
    requestRunning = false;
}
