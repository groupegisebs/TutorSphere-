window.tutorSphereCulture = {
    setCulture: function (culture) {
        var cookieValue = 'c=' + culture + '|uic=' + culture;
        document.cookie = '.AspNetCore.Culture=' + encodeURIComponent(cookieValue) + ';path=/;max-age=31536000;SameSite=Lax';
        window.location.reload();
    }
};
