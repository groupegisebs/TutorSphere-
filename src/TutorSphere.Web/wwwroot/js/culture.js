// Persist browser / selected language via the ASP.NET Core culture cookie.
// InteractiveServer circuits read this cookie on connect — query-string culture alone is not enough.
(function () {
    var cookieName = '.AspNetCore.Culture';
    function readCultureCookie() {
        var parts = document.cookie.split(';');
        for (var i = 0; i < parts.length; i++) {
            var c = parts[i].trim();
            if (c.indexOf(cookieName + '=') === 0) {
                return decodeURIComponent(c.substring(cookieName.length + 1));
            }
        }
        return null;
    }

    function isValidCultureCookie(value) {
        // Expected: c=fr|uic=fr  (reject legacy/malformed encodings)
        return typeof value === 'string' && /^c=[^|]+\|uic=[^|]+$/.test(value);
    }

    var existing = readCultureCookie();
    if (!isValidCultureCookie(existing)) {
        var raw = (navigator.languages && navigator.languages[0]) || navigator.language || 'fr';
        var code = raw.split('-')[0].toLowerCase();
        if (raw.toLowerCase().startsWith('zh')) { code = 'zh-Hans'; }
        var supported = ['fr', 'en', 'es', 'de', 'pt', 'zh-Hans', 'ar'];
        if (supported.indexOf(code) < 0) { code = 'fr'; }
        var val = 'c=' + code + '|uic=' + code;
        var secure = window.location.protocol === 'https:' ? ';Secure' : '';
        document.cookie = cookieName + '=' + val + ';path=/;max-age=31536000;SameSite=Lax' + secure;
        window.location.reload();
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
