async function loadMetrics() {
  state.metrics = await SeniorCareAuth.api("/api/analytics/recent?days=7");
  const today = state.metrics[0];
  if (today) {
    elements.adherence.textContent = formatAdherence(today);
    elements.doseSummary.textContent = formatDoses(today);
    elements.etlTime.textContent = new Intl.DateTimeFormat("es-GT", { hour: "2-digit", minute: "2-digit" }).format(new Date(today.etlRunAt));
  }
  elements.metricsList.innerHTML = state.metrics.length === 0
    ? "<p>El ETL está preparando la primera actualización.</p>"
    : state.metrics.map((metric) => `<article class="metric-row"><strong>${escapeHtml(formatDate(metric.metricDate))}</strong><div><div class="metric-bar"><span style="width: ${Math.min(metric.adherencePercentage ?? 0, 100)}%"></span></div><p>${formatDoses(metric, true)} · ${metric.emergencyAlerts} SOS · ${metric.assistanceAlerts} asistencias</p></div><strong${metric.adherencePercentage === null ? ' title="Día reconstruido: no hay registro del plan de medicamentos de esa fecha."' : ""}>${formatAdherence(metric)}</strong></article>`).join("");
}

// Un día reconstruido (dataQuality "partial") no tiene plan programado: se muestran solo
// las tomas confirmadas y la adherencia queda sin dato, nunca como 0 %.
function formatAdherence(metric) {
  return metric.adherencePercentage === null ? "—" : `${metric.adherencePercentage}%`;
}

function formatDoses(metric, compact = false) {
  if (metric.scheduledDoses === null) return `${metric.takenDoses} tomas confirmadas · plan no disponible`;
  return compact
    ? `${metric.takenDoses}/${metric.scheduledDoses} tomas`
    : `${metric.takenDoses} de ${metric.scheduledDoses} tomas`;
}
