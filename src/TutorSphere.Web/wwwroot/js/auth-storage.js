window.tsAuth = {
    save: function (json) {
        try { sessionStorage.setItem('ts_auth', json); } catch (_) { }
    },
    load: function () {
        try { return sessionStorage.getItem('ts_auth'); } catch (_) { return null; }
    },
    clear: function () {
        try { sessionStorage.removeItem('ts_auth'); } catch (_) { }
    },
    /** Saves to sessionStorage and mirrors JWT into an HttpOnly cookie via the BFF endpoint. */
    persist: async function (json) {
        this.save(json);
        try {
            var auth = JSON.parse(json);
            if (!auth || !auth.token)
                return;
            await fetch('/bff/auth/establish', {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ token: auth.token, expiresAt: auth.expiresAt })
            });
        } catch (_) { }
    },
    /** Clears sessionStorage and HttpOnly auth cookie. */
    clearAll: async function () {
        this.clear();
        try {
            await fetch('/bff/auth/logout', { method: 'POST', credentials: 'same-origin' });
        } catch (_) { }
    }
};
