// Payments of the last 30 days (bar chart on the dashboard).
(function () {
  const data = window.vetflowRevenue;
  const el = document.getElementById('revenue-chart');
  if (!data || !el || !window.Chart) return;

  const fmt = v => new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 0 }).format(v);
  new Chart(el, {
    type: 'bar',
    data: { labels: data.labels, datasets: [{ data: data.values, backgroundColor: '#2f6f4f', borderRadius: 4, maxBarThickness: 18 }] },
    options: {
      maintainAspectRatio: false,
      plugins: { legend: { display: false }, tooltip: { callbacks: { label: ctx => fmt(ctx.parsed.y) } } },
      scales: {
        x: { grid: { display: false }, ticks: { maxTicksLimit: 8, color: '#63736b' } },
        y: { beginAtZero: true, grid: { color: '#eef2ee' }, ticks: { color: '#63736b', callback: v => fmt(v) } }
      }
    }
  });
})();
