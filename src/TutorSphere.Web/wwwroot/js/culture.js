// Persist browser / selected language via the ASP.NET Core culture cookie.
// InteractiveServer circuits read this cookie on connect — query-string culture alone is not enough.
(function () {
    var cookieName = '.AspNetCore.Culture';
    var hasCookie = document.cookie.split(';').some(function (c) {
        return c.trim().startsWith(cookieName + '=');
    });

    if (!hasCookie) {
        var raw = (navigator.languages && navigator.languages[0]) || navigator.language || 'fr';
        var code = raw.split('-')[0].toLowerCase();
        if (raw.toLowerCase().startsWith('zh')) { code = 'zh-Hans'; }
        var supported = ['fr', 'en', 'es', 'de', 'pt', 'zh-Hans', 'ar'];
        if (supported.indexOf(code) < 0) { code = 'fr'; }
        // Do not encodeURIComponent the whole value — CookieRequestCultureProvider expects "c=xx|uic=xx".
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
