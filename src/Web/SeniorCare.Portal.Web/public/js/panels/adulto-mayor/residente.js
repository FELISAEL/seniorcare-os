async function showApplication() {
  const cacheKey = `seniorcare.resident.${state.session.user.id}`;
  try {
    const residents = await api("/api/care/me/residents");
    state.resident = residents[0] || null;
    if (state.resident) {
      // Solo lo necesario para el SOS sin conexión (no se guarda el correo familiar).
      const { id, fullName, emergencyContactName, emergencyContactPhone } = state.resident;
      try {
        localStorage.setItem(cacheKey, JSON.stringify({ id, fullName, emergencyContactName, emergencyContactPhone }));
      } catch {
        // Sin almacenamiento local el panel funciona igual mientras haya servidor.
      }
    }
  } catch (error) {
    // Sin servidor se usa la última copia: el botón SOS y la cola deben seguir funcionando.
    state.resident = readJson(localStorage.getItem(cacheKey));
    if (!state.resident) throw error;
  }
  if (!state.resident) {
    throw new Error("Tu cuenta no tiene una persona adulta mayor vinculada.");
  }

  elements.residentName.textContent = state.resident.fullName;
  renderSosStatus();
  await loadMedications();
  speak(`Bienvenida, ${state.resident.fullName}.`);
}
