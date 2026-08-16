/**
 * Charge CSS/JS à la demande (évite d'alourdir le shell mobile).
 */
window.tsPageAssets = (function () {
  var loaded = Object.create(null);

  function loadCss(href) {
    if (!href) return Promise.resolve();
    if (loaded[href]) return loaded[href];
    if (document.querySelector('link[data-ts-asset="' + href + '"]')) {
      loaded[href] = Promise.resolve();
      return loaded[href];
    }
    loaded[href] = new Promise(function (resolve, reject) {
      var link = document.createElement('link');
      link.rel = 'stylesheet';
      link.href = href;
      link.setAttribute('data-ts-asset', href);
      link.onload = function () { resolve(); };
      link.onerror = function () { reject(new Error('CSS failed: ' + href)); };
      document.head.appendChild(link);
    });
    return loaded[href];
  }

  function loadScript(src) {
    if (!src) return Promise.resolve();
    if (loaded[src]) return loaded[src];
    if (document.querySelector('script[data-ts-asset="' + src + '"]')) {
      loaded[src] = Promise.resolve();
      return loaded[src];
    }
    loaded[src] = new Promise(function (resolve, reject) {
      var s = document.createElement('script');
      s.src = src;
      s.async = false;
      s.setAttribute('data-ts-asset', src);
      s.onload = function () { resolve(); };
      s.onerror = function () { reject(new Error('Script failed: ' + src)); };
      document.body.appendChild(s);
    });
    return loaded[src];
  }

  async function loadMany(items) {
    var list = items || [];
    for (var i = 0; i < list.length; i++) {
      var item = list[i];
      if (!item) continue;
      if (typeof item === 'string') {
        if (item.endsWith('.css')) await loadCss(item);
        else await loadScript(item);
      } else if (item.css) {
        await loadCss(item.css);
      } else if (item.js) {
        await loadScript(item.js);
      }
    }
  }

  return {
    loadCss: loadCss,
    loadScript: loadScript,
    loadMany: loadMany,
    loadClassroom: function () {
      return loadMany([
        { css: 'css/classroom-pro.css' },
        { js: 'js/whiteboard.js' },
        { js: 'js/classroom-virtual-bg.js' },
        { js: 'js/classroom-media.js' },
        { js: 'js/classroom-rtc.js' }
      ]);
    },
    // Salle de réunion : mêmes moteurs média/WebRTC que la classe, sans la feuille de style des cours.
    loadMeetingRoom: function () {
      return loadMany([
        { js: 'js/whiteboard.js' },
        { js: 'js/classroom-virtual-bg.js' },
        { js: 'js/classroom-media.js' },
        { js: 'js/classroom-rtc.js' }
      ]);
    },
    loadLanding: function () {
      return loadMany([{ js: 'js/landing.js' }]);
    },
    loadPresentationRecorder: function () {
      return loadMany([{ js: 'js/presentation-recorder.js' }]);
    }
  };
})();

window.tsScrollToId = function (id) {
  var el = document.getElementById(id);
  if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
};
