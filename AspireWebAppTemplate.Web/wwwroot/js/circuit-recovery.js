/**
 * Blazor Circuit Startup & Recovery
 *
 * 1. Starts Blazor with custom reconnection options.
 * 2. Auto-reloads the page when the circuit dies from unrecoverable render tree
 *    desync errors (e.g., "Cannot read properties of null (reading 'insertBefore')").
 *
 * Detection methods for recovery:
 * - MutationObserver: watches for the reconnect modal entering "failed" state.
 * - console.error intercept: triggers reload after 2+ "error applying batch" messages.
 */
(function () {
    "use strict";

    // --- Blazor Startup ---
    Blazor.start({
        circuit: {
            reconnectionOptions: {
                maxRetries: 5,
                retryIntervalMilliseconds: 2000
            }
        }
    });

    // --- Circuit Recovery ---
    var reloadScheduled = false;

    function scheduleReload(reason) {
        if (reloadScheduled) return;
        reloadScheduled = true;
        console.warn("[Blazor] " + reason + " — reloading page...");
        setTimeout(function () { location.reload(); }, 1000);
    }

    // Method 1: Observe the Blazor reconnect modal.
    // When the circuit is permanently dead, Blazor shows a modal with class "components-reconnect-failed".
    var observer = new MutationObserver(function () {
        var modal = document.getElementById("components-reconnect-modal");
        if (modal && modal.classList.contains("components-reconnect-failed")) {
            scheduleReload("Circuit permanently failed (reconnect modal)");
        }
    });

    function startObserving() {
        observer.observe(document.body, { childList: true, subtree: true, attributes: true });
    }

    if (document.body) {
        startObserving();
    } else {
        document.addEventListener("DOMContentLoaded", startObserving);
    }

    // Method 2: Intercept console.error for batch application failures.
    // The Blazor renderer logs "error applying batch N" when render diffs can't be applied
    // to the DOM (e.g., target node was removed by a concurrent page reload).
    var originalError = console.error;
    var batchErrorCount = 0;

    console.error = function () {
        originalError.apply(console, arguments);
        var msg = arguments.length > 0 ? String(arguments[0]) : "";
        if (msg.indexOf("error applying batch") !== -1) {
            batchErrorCount++;
            if (batchErrorCount >= 2) {
                scheduleReload("Multiple batch errors detected");
            }
        }
    };
})();
