let loginInFlight = false;

async function login(event) {
  event.preventDefault();
  if (loginInFlight) return;
  loginInFlight = true;
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

    const payload = await readResponsePayload(response);

    if (!response.ok) {
      throw new Error(loginErrorMessage(response.status, payload));
    }

    if (!isSuccessfulLogin(payload)) {
      throw new Error("No se pudo iniciar sesión. Intentá nuevamente.");
    }

    SeniorCareAuth.saveLogin(payload);
    loginMessage.textContent = "Acceso correcto. Abriendo tu panel…";
    window.location.assign(payload.panelPath);
  } catch (error) {
    loginMessage.textContent = SeniorCareErrors.messageFrom(
      error,
      "No se pudo iniciar sesión. Intentá nuevamente."
    );
  } finally {
    loginInFlight = false;
  }
}

async function readResponsePayload(response) {
  let text;
  try {
    text = await response.text();
  } catch {
    return null;
  }

  if (!text) return null;

  const contentType = response.headers.get("content-type") || "";
  if (!contentType.includes("application/json")) return null;

  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

function loginErrorMessage(status, payload) {
  if (status === 429) {
    return "Demasiados intentos. Esperá un minuto e intentá nuevamente.";
  }

  if (status === 401) {
    const apiMessage =
      payload && typeof payload.message === "string" ? payload.message.trim() : "";
    return apiMessage || "Usuario o contraseña incorrectos.";
  }

  return "No se pudo iniciar sesión. Intentá nuevamente en unos minutos.";
}

function isSuccessfulLogin(payload) {
  return (
    !!payload &&
    typeof payload.accessToken === "string" &&
    payload.accessToken.length > 0 &&
    typeof payload.panelPath === "string" &&
    payload.panelPath.length > 0 &&
    !!payload.user
  );
}
