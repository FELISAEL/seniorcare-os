document.addEventListener("DOMContentLoaded", () => SeniorCareErrors.run(initialize, { context: "panel-equipo-cuidado" }));

async function initialize() {
  state.session = await SeniorCareAuth.requireRole([expectedRole]);
  if (!state.session) return;

  elements.operatorName.textContent = state.session.user.displayName;
  elements.medicationForm.addEventListener("submit", addMedication);
  $("#logoutButton").addEventListener("click", SeniorCareAuth.logout);
  $("#refreshButton").addEventListener("click", refreshAll);

  if (isAdmin && elements.userForm) {
    elements.userForm.addEventListener("submit", createUser);
    $("#userRole").addEventListener("change", updateResidentLinkVisibility);
  }

  await refreshAll();
  window.clearInterval(initialize.poller);
  initialize.poller = window.setInterval(refreshOperationalData, 8000);
}

async function refreshAll() {
  const work = [loadResidents(), loadAlerts(), loadMetrics(), loadVideoRooms()];
  if (isAdmin) work.push(loadUsers());
  await SeniorCareErrors.allSettled(work, "panel-equipo-cuidado");
}

async function refreshOperationalData() {
  await SeniorCareErrors.allSettled([loadAlerts(), loadMetrics(), loadVideoRooms()], "actualizacion-operativa");
}
