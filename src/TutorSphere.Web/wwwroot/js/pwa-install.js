(function () {
  'use strict';

  let deferredPrompt = null;

  window.addEventListener('beforeinstallprompt', (event) => {
    event.preventDefault();
    deferredPrompt = event;
    window.dispatchEvent(new CustomEvent('tutorsphere:pwa-installable', { detail: { prompt: deferredPrompt } }));
  });

  window.tutorSpherePwa = {
    canInstall: function () {
      return !!deferredPrompt;
    },
    promptInstall: async function () {
      if (!deferredPrompt) return { outcome: 'unavailable' };
      deferredPrompt.prompt();
      const choice = await deferredPrompt.userChoice;
      deferredPrompt = null;
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
