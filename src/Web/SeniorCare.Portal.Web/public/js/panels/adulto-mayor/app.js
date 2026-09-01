document.addEventListener("DOMContentLoaded", () => SeniorCareErrors.run(initialize, { context: "panel-adulto-mayor" }));

async function initialize() {
  state.session = await SeniorCareAuth.requireRole(["resident"]);
  if (!state.session) return;

  bindEvents();
  updateClock();
  updateConnectionStatus();
  window.setInterval(updateClock, 30000);

  if ("serviceWorker" in navigator) {
    navigator.serviceWorker.register("./service-worker.js").catch((error) =>
      SeniorCareErrors.report(error, "service-worker", { silent: true })
    );
  }

  if (localStorage.getItem("seniorcare.largeText") === "on") document.body.classList.add("large-text");
  if (localStorage.getItem("seniorcare.highContrast") === "on") document.body.classList.add("high-contrast");

  updateVoiceButton();
  await showApplication();
  await flushOfflineQueue();
}
