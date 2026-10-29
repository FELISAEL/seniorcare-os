// Panel de administración: registrar adultos mayores y administrar el correo
// del familiar que recibirá las notificaciones (medicamentos, SOS, etc.).
// Usa los endpoints existentes del API:
//   POST /api/care/residents                      -> crear residente
//   PUT  /api/care/residents/{id}/family-email    -> guardar/borrar correo

(function () {
  const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

  function setMessage(element, text, kind) {
    if (!element) return;
    element.textContent = text;
    element.classList.toggle("is-ok", kind === "ok");
    element.classList.toggle("is-error", kind === "error");
  }

  function isValidEmail(value) {
    return value === "" || (value.length <= 254 && EMAIL_PATTERN.test(value));
  }

  function renderFamilyEmails() {
    const container = document.getElementById("familyEmailList");
    if (!container) return;

    if (!state.residents.length) {
      container.innerHTML = "<p>No hay personas adultas mayores registradas.</p>";
      return;
    }

    container.innerHTML = state.residents.map((resident) => `
      <div class="family-email-row" data-family-row="${escapeAttribute(resident.id)}">
        <div>
          <strong>${escapeHtml(resident.fullName)}</strong>
          <small>${resident.familyEmail ? "Recibe notificaciones" : "Sin correo registrado"}</small>
        </div>
        <input type="email" maxlength="254" placeholder="familiar@correo.com"
               aria-label="Correo del familiar de ${escapeAttribute(resident.fullName)}"
               value="${escapeAttribute(resident.familyEmail || "")}">
        <button class="button button--secondary" type="button" data-save-family-email="${escapeAttribute(resident.id)}">Guardar</button>
        <p class="form-message family-email-status" role="status"></p>
      </div>`).join("");
  }

  async function saveFamilyEmail(button) {
    const row = button.closest("[data-family-row]");
    const input = row.querySelector("input");
    const status = row.querySelector(".family-email-status");
    const value = input.value.trim();

    if (!isValidEmail(value)) {
      setMessage(status, "Escribe un correo válido, por ejemplo familiar@correo.com.", "error");
      input.focus();
      return;
    }

    button.disabled = true;
    try {
      await SeniorCareAuth.api(`/api/care/residents/${button.dataset.saveFamilyEmail}/family-email`, {
        method: "PUT",
        body: { familyEmail: value || null }
      });
      await loadResidents();
      renderFamilyEmails();
      const updatedRow = document.querySelector(`[data-family-row="${button.dataset.saveFamilyEmail}"] .family-email-status`);
      setMessage(updatedRow, value ? "Correo guardado." : "Correo eliminado.", "ok");
    } catch (error) {
      setMessage(status, error.message, "error");
      button.disabled = false;
    }
  }

  async function submitResident(event) {
    event.preventDefault();
    const form = event.currentTarget;
    const message = document.getElementById("residentFormMessage");
    const data = new FormData(form);
    const familyEmail = String(data.get("familyEmail") || "").trim();

    if (!form.checkValidity() || !isValidEmail(familyEmail)) {
      setMessage(message, "Revisa los datos: el correo debe tener un formato válido.", "error");
      form.reportValidity();
      return;
    }

    const submit = form.querySelector('button[type="submit"]');
    submit.disabled = true;
    try {
      await SeniorCareAuth.api("/api/care/residents", {
        method: "POST",
        body: {
          username: String(data.get("username")).trim(),
          fullName: String(data.get("fullName")).trim(),
          birthDate: data.get("birthDate") || null,
          emergencyContactName: String(data.get("emergencyContactName")).trim(),
          emergencyContactPhone: String(data.get("emergencyContactPhone")).trim(),
          familyEmail: familyEmail || null
        }
      });
      form.reset();
      await loadResidents();
      renderFamilyEmails();
      setMessage(message,
        "Adulto mayor registrado. Para que pueda iniciar sesión, crea su cuenta en Usuarios → Administración (rol Adulto mayor).",
        "ok");
    } catch (error) {
      setMessage(message, error.message, "error");
    } finally {
      submit.disabled = false;
    }
  }

  document.addEventListener("DOMContentLoaded", () => {
    const form = document.getElementById("residentForm");
    if (!form) return;

    form.addEventListener("submit", submitResident);

    document.getElementById("familyEmailList").addEventListener("click", (event) => {
      const button = event.target.closest("[data-save-family-email]");
      if (button) saveFamilyEmail(button);
    });

    // La lista se dibuja al abrir la pestaña "Registrar", cuando ya hay datos.
    document.querySelector('[data-subtab="register"]')
      ?.addEventListener("click", renderFamilyEmails);
  });
})();
