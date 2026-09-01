async function sendAlert(type) {
  const emergency = type === "emergency";
  try {
    await api("/api/emergencies/alerts", {
      method: "POST",
      queueWhenOffline: true,
      body: {
        residentId: state.resident.id,
        residentName: state.resident.fullName,
        type,
        message: emergency ? "La persona adulta mayor activó el botón SOS." : "La persona adulta mayor solicitó asistencia."
      }
    });
    const message = navigator.onLine
      ? emergency ? "Emergencia enviada. Tu cuidador ha sido avisado." : "Solicitud enviada. Tu cuidador ha sido avisado."
      : "Solicitud guardada. Se enviará cuando regrese la conexión.";
    showToast(message);
    speak(message);
  } catch (error) {
    showToast(error.message, true);
  }
}
