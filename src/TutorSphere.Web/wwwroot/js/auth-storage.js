window.tsAuth = {
    save: function (json) {
        try { sessionStorage.setItem('ts_auth', json); } catch (_) { }
    },
    load: function () {
        try { return sessionStorage.getItem('ts_auth'); } catch (_) { return null; }
    },
    clear: function () {
        try { sessionStorage.removeItem('ts_auth'); } catch (_) { }
    }
};
