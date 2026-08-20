// Browser-side helpers for the Results panel's export/print actions.
// Invoked from Calculator.razor via IJSRuntime.
window.paycheckExport = {
    // Triggers a file download from base64-encoded bytes produced server-side.
    downloadFile: function (fileName, contentType, base64) {
        const binary = atob(base64);
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
        }
        const blob = new Blob([bytes], { type: contentType });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = fileName;
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
        // Revoke on the next tick so the download has a chance to start.
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    },

    // Opens the browser's native print dialog. A print stylesheet (@media print
    // in app.css) isolates the results so only the paycheck summary is printed.
    print: function () {
        window.print();
    }
};

// localStorage helpers backing SessionPaycheckStore / SessionBudgetStore, so anonymous
// saved paychecks and budgets survive a refresh or a closed tab. Every call is guarded:
// localStorage throws in some private-browsing modes and when the origin quota is full,
// and losing persistence must never break the calculator.
window.paycheckStorage = {
    get: function (key) {
        try {
            return window.localStorage.getItem(key);
        } catch (e) {
            return null;
        }
    },

    set: function (key, value) {
        try {
            window.localStorage.setItem(key, value);
        } catch (e) {
            // Quota exceeded or storage blocked — the in-memory store still works.
        }
    },

    remove: function (key) {
        try {
            window.localStorage.removeItem(key);
        } catch (e) {
            // Ignore; see set().
        }
    }
};
