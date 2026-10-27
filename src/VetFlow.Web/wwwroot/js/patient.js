(function () {
  const data = window.vetflowWeights;
  const el = document.getElementById('weight-chart');
  if (!data || !el || !window.Chart) return;
  new Chart(el, {
    type: 'line',
    data: { labels: data.labels, datasets: [{ data: data.values, borderColor: '#2f6f4f', backgroundColor: 'rgba(47,111,79,.12)', fill: true, tension: .3, pointRadius: 3 }] },
    options: { maintainAspectRatio: false, plugins: { legend: { display: false }, tooltip: { callbacks: { label: c => `${c.parsed.y} kg` } } },
      scales: { x: { grid: { display: false } }, y: { grid: { color: '#eef2ee' }, ticks: { callback: v => `${v} kg` } } } }
  });
})();
