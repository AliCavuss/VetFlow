// Booking form: patient autocomplete, default duration per type, free-slot picker.
(function () {
  'use strict';
  const form = document.getElementById('appointment-form');
  if (!form) return;

  const ignoreId = form.dataset.ignoreId || '';
  const $ = id => document.getElementById(id);
  const patientId = $('PatientId'), search = $('patient-search'), results = $('patient-results');
  const type = $('type'), vet = $('vet'), date = $('date'), duration = $('duration'), time = $('time'), slots = $('slots');
  const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

  // ---- Patient autocomplete ----
  let timer;
  search.addEventListener('input', () => {
    patientId.value = '0';
    clearTimeout(timer);
    const term = search.value.trim();
    if (term.length < 2) { results.hidden = true; return; }
    timer = setTimeout(async () => {
      const res = await fetch(`/api/patients/search?term=${encodeURIComponent(term)}`, { headers: { Accept: 'application/json' } });
      const hits = res.ok ? await res.json() : [];
      results.innerHTML = hits.length
        ? hits.map(h => `<button type="button" class="list-group-item list-group-item-action" data-id="${h.id}" data-label="${esc(h.name)} (${esc(h.ownerName)})">
            <strong>${esc(h.name)}</strong> <span class="text-muted small">· ${esc(h.species)}${h.breed ? ' · ' + esc(h.breed) : ''}</span><br>
            <span class="small">${esc(h.ownerName)} · ${esc(h.ownerPhone)}</span></button>`).join('')
        : '<div class="list-group-item text-muted small">No patient found.</div>';
      results.hidden = false;
    }, 250);
  });
  results.addEventListener('click', e => {
    const item = e.target.closest('[data-id]');
    if (!item) return;
    patientId.value = item.dataset.id;
    search.value = item.dataset.label;
    results.hidden = true;
  });
  document.addEventListener('click', e => { if (!results.contains(e.target) && e.target !== search) results.hidden = true; });

  // ---- Duration follows appointment type ----
  type.addEventListener('change', () => {
    const d = (window.vetflowDurations || {})[type.value];
    if (d) { duration.value = d; loadSlots(); }
  });

  // ---- Free slots ----
  async function loadSlots() {
    if (!vet.value || vet.value === '0' || !date.value) return;
    slots.innerHTML = '<span class="text-muted small">Loading free times…</span>';
    const url = `/api/slots?vetId=${vet.value}&date=${date.value}&duration=${duration.value || 30}${ignoreId ? '&ignoreId=' + ignoreId : ''}`;
    const res = await fetch(url, { headers: { Accept: 'application/json' } });
    if (!res.ok) { slots.innerHTML = '<span class="text-danger small">Could not load free times.</span>'; return; }
    const free = await res.json();
    if (time.value && !free.includes(time.value)) time.value = '';
    slots.innerHTML = free.length
      ? free.map(t => `<button type="button" class="slot-btn ${t === time.value ? 'selected' : ''}" data-time="${t}">${t}</button>`).join('')
      : '<span class="text-muted small">No free time on this day – try another day or veterinarian.</span>';
  }
  slots.addEventListener('click', e => {
    const btn = e.target.closest('[data-time]');
    if (!btn) return;
    time.value = btn.dataset.time;
    slots.querySelectorAll('.slot-btn').forEach(b => b.classList.toggle('selected', b === btn));
  });
  [vet, date, duration].forEach(el => el.addEventListener('change', loadSlots));
  loadSlots();
})();
