async function loadAlerts() {
  state.alerts = await SeniorCareAuth.api("/api/emergencies/alerts/active");
  elements.alertCount.textContent = state.alerts.length;
  elements.noAlerts.classList.toggle("is-hidden", state.alerts.length !== 0);
  elements.alertList.innerHTML = state.alerts.map((alert) => {
    const typeLabel = alert.type === "emergency" ? "SOS" : "Asistencia";
    return `<article class="alert-item ${alert.type === "assistance" ? "alert-item--assistance" : ""}">
      <span class="alert-badge">${typeLabel}</span>
      <div class="alert-copy"><h3>${escapeHtml(alert.residentName)}</h3><p>${escapeHtml(alert.message)} · ${escapeHtml(formatDateTime(alert.createdAt))}</p></div>
      <div class="alert-actions">${alert.status === "active" ? `<button data-alert-action="acknowledged" data-alert-id="${alert.id}">Atender</button>` : ""}<button data-alert-action="resolved" data-alert-id="${alert.id}">Resolver</button></div>
    </article>`;
  }).join("");
  document.querySelectorAll("[data-alert-action]").forEach((button) => {
    button.addEventListener("click", () => updateAlert(button.dataset.alertId, button.dataset.alertAction));
  });
}

async function updateAlert(alertId, status) {
  try {
    await SeniorCareAuth.api(`/api/emergencies/alerts/${alertId}`, { method: "PATCH", body: { status } });
    showToast(status === "resolved" ? "Alerta resuelta." : "Alerta en atención.");
    await loadAlerts();
  } catch (error) {
    showToast(error.message, true);
  }
}
