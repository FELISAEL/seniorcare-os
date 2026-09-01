async function startVideoCall() {
  try {
    const room = await api("/api/communication/video-rooms", {
      method: "POST",
      body: { residentId: state.resident.id, residentName: state.resident.fullName }
    });
    speak("Abriendo videollamada con tu familia.");
    window.open(room.roomUrl, "_blank", "noopener,noreferrer");
  } catch (error) {
    showToast(navigator.onLine ? error.message : "La videollamada necesita conexión a internet.", true);
  }
}
