function bindEvents() {
  document.addEventListener("keydown", (event) => {
    if (event.key === "F9") {
      event.preventDefault();
      if (!elements.emergencyDialog.open) {
        elements.emergencyDialog.showModal();
        speak("Confirmá si necesitás ayuda urgente.");
      }
    }
  });

  document.querySelectorAll("[data-action]").forEach((button) => {
    button.addEventListener("click", () => {
      const action = button.dataset.action;
      if (action === "medications") openMedications();
      if (action === "assistance") sendAlert("assistance");
      if (action === "emergency") {
        elements.emergencyDialog.showModal();
        speak("Confirmá si necesitás ayuda urgente.");
      }
      if (action === "video") startVideoCall();
    });
  });

  document.querySelector("#confirmEmergency").addEventListener("click", (event) => {
    event.preventDefault();
    elements.emergencyDialog.close();
    sendAlert("emergency");
  });
  document.querySelector("#closeMedications").addEventListener("click", () => {
    elements.medicationPanel.classList.add("is-hidden");
  });
  document.querySelector("#increaseText").addEventListener("click", () => {
    const enabled = document.body.classList.toggle("large-text");
    localStorage.setItem("seniorcare.largeText", enabled ? "on" : "off");
    speak(enabled ? "Texto ampliado." : "Tamaño de texto normal.");
  });
  document.querySelector("#toggleContrast").addEventListener("click", () => {
    const enabled = document.body.classList.toggle("high-contrast");
    localStorage.setItem("seniorcare.highContrast", enabled ? "on" : "off");
    speak(enabled ? "Alto contraste activado." : "Alto contraste desactivado.");
  });
  elements.toggleVoice.addEventListener("click", () => {
    state.voiceEnabled = !state.voiceEnabled;
    localStorage.setItem("seniorcare.voice", state.voiceEnabled ? "on" : "off");
    updateVoiceButton();
    if (state.voiceEnabled) speak("Voz activada.");
  });
  document.querySelector("#logoutButton").addEventListener("click", SeniorCareAuth.logout);
  window.addEventListener("online", () => {
    updateConnectionStatus();
    flushOfflineQueue();
    loadMedications();
  });
  window.addEventListener("offline", updateConnectionStatus);
}
