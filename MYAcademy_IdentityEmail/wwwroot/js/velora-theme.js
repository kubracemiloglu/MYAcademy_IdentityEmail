(() => {
  'use strict';
  const root = document.documentElement;
  const key = 'velora.theme';
  const preferred = () => window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  let stored;
  try { stored = localStorage.getItem(key); } catch { /* Private storage may be unavailable. */ }
  const apply = theme => {
    root.dataset.veloraTheme = theme === 'dark' ? 'dark' : 'light';
    document.querySelectorAll('[data-velora-theme-toggle]').forEach(button => {
      const dark = root.dataset.veloraTheme === 'dark';
      button.setAttribute('aria-pressed', String(dark));
      button.setAttribute('aria-label', dark ? 'Açık temaya geç' : 'Koyu temaya geç');
      button.title = dark ? 'Açık temaya geç' : 'Koyu temaya geç';
      const icon = button.querySelector('i');
      if (icon) icon.className = dark ? 'bi bi-sun' : 'bi bi-moon-stars';
    });
    document.dispatchEvent(new CustomEvent('velora:themechange'));
  };
  apply(stored === 'dark' || stored === 'light' ? stored : preferred());
  document.addEventListener('DOMContentLoaded', () => {
    apply(root.dataset.veloraTheme);
    document.querySelectorAll('[data-velora-theme-toggle]').forEach(button => button.addEventListener('click', () => {
      const next = root.dataset.veloraTheme === 'dark' ? 'light' : 'dark';
      try { localStorage.setItem(key, next); } catch { /* Theme still works for this page. */ }
      apply(next);
    }));
  });
  window.addEventListener('storage', event => { if (event.key === key) apply(event.newValue || preferred()); });
})();
