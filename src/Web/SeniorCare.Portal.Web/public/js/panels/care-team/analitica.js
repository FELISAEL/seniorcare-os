async function loadMetrics() {
  state.metrics = await SeniorCareAuth.api("/api/analytics/recent?days=7");
  const today = state.metrics[0];
  if (today) {
    elements.adherence.textContent = `${today.adherencePercentage}%`;
    elements.doseSummary.textContent = `${today.takenDoses} de ${today.scheduledDoses} tomas`;
    elements.etlTime.textContent = new Intl.DateTimeFormat("es-GT", { hour: "2-digit", minute: "2-digit" }).format(new Date(today.etlRunAt));
  }
  elements.metricsList.innerHTML = state.metrics.length === 0
    ? "<p>El ETL está preparando la primera actualización.</p>"
    : state.metrics.map((metric) => `<article class="metric-row"><strong>${escapeHtml(formatDate(metric.metricDate))}</strong><div><div class="metric-bar"><span style="width: ${Math.min(metric.adherencePercentage, 100)}%"></span></div><p>${metric.takenDoses}/${metric.scheduledDoses} tomas · ${metric.emergencyAlerts} SOS · ${metric.assistanceAlerts} asistencias</p></div><strong>${metric.adherencePercentage}%</strong></article>`).join("");
}
