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
