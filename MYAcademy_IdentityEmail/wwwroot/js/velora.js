(() => {
  'use strict';
  const sidebar = document.getElementById('veloraSidebar');
  const toggle = document.querySelector('[data-velora-menu]');
  const close = () => { if (sidebar?.contains(document.activeElement)) toggle?.focus(); document.body.classList.remove('v-sidebar-open'); toggle?.setAttribute('aria-expanded', 'false'); };
  toggle?.addEventListener('click', () => {
    const open = document.body.classList.toggle('v-sidebar-open');
    toggle.setAttribute('aria-expanded', String(open));
    if (open) sidebar?.querySelector('a')?.focus();
  });
  document.querySelector('[data-velora-backdrop]')?.addEventListener('click', close);
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape') { close(); document.querySelectorAll('details[open]').forEach(item => item.open = false); }
    if (event.key === 'Tab' && document.body.classList.contains('v-sidebar-open')) {
      const links = sidebar?.querySelectorAll('a,button');
      if (links?.length && event.shiftKey && document.activeElement === links[0]) { event.preventDefault(); links[links.length-1].focus(); }
      else if (links?.length && !event.shiftKey && document.activeElement === links[links.length-1]) { event.preventDefault(); links[0].focus(); }
    }
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') { event.preventDefault(); document.querySelector('[data-velora-search]')?.focus(); }
  });
  window.matchMedia('(min-width: 1101px)').addEventListener('change', event => { if (event.matches) close(); });
  document.querySelectorAll('[data-velora-sort]').forEach(select => select.addEventListener('change', () => { const url = new URL(window.location.href); url.searchParams.set('sort', select.value); window.location.assign(url); }));
  document.querySelectorAll('form[data-confirm]').forEach(form => form.addEventListener('submit', event => { if (!window.confirm(form.dataset.confirm)) event.preventDefault(); }));
  const autoReadForms = document.querySelectorAll('form[data-auto-read]');
  const markVisibleRead = async form => {
    if (form.dataset.saved === 'true' || getComputedStyle(form.closest('.v-detail')).display === 'none') return;
    try {
      const response = await fetch(form.action, { method:'POST', body:new FormData(form), headers:{ 'X-Requested-With':'XMLHttpRequest' }, credentials:'same-origin' });
      if (!response.ok || !(await response.json()).success) return;
      form.dataset.saved = 'true';
      const id = form.dataset.messageId;
      document.querySelectorAll('[data-message-id]').forEach(item => { if (item.dataset.messageId === id) { item.classList.add('is-read'); const label = item.querySelector('.v-sr'); if (label) label.textContent = 'Okundu'; } });
      document.querySelectorAll('[data-velora-unread]').forEach(item => { const count = Number(item.textContent); if (Number.isFinite(count)) item.textContent = Math.max(0, count - 1); });
      const flag = document.querySelector('[data-read-label]'); if (flag) flag.textContent = 'Okundu';
    } catch { /* Do not show a successful read state when the server did not save it. */ }
  };
  autoReadForms.forEach(markVisibleRead);
  window.matchMedia('(min-width: 768px)').addEventListener('change', () => autoReadForms.forEach(markVisibleRead));
  const chartElement = document.getElementById('veloraTraffic');
  const chartData = document.getElementById('veloraTrafficData');
  if (chartElement && chartData && typeof Chart !== 'undefined') {
    const data = JSON.parse(chartData.textContent);
    const colors = () => getComputedStyle(document.documentElement);
    const chart = new Chart(chartElement, { type:'line', data:{ labels:data.labels, datasets:[
      { label:'Gelen', data:data.received, borderColor:'#8054F4', backgroundColor:'#8054F422', pointRadius:4, borderWidth:3, tension:.35, fill:true },
      { label:'Gönderilen', data:data.sent, borderColor:'#D5F45B', backgroundColor:'#D5F45B22', pointRadius:4, borderWidth:3, tension:.35, fill:true }
    ] }, options:{ responsive:true, maintainAspectRatio:false, animation:false, plugins:{ legend:{ labels:{ color:colors().getPropertyValue('--v-text') } } }, scales:{ y:{ beginAtZero:true, ticks:{ precision:0, color:colors().getPropertyValue('--v-muted') }, grid:{ color:colors().getPropertyValue('--v-line') } }, x:{ ticks:{ color:colors().getPropertyValue('--v-muted') }, grid:{ display:false } } } } });
    document.addEventListener('velora:themechange', () => {
      chart.options.plugins.legend.labels.color = colors().getPropertyValue('--v-text');
      chart.options.scales.y.ticks.color = colors().getPropertyValue('--v-muted'); chart.options.scales.x.ticks.color = colors().getPropertyValue('--v-muted'); chart.options.scales.y.grid.color = colors().getPropertyValue('--v-line'); chart.update('none');
    });
  }
})();
