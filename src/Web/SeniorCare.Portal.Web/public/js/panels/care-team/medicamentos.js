async function loadSelectedResidentSchedules() {
  if (!state.selectedResident) {
    elements.scheduleList.innerHTML = "<p>Seleccioná una persona adulta mayor.</p>";
    return;
  }
  elements.selectedResidentTitle.textContent = `Medicamentos de ${state.selectedResident.fullName}`;
  const schedules = await SeniorCareAuth.api(`/api/care/residents/${state.selectedResident.id}/medications/today`);
  elements.scheduleList.innerHTML = schedules.length === 0
    ? "<p>No tiene medicamentos programados.</p>"
    : schedules.map((item) => `
      <article class="schedule-item">
        <time class="schedule-time">${escapeHtml(formatTime(item.time))}</time>
        <div><h3>${escapeHtml(item.medicationName)} · ${escapeHtml(item.dosage)}</h3><p>${escapeHtml(item.instructions)}</p></div>
        <span class="schedule-status ${item.status === "taken" ? "is-taken" : ""}">${item.status === "taken" ? "Tomado" : "Pendiente"}</span>
      </article>`).join("");
}

async function addMedication(event) {
  event.preventDefault();
  if (!state.selectedResident) {
    elements.medicationMessage.textContent = "Seleccioná una persona adulta mayor.";
    return;
  }
  const form = new FormData(elements.medicationForm);
  elements.medicationMessage.textContent = "Guardando…";
  try {
    await SeniorCareAuth.api(`/api/care/residents/${state.selectedResident.id}/medications`, {
      method: "POST",
      body: {
        name: form.get("name"),
        dosage: form.get("dosage"),
        instructions: form.get("instructions"),
        time: `${form.get("time")}:00`
      }
    });
    elements.medicationForm.reset();
    elements.medicationMessage.textContent = "Medicamento guardado.";
    showToast("El plan de medicamentos se actualizó.");
    await loadSelectedResidentSchedules();
  } catch (error) {
    elements.medicationMessage.textContent = error.message;
  }
}
