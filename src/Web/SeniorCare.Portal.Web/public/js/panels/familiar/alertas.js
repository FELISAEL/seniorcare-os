async function loadAlerts() {
  const alerts = await SeniorCareAuth.api(`/api/emergencies/residents/${state.resident.id}/alerts`);
  $("#alertList").innerHTML = alerts.length === 0
    ? '<div class="empty-state">No hay alertas registradas.</div>'
    : alerts.map((alert) => `<article class="alert-item ${alert.type === "assistance" ? "alert-item--assistance" : ""}"><span class="alert-badge">${alert.type === "emergency" ? "SOS" : "Asistencia"}</span><div class="alert-copy"><h3>${escapeHtml(alert.status === "resolved" ? "Resuelta" : alert.status === "acknowledged" ? "En atención" : "Activa")}</h3><p>${escapeHtml(alert.message)} · ${escapeHtml(formatDateTime(alert.createdAt))}</p></div></article>`).join("");
}
