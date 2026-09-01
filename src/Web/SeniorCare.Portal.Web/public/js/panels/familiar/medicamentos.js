async function loadMedications() {
  const items = await SeniorCareAuth.api(`/api/care/residents/${state.resident.id}/medications/today`);
  $("#scheduleList").innerHTML = items.length === 0
    ? "<p>No hay medicamentos programados para hoy.</p>"
    : items.map((item) => `<article class="schedule-item"><time class="schedule-time">${escapeHtml(formatTime(item.time))}</time><div><h3>${escapeHtml(item.medicationName)} · ${escapeHtml(item.dosage)}</h3><p>${escapeHtml(item.instructions)}</p></div><span class="schedule-status ${item.status === "taken" ? "is-taken" : ""}">${item.status === "taken" ? "Tomado" : "Pendiente"}</span></article>`).join("");
}
