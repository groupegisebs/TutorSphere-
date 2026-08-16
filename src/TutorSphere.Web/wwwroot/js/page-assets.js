/**
 * Charge CSS/JS à la demande (évite d'alourdir le shell mobile).
 */
window.tsPageAssets = (function () {
  var loaded = Object.create(null);

  /** Traduit un chemin logique en URL empreintée (window.tsAssetMap, alimenté par App.razor). */
  function assetUrl(path) {
    var map = window.tsAssetMap;
    return (map && map[path]) || path;
  }

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
      link.href = assetUrl(href);
      link.setAttribute('data-ts-asset', href);
      link.onload = function () { resolve(); };
      link.onerror = function () {
        // Ne pas mémoriser l'échec : un incident réseau ne doit pas condamner la page entière.
        delete loaded[href];
        reject(new Error('CSS failed: ' + href));
      };
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
      s.src = assetUrl(src);
      s.async = false;
      s.setAttribute('data-ts-asset', src);
      s.onload = function () { resolve(); };
      s.onerror = function () {
        delete loaded[src];
        reject(new Error('Script failed: ' + src));
      };
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

  // Moteurs média/WebRTC : le nom de la globale attendue permet de vérifier que le script s'est bien exécuté.
  var mediaEngines = [
    { js: 'js/whiteboard.js', global: 'whiteboard' },
    { js: 'js/classroom-virtual-bg.js', global: 'classroomVirtualBg' },
    { js: 'js/classroom-media.js', global: 'classroomMedia' },
    { js: 'js/classroom-rtc.js', global: 'classroomRtc' }
  ];

  /**
   * Un <script> peut se charger sans définir sa globale (erreur d'exécution, réponse tronquée,
   * fichier servi depuis un cache périmé). On revérifie et on retente une fois hors cache.
   */
  async function loadEngines() {
    for (var i = 0; i < mediaEngines.length; i++) {
      var item = mediaEngines[i];
      if (window[item.global]) continue;
      try {
        await loadScript(item.js);
      } catch (e) {
        /* réessai ci-dessous */
      }
      if (!window[item.global]) {
        delete loaded[item.js];
        var node = document.querySelector('script[data-ts-asset="' + item.js + '"]');
        if (node && node.parentNode) node.parentNode.removeChild(node);
        try {
          await loadScript(item.js + '?r=' + Date.now());
        } catch (e2) {
          /* signalé via missing */
        }
      }
    }
    var missing = mediaEngines
      .filter(function (i) { return !window[i.global]; })
      .map(function (i) { return i.global; });
    return { ok: missing.length === 0, missing: missing };
  }

  return {
    loadCss: loadCss,
    loadScript: loadScript,
    loadMany: loadMany,
    loadClassroom: async function () {
      await loadCss('css/classroom-pro.css');
      return loadEngines();
    },
    // Salle de réunion : mêmes moteurs média/WebRTC que la classe, sans la feuille de style des cours.
    loadMeetingRoom: function () {
      return loadEngines();
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
