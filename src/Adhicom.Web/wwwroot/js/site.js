// Adhicom — comportements front (léger, aucun framework requis)
(function () {
  'use strict';

  // Menu mobile du site public
  var toggle = document.querySelector('.menu-toggle');
  var mobileMenu = document.getElementById('mobile-menu');
  if (toggle && mobileMenu) {
    toggle.addEventListener('click', function () { mobileMenu.classList.toggle('open'); });
  }

  // Auto-fermeture des toasts
  document.querySelectorAll('.toast').forEach(function (el) {
    setTimeout(function () { el.style.opacity = '0'; setTimeout(function () { el.remove(); }, 350); }, 4200);
  });

  // Confirmation avant action sensible (boutons avec data-confirm)
  document.querySelectorAll('form[data-confirm]').forEach(function (form) {
    form.addEventListener('submit', function (e) {
      var message = form.getAttribute('data-confirm') || 'Confirmer cette action ?';
      if (!window.confirm(message)) e.preventDefault();
    });
  });

  // Vérification en direct de la disponibilité du sous-domaine
  var slugInput = document.getElementById('RequestedSlug');
  var slugHint = document.getElementById('slug-hint');
  if (slugInput && slugHint) {
    var timer = null;
    var checkSlug = function () {
      var value = (slugInput.value || '').trim().toLowerCase();
      if (value.length < 3) { slugHint.textContent = ''; return; }
      fetch('/app/stores/check-slug?slug=' + encodeURIComponent(value), { headers: { 'Accept': 'application/json' } })
        .then(function (r) { return r.ok ? r.json() : null; })
        .then(function (data) {
          if (!data) return;
          slugHint.textContent = data.available ? '✓ « ' + value + '.adhicom.com » est disponible' : '✗ Ce nom est déjà pris ou réservé';
          slugHint.className = data.available ? 'form-hint ok' : 'form-hint err';
        })
        .catch(function () { /* silencieux */ });
    };
    slugInput.addEventListener('input', function () { clearTimeout(timer); timer = setTimeout(checkSlug, 350); });
  }

  // Aperçu de l'image sélectionnée (logo / bannière / produits)
  document.querySelectorAll('input[type=file][data-preview]').forEach(function (input) {
    input.addEventListener('change', function () {
      var target = document.querySelector(input.getAttribute('data-preview'));
      if (!target || !input.files || !input.files[0]) return;
      var url = URL.createObjectURL(input.files[0]);
      if (target.tagName === 'IMG') target.src = url;
    });
  });

  // Étapeurs de quantité (panier)
  document.querySelectorAll('[data-stepper]').forEach(function (group) {
    var input = group.querySelector('input[type=number]');
    group.querySelectorAll('[data-step]').forEach(function (btn) {
      btn.addEventListener('click', function () {
        var step = parseInt(btn.getAttribute('data-step'), 10) || 0;
        var next = Math.max(0, (parseInt(input.value, 10) || 0) + step);
        input.value = next;
        var form = input.closest('form');
        if (form) form.submit();
      });
    });
  });

  // Sélection de variante (fiche produit)
  document.querySelectorAll('.variant-pill').forEach(function (pill) {
    pill.addEventListener('click', function () {
      pill.closest('.variant-group').querySelectorAll('.variant-pill').forEach(function (p) { p.classList.remove('selected'); });
      pill.classList.add('selected');
      var radio = pill.querySelector('input[type=radio]');
      if (radio) radio.checked = true;
    });
  });

  // Copie rapide (numéro de commande, lien de la boutique)
  document.querySelectorAll('[data-copy]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var text = btn.getAttribute('data-copy');
      navigator.clipboard && navigator.clipboard.writeText(text).then(function () {
        var original = btn.textContent;
        btn.textContent = 'Copié !';
        setTimeout(function () { btn.textContent = original; }, 1600);
      });
    });
  });
})();

// ---- Compléments sans inline handlers (CSP script-src 'self') ----
// Selects avec data-auto-submit : soumission automatique au changement
document.querySelectorAll('select[data-auto-submit]').forEach(function (el) {
  el.addEventListener('change', function () {
    if (el.form) el.form.submit();
  });
});
// Galerie produit : miniature → image principale
document.querySelectorAll('.gallery-thumb').forEach(function (thumb) {
  thumb.addEventListener('click', function () {
    var main = document.getElementById('main-img');
    var full = thumb.getAttribute('data-full');
    if (main && full) {
      main.src = full;
      var group = thumb.closest('.gallery-thumbs');
      if (group) group.querySelectorAll('.gallery-thumb').forEach(function (t) { t.classList.toggle('active', t === thumb); });
    }
  });
});
// Images distantes cassées : masquage discret
document.querySelectorAll('img.logo-img, img.banner-img, img.lazy-img').forEach(function (img) {
  img.addEventListener('error', function () { img.style.display = 'none'; });
});
