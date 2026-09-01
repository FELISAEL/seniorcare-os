async function showApplication() {
  const residents = await api("/api/care/me/residents");
  state.resident = residents[0] || null;
  if (!state.resident) {
    throw new Error("Tu cuenta no tiene una persona adulta mayor vinculada.");
  }

  elements.residentName.textContent = state.resident.fullName;
  await loadMedications();
  speak(`Bienvenida, ${state.resident.fullName}.`);
}
