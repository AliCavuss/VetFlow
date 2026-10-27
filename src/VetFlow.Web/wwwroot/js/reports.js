(function () {
  const data = window.vetflowMonths;
  const el = document.getElementById('month-chart');
  if (!data || !el || !window.Chart) return;
  const fmt = v => new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 0 }).format(v);
  new Chart(el, {
    type: 'bar',
    data: { labels: data.labels, datasets: [{ data: data.values, backgroundColor: data.values.map((_, i) => i === data.values.length - 1 ? '#e8a33d' : '#2f6f4f'), borderRadius: 6, maxBarThickness: 36 }] },
    options: {
      maintainAspectRatio: false,
      plugins: { legend: { display: false }, tooltip: { callbacks: { label: ctx => fmt(ctx.parsed.y) } } },
      scales: { x: { grid: { display: false } }, y: { beginAtZero: true, grid: { color: '#eef2ee' }, ticks: { callback: v => fmt(v) } } }
    }
  });
})();
