async function loadResident() {
  const residents = await SeniorCareAuth.api("/api/care/me/residents");
  state.resident = residents[0] || null;
  if (!state.resident) {
    $("#residentSummary").textContent = "Tu cuenta todavía no tiene una persona adulta mayor vinculada.";
    $("#videoButton").disabled = true;
    return;
  }

  $("#residentName").textContent = state.resident.fullName;
  $("#residentSummary").textContent = `Seguimiento de ${state.resident.fullName}`;
  $("#residentContact").textContent = `Contacto de emergencia: ${state.resident.emergencyContactName} · ${state.resident.emergencyContactPhone}`;
}
