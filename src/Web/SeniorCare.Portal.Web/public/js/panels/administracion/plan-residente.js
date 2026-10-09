// Abre un <dialog> propio con el plan de cuidado completo de UN residente
// específico, al hacer clic en "Ver plan" dentro de la lista de Residentes.
// Archivo exclusivo del panel de Administración: no toca residentes.js ni
// medicamentos.js (compartidos con el panel de Cuidador), solo escucha
// los mismos botones desde afuera.

(function () {
  function scheduleItemsMarkup(schedules) {
    return schedules.length === 0
      ? "<p>No tiene medicamentos programados.</p>"
      : schedules
          .map(
            (item) => `
        <article class="schedule-item">
          <time class="schedule-time">${escapeHtml(formatTime(item.time))}</time>
          <div><h3>${escapeHtml(item.medicationName)} · ${escapeHtml(item.dosage)}</h3><p>${escapeHtml(item.instructions)}</p></div>
          <span class="schedule-status ${item.status === "taken" ? "is-taken" : ""}">${item.status === "taken" ? "Tomado" : "Pendiente"}</span>
        </article>`
          )
          .join("");
  }

  async function openResidentPlanDialog(residentId) {
    const dialog = document.getElementById("residentPlanDialog");
    if (!dialog) return;

    const resident = (state.residents || []).find(
      (item) => item.id === residentId
    );
    if (!resident) return;

    document.getElementById("planDialogName").textContent = resident.fullName;
    document.getElementById("planDialogContact").textContent =
      `Contacto: ${resident.emergencyContactName} · ${resident.emergencyContactPhone}`;

    const scheduleContainer = document.getElementById("planDialogSchedule");
    scheduleContainer.innerHTML = "<p>Cargando…</p>";
    dialog.showModal();

    try {
      const schedules = await SeniorCareAuth.api(
        `/api/care/residents/${resident.id}/medications/today`
      );
      scheduleContainer.innerHTML = scheduleItemsMarkup(schedules);
    } catch (error) {
      scheduleContainer.innerHTML = `<p>${escapeHtml(error.message)}</p>`;
    }
  }

  document.addEventListener("DOMContentLoaded", () => {
    const residentList = document.getElementById("residentList");
    if (!residentList) return;

    // Delegación de eventos: residentList nunca se destruye, aunque su
    // contenido interno se vuelva a dibujar cada vez que cambian los
    // residentes o se selecciona uno distinto.
    residentList.addEventListener("click", (event) => {
      const button = event.target.closest("[data-resident-id]");
      if (!button) return;
      openResidentPlanDialog(button.dataset.residentId);
    });
  });
})();