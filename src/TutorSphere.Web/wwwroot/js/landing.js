(function () {
    let revealObserver = null;

    function observeRevealElements() {
        const revealEls = document.querySelectorAll('.lp-reveal:not(.lp-reveal--visible)');
        if (!revealEls.length) return;

        if ('IntersectionObserver' in window) {
            if (!revealObserver) {
                revealObserver = new IntersectionObserver(
                    (entries) => {
                        entries.forEach((entry) => {
                            if (entry.isIntersecting) {
                                entry.target.classList.add('lp-reveal--visible');
                                revealObserver.unobserve(entry.target);
                            }
                        });
                    },
                    { threshold: 0.12, rootMargin: '0px 0px -40px 0px' }
                );
            }
            revealEls.forEach((el) => revealObserver.observe(el));
        } else {
            revealEls.forEach((el) => el.classList.add('lp-reveal--visible'));
        }
    }

    function bindAnchorLinks() {
        if (window.__landingAnchorsBound) return;
        window.__landingAnchorsBound = true;

        document.querySelectorAll('a[href^="#"]').forEach((link) => {
            link.addEventListener('click', (e) => {
                const id = link.getAttribute('href');
                if (!id || id === '#') return;
                const target = document.querySelector(id);
                if (target) {
                    e.preventDefault();
                    target.scrollIntoView({ behavior: 'smooth', block: 'start' });
                }
            });
        });
    }

    window.landingScrollCarousel = function (elementId, direction) {
        const el = document.getElementById(elementId);
        if (!el) return;
        const card = el.querySelector('.lp-benefit-card');
        const gap = 16;
        const amount = card ? card.offsetWidth + gap : 320;
        el.scrollBy({ left: direction * amount, behavior: 'smooth' });
    };

    // Safe to call multiple times — Blazor may render .lp-reveal nodes after DOMContentLoaded.
    window.landingInit = function () {
        document.documentElement.style.scrollBehavior = 'smooth';
        observeRevealElements();
        bindAnchorLinks();
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => window.landingInit());
    } else {
        window.landingInit();
    }
})();
