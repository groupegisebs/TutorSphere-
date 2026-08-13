(function () {
  'use strict';

  var STORAGE_KEY = 'ts-pwa-install-dismissed';
  var DISMISS_DAYS = 14;
  var deferredPrompt = null;
  var blazorRef = null;

  function currentState() {
    return {
      canInstall: !!deferredPrompt,
      isStandalone: isStandalone(),
      isIos: isIos(),
      isDismissed: isDismissed()
    };
  }

  function pushState() {
    if (!blazorRef) return;
    try {
      blazorRef.invokeMethodAsync('OnPwaStateChanged', currentState());
    } catch (_) { /* circuit gone */ }
  }

  function isStandalone() {
    return window.matchMedia('(display-mode: standalone)').matches
      || window.navigator.standalone === true
      || document.referrer.indexOf('android-app://') === 0;
  }

  function isIos() {
    var ua = window.navigator.userAgent || '';
    var iOS = /iPad|iPhone|iPod/.test(ua)
      || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    return iOS && !window.MSStream;
  }

  function isDismissed() {
    try {
      var raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return false;
      var until = parseInt(raw, 10);
      if (!until || Date.now() > until) {
        localStorage.removeItem(STORAGE_KEY);
        return false;
      }
      return true;
    } catch (_) {
      return false;
    }
  }

  function dismiss(days) {
    try {
      var ms = (days || DISMISS_DAYS) * 24 * 60 * 60 * 1000;
      localStorage.setItem(STORAGE_KEY, String(Date.now() + ms));
    } catch (_) { /* private mode */ }
    pushState();
  }

  window.addEventListener('beforeinstallprompt', function (event) {
    event.preventDefault();
    deferredPrompt = event;
    var state = currentState();
    window.dispatchEvent(new CustomEvent('tutorsphere:pwa-installable', { detail: state }));
    pushState();
  });

  window.addEventListener('appinstalled', function () {
    deferredPrompt = null;
    try { localStorage.removeItem(STORAGE_KEY); } catch (_) { /* ignore */ }
    pushState();
  });

  window.tutorSpherePwa = {
    canInstall: function () { return !!deferredPrompt; },
    isStandalone: isStandalone,
    isIos: isIos,
    isDismissed: isDismissed,
    getState: currentState,
    dismiss: dismiss,
    clearDismiss: function () {
      try { localStorage.removeItem(STORAGE_KEY); } catch (_) { /* ignore */ }
      pushState();
    },
    bind: function (dotNetRef) {
      blazorRef = dotNetRef;
      pushState();
    },
    unbind: function () {
      blazorRef = null;
    },
    promptInstall: async function () {
      if (!deferredPrompt) return { outcome: 'unavailable' };
      deferredPrompt.prompt();
      var choice = await deferredPrompt.userChoice;
      deferredPrompt = null;
      pushState();
      return choice;
    }
  };

  window.addEventListener('load', function () {
    if (!('serviceWorker' in navigator)) return;
    navigator.serviceWorker.register('/service-worker.js').catch(function (err) {
      console.warn('[TutorSphere PWA] Service worker registration failed:', err);
    });
  });
})();
