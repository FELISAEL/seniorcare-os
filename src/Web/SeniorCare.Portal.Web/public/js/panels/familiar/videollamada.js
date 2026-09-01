async function loadVideoRooms() {
  const rooms = await SeniorCareAuth.api(`/api/communication/residents/${state.resident.id}/video-rooms/active`);
  $("#videoList").innerHTML = rooms.length === 0
    ? "<p>No hay una videollamada activa.</p>"
    : rooms.map((room) => `<article class="video-item"><div><strong>${escapeHtml(state.resident.fullName)}</strong><p>Disponible desde ${escapeHtml(formatDateTime(room.createdAt))}</p></div><a href="${escapeAttribute(room.roomUrl)}" target="_blank" rel="noopener">Unirse</a></article>`).join("");
}

async function startVideoCall() {
  if (!state.resident) return;
  try {
    const room = await SeniorCareAuth.api("/api/communication/video-rooms", {
      method: "POST",
      body: { residentId: state.resident.id, residentName: state.resident.fullName }
    });
    window.open(room.roomUrl, "_blank", "noopener,noreferrer");
    showToast("Videollamada preparada.");
    await loadVideoRooms();
  } catch (error) {
    showToast(error.message, true);
  }
}
