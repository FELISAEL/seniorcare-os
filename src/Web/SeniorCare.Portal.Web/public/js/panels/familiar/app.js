document.addEventListener("DOMContentLoaded", () => SeniorCareErrors.run(initialize, { context: "panel-familiar" }));

async function initialize() {
  initializeFamilyNavigation();
  state.session = await SeniorCareAuth.requireRole(["family"]);
  if (!state.session) return;

  $("#familyName").textContent = state.session.user.displayName;
  $("#logoutButton").addEventListener("click", SeniorCareAuth.logout);
  $("#refreshButton").addEventListener("click", refresh);
  $("#videoButton").addEventListener("click", startVideoCall);

  await loadResident();
  await refresh();
  window.setInterval(refresh, 15000);
}

async function refresh() {
  if (!state.resident) return;
  await SeniorCareErrors.allSettled([loadMedications(), loadAlerts(), loadVideoRooms()], "panel-familiar");
}
