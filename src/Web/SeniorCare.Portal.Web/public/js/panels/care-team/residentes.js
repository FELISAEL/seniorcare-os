async function loadResidents() {
  state.residents = await SeniorCareAuth.api("/api/care/residents");
  elements.residentCount.textContent = state.residents.length;

  if (!state.selectedResident && state.residents.length > 0) {
    state.selectedResident = state.residents[0];
  } else if (state.selectedResident) {
    state.selectedResident = state.residents.find((item) => item.id === state.selectedResident.id) || state.residents[0] || null;
  }

  renderResidents();
  populateResidentLinks();
  await loadSelectedResidentSchedules();
}

function renderResidents() {
  if (state.residents.length === 0) {
    elements.residentList.innerHTML = "<p>No hay personas adultas mayores registradas.</p>";
    return;
  }

  elements.residentList.innerHTML = state.residents.map((resident) => `
    <article class="resident-item ${resident.id === state.selectedResident?.id ? "is-selected" : ""}">
      <div><h3>${escapeHtml(resident.fullName)}</h3><p>Contacto: ${escapeHtml(resident.emergencyContactName)} · ${escapeHtml(resident.emergencyContactPhone)}</p></div>
      <button type="button" data-resident-id="${resident.id}">Ver plan</button>
    </article>`).join("");

  document.querySelectorAll("[data-resident-id]").forEach((button) => {
    button.addEventListener("click", async () => {
      state.selectedResident = state.residents.find((resident) => resident.id === button.dataset.residentId);
      renderResidents();
      await loadSelectedResidentSchedules();
    });
  });
}
