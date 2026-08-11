// Persist language via the ASP.NET Core culture cookie (.AspNetCore.Culture).
// Default first visit = French (product default). User can change via LanguageSelector.
(function () {
    var cookieName = '.AspNetCore.Culture';

    function readCultureCookie() {
        var parts = document.cookie.split(';');
        for (var i = 0; i < parts.length; i++) {
            var c = parts[i].trim();
            if (c.indexOf(cookieName + '=') === 0) {
                try { return decodeURIComponent(c.substring(cookieName.length + 1)); }
                catch (e) { return c.substring(cookieName.length + 1); }
            }
        }
        return null;
    }

    function isValidCultureCookie(value) {
        return typeof value === 'string' && /^c=[^|]+\|uic=[^|]+$/.test(value);
    }

    function writeCultureCookie(code) {
        var val = 'c=' + code + '|uic=' + code;
        var secure = window.location.protocol === 'https:' ? ';Secure' : '';
        document.cookie = cookieName + '=' + val + ';path=/;max-age=31536000;SameSite=Lax' + secure;
    }

    var existing = readCultureCookie();
    if (!isValidCultureCookie(existing)) {
        writeCultureCookie('fr');
        window.location.reload();
        return;
    }
})();

window.tutorSphereCulture = {
    setCulture: function (culture) {
        var val = 'c=' + culture + '|uic=' + culture;
        var secure = window.location.protocol === 'https:' ? ';Secure' : '';
        document.cookie = '.AspNetCore.Culture=' + val + ';path=/;max-age=31536000;SameSite=Lax' + secure;
        window.location.reload();
    }
};
