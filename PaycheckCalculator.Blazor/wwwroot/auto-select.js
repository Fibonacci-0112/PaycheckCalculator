// Auto-selects the whole value of an input as soon as it takes focus, so moving from field to
// field (tab, arrow key, or click) leaves the field ready to be typed over instead of making the
// user clear the previous value first.
//
// Self-installing and fully delegated off `document`, so it also covers inputs Blazor renders
// later (deduction rows, schema-driven state fields, budget categories) with no per-field wiring.
// Opt a single field out with a `data-no-autoselect` attribute.
(function () {
    "use strict";

    // Text-ish fields only. Checkboxes, radios, and the file/color/date pickers either have no
    // text to select or throw on select(). Password fields are left out on purpose: re-focusing
    // one is usually a correction, not a retype.
    var SELECTABLE_TYPES = ["text", "number", "tel", "search", "url", "email"];

    var pendingClickTarget = null;

    function shouldAutoSelect(el) {
        if (!el || el.tagName !== "INPUT" || el.readOnly || el.disabled) {
            return false;
        }
        if (el.hasAttribute("data-no-autoselect")) {
            return false;
        }
        var type = (el.getAttribute("type") || "text").toLowerCase();
        return SELECTABLE_TYPES.indexOf(type) !== -1;
    }

    document.addEventListener("focusin", function (e) {
        var el = e.target;
        if (!shouldAutoSelect(el)) {
            return;
        }

        try {
            el.select();
        } catch (err) {
            // Some browsers refuse select() on certain input types; leave the field as-is.
            return;
        }

        // A click focuses the field on mousedown and then collapses the selection to a caret on
        // mouseup. Remember the element so the matching mouseup can be suppressed; a click-drag
        // is unaffected because that selection is applied during mousemove.
        pendingClickTarget = el;
    });

    document.addEventListener("mouseup", function (e) {
        if (pendingClickTarget && e.target === pendingClickTarget) {
            e.preventDefault();
        }
        pendingClickTarget = null;
    });

    document.addEventListener("focusout", function () {
        pendingClickTarget = null;
    });
})();
