document.addEventListener("DOMContentLoaded", () => SeniorCareErrors.run(async () => {
  loginForm.addEventListener("submit", login);
  continueButton.addEventListener("click", async () => {
    const session = await SeniorCareAuth.validate();
    if (session?.panelPath) window.location.assign(session.panelPath);
  });

  const session = await SeniorCareAuth.validate();
  if (session) {
    continueButton.textContent = `Continuar como ${session.user.displayName}`;
    continueButton.classList.remove("is-hidden");
  }

  const params = new URLSearchParams(window.location.search);
  if (params.get("sesion") === "expirada") {
    loginMessage.textContent = "Tu sesión terminó. Iniciá sesión nuevamente.";
  }
}, { context: "portal-publico", target: "#loginMessage" }));
