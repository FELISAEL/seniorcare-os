function updateClock() {
  const now = new Date();
  const hour = now.getHours();
  elements.greeting.textContent = hour < 12 ? "Buenos días" : hour < 18 ? "Buenas tardes" : "Buenas noches";
  elements.currentDate.textContent = new Intl.DateTimeFormat("es-GT", { weekday: "long", day: "numeric", month: "long" }).format(now);
  elements.currentTime.textContent = new Intl.DateTimeFormat("es-GT", { hour: "2-digit", minute: "2-digit" }).format(now);
}

function updateConnectionStatus() {
  const online = navigator.onLine;
  elements.connectionStatus.textContent = online ? "Con conexión" : "Modo sin conexión";
  elements.connectionStatus.classList.toggle("is-offline", !online);
}

function updateVoiceButton() {
  elements.toggleVoice.lastChild.textContent = state.voiceEnabled ? " Voz activada" : " Voz desactivada";
}

function speak(message) {
  if (!state.voiceEnabled || !message || !("speechSynthesis" in window)) return;
  window.speechSynthesis.cancel();
  const utterance = new SpeechSynthesisUtterance(message);
  utterance.lang = "es-GT";
  utterance.rate = 0.9;
  window.speechSynthesis.speak(utterance);
}
