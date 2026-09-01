(function () {
  "use strict";

  const keys = {
    token: "seniorcare.auth.token",
    user: "seniorcare.auth.user",
    panel: "seniorcare.auth.panel"
  };

  function readJson(value) {
    if (!value) return null;
    try {
      return JSON.parse(value);
    } catch {
      return null;
    }
  }

  function getSession() {
    const token = sessionStorage.getItem(keys.token);
    const user = readJson(sessionStorage.getItem(keys.user));
    const panelPath = sessionStorage.getItem(keys.panel);
    return token && user ? { token, user, panelPath } : null;
  }

  function saveLogin(payload) {
    sessionStorage.setItem(keys.token, payload.accessToken);
    sessionStorage.setItem(keys.user, JSON.stringify(payload.user));
    sessionStorage.setItem(keys.panel, payload.panelPath);
    return getSession();
  }

  function clear() {
    Object.values(keys).forEach((key) => sessionStorage.removeItem(key));
    sessionStorage.removeItem("seniorcare.resident");
  }

  function redirectToLogin(reason) {
    clear();
    const suffix = reason ? `?sesion=${encodeURIComponent(reason)}#login` : "#login";
    window.location.replace(`/${suffix}`);
  }

  async function validate() {
    const session = getSession();
    if (!session) return null;

    try {
      const response = await fetch("/api/identity/me", {
        headers: { Authorization: `Bearer ${session.token}` }
      });
      if (!response.ok) {
        clear();
        return null;
      }

      const profile = await response.json();
      sessionStorage.setItem(keys.user, JSON.stringify(profile));
      sessionStorage.setItem(keys.panel, profile.panelPath);
      return {
        token: session.token,
        user: profile,
        panelPath: profile.panelPath
      };
    } catch {
      return session;
    }
  }

  async function requireRole(allowedRoles) {
    const session = await validate();
    if (!session) {
      redirectToLogin("expirada");
      return null;
    }

    if (!allowedRoles.includes(session.user.role)) {
      window.location.replace(session.panelPath || "/");
      return null;
    }

    return session;
  }

  async function api(path, options = {}) {
    const session = getSession();
    if (!session) {
      redirectToLogin("expirada");
      throw new Error("Tu sesión terminó.");
    }

    const response = await fetch(path, {
      method: options.method || "GET",
      headers: {
        Authorization: `Bearer ${session.token}`,
        ...(options.body ? { "Content-Type": "application/json" } : {})
      },
      ...(options.body ? { body: JSON.stringify(options.body) } : {})
    });

    const contentType = response.headers.get("content-type") || "";
    const payload = response.status === 204
      ? null
      : contentType.includes("application/json")
        ? await response.json()
        : null;

    if (response.status === 401) {
      redirectToLogin("expirada");
      throw new Error("Tu sesión terminó.");
    }

    if (response.status === 403) {
      throw new Error("Tu rol no tiene permiso para realizar esta operación.");
    }

    if (!response.ok) {
      throw new Error(payload?.message || "No se pudo completar la operación.");
    }

    return payload;
  }

  async function logout() {
    const session = getSession();
    if (session) {
      try {
        await fetch("/api/identity/logout", {
          method: "POST",
          headers: { Authorization: `Bearer ${session.token}` }
        });
      } catch {
        // El token es stateless: limpiar el navegador siempre cierra la sesión local.
      }
    }
    clear();
    window.location.replace("/#login");
  }

  window.SeniorCareAuth = {
    getSession,
    saveLogin,
    clear,
    validate,
    requireRole,
    api,
    logout,
    redirectToLogin
  };
})();
