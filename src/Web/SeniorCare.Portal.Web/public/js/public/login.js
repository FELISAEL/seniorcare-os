async function login(event) {
  event.preventDefault();
  loginMessage.textContent = "Verificando…";
  const form = new FormData(loginForm);

  try {
    const response = await fetch("/api/identity/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        username: form.get("username"),
        password: form.get("password")
      })
    });
    const payload = await response.json();
    if (!response.ok) {
      throw new Error(payload.message || "No se pudo iniciar sesión.");
    }

    SeniorCareAuth.saveLogin(payload);
    loginMessage.textContent = "Acceso correcto. Abriendo tu panel…";
    window.location.assign(payload.panelPath);
  } catch (error) {
    loginMessage.textContent = error.message;
  }
}
