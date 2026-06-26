// Auto-detect browser language on first visit (no culture cookie present).
// Maps navigator.language to a supported TutorSphere language code and sets
// the ASP.NET Core culture cookie, then reloads once so the server picks it up.
// On subsequent visits the cookie is already set, so this is a no-op.
(function () {
    var cookieName = '.AspNetCore.Culture';
    var hasCookie = document.cookie.split(';').some(function (c) {
        return c.trim().startsWith(cookieName + '=');
    });

    if (!hasCookie) {
        var raw = (navigator.languages && navigator.languages[0]) || navigator.language || 'fr';
        var code = raw.split('-')[0].toLowerCase();
        // zh-* variants map to zh-Hans
        if (raw.toLowerCase().startsWith('zh')) { code = 'zh-Hans'; }
        var supported = ['fr', 'en', 'es', 'de', 'pt', 'zh-Hans', 'ar'];
        if (!supported.includes(code)) { code = 'fr'; }
        var val = 'c=' + code + '|uic=' + code;
        document.cookie = cookieName + '=' + encodeURIComponent(val) + ';path=/;max-age=31536000;SameSite=Lax';
        window.location.reload();
    }
})();

window.tutorSphereCulture = {
    setCulture: function (culture) {
        var cookieValue = 'c=' + culture + '|uic=' + culture;
        document.cookie = '.AspNetCore.Culture=' + encodeURIComponent(cookieValue) + ';path=/;max-age=31536000;SameSite=Lax';
        window.location.reload();
    }
};
