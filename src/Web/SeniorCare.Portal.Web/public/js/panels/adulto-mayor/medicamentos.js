async function loadMedications() {
  if (!state.resident) return;
  try {
    state.medications = await api(`/api/care/residents/${state.resident.id}/medications/today`);
    localStorage.setItem(`seniorcare.medications.${state.resident.id}`, JSON.stringify(state.medications));
  } catch {
    state.medications = readJson(localStorage.getItem(`seniorcare.medications.${state.resident.id}`)) || [];
  }
  renderMedicationSummary();
  renderMedications();
}

function renderMedicationSummary() {
  if (state.medications.length === 0) {
    elements.todaySummary.textContent = "No tenés medicamentos programados.";
    return;
  }
  const pending = state.medications.filter((item) => item.status !== "taken");
  if (pending.length === 0) {
    elements.todaySummary.textContent = "Completaste todos tus medicamentos de hoy.";
    return;
  }
  const next = pending[0];
  elements.todaySummary.textContent = `Tenés ${pending.length} pendiente${pending.length === 1 ? "" : "s"}. Próximo: ${formatTime(next.time)} – ${next.medicationName} ${next.dosage}`;
}

function renderMedications() {
  if (state.medications.length === 0) {
    elements.medicationList.innerHTML = '<p class="medication-details">No hay horarios para mostrar.</p>';
    return;
  }
  elements.medicationList.innerHTML = state.medications.map((item) => {
    const action = item.status === "taken"
      ? '<span class="taken-badge">Tomado ✓</span>'
      : `<button class="confirm-dose" data-schedule-id="${item.scheduleId}">Confirmar que lo tomé</button>`;
    return `<article class="medication-item">
      <time class="medication-time">${escapeHtml(formatTime(item.time))}</time>
      <div><h3 class="medication-name">${escapeHtml(item.medicationName)} – ${escapeHtml(item.dosage)}</h3><p class="medication-details">${escapeHtml(item.instructions)}</p></div>${action}
    </article>`;
  }).join("");
  document.querySelectorAll(".confirm-dose").forEach((button) => {
    button.addEventListener("click", () => confirmDose(button.dataset.scheduleId));
  });
}

function openMedications() {
  elements.medicationPanel.classList.remove("is-hidden");
  elements.medicationPanel.scrollIntoView({ behavior: "smooth", block: "start" });
  speak(elements.todaySummary.textContent);
}

async function confirmDose(scheduleId) {
  try {
    await api(`/api/care/residents/${state.resident.id}/medications/${scheduleId}/confirm`, { method: "POST", queueWhenOffline: true });
    showToast("Medicamento confirmado. Gracias.");
    speak("Medicamento confirmado.");
    await loadMedications();
  } catch (error) {
    showToast(error.message, true);
  }
}
