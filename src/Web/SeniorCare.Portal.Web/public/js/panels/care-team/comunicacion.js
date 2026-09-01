async function loadVideoRooms() {
  state.videoRooms = await SeniorCareAuth.api("/api/communication/video-rooms/active");
  elements.videoList.innerHTML = state.videoRooms.length === 0
    ? "<p>No hay videollamadas activas.</p>"
    : state.videoRooms.map((room) => `<article class="video-item"><div><strong>${escapeHtml(room.residentName)}</strong><p>Solicitada ${escapeHtml(formatDateTime(room.createdAt))}</p></div><a href="${escapeAttribute(room.roomUrl)}" target="_blank" rel="noopener">Unirse</a></article>`).join("");
}
