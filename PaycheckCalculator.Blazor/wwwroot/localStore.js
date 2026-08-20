// Browser local-storage glue for the anonymous (signed-out) session. It only moves opaque
// strings in and out of window.localStorage — all serialization happens in C# so the JSON
// converters that keep state-input values as real CLR primitives stay in charge.
//
// Every call is wrapped: Safari private browsing and "block third-party cookies" style
// settings can make localStorage throw on access, and a storage failure must never break
// the calculator.
window.paycheckLocalStore = {
    get: function (key) {
        try {
            return window.localStorage.getItem(key);
        } catch {
            return null;
        }
    },
    set: function (key, value) {
        try {
            window.localStorage.setItem(key, value);
            return true;
        } catch {
            // Quota exceeded or storage disabled — the session simply stays in-memory.
            return false;
        }
    },
    remove: function (key) {
        try {
            window.localStorage.removeItem(key);
            return true;
        } catch {
            return false;
        }
    }
};
