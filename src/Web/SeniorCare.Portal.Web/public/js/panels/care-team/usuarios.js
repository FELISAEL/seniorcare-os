async function loadUsers() {
  if (!elements.userList) return;
  state.users = await SeniorCareAuth.api("/api/identity/users");
  elements.userList.innerHTML = state.users.map((user) => `<article class="user-item"><div><strong>${escapeHtml(user.displayName)}</strong><p>@${escapeHtml(user.username)} · ${escapeHtml(roleLabel(user.role))}</p></div><span>${user.isActive ? "Activo" : "Inactivo"}</span></article>`).join("");
}

async function createUser(event) {
  event.preventDefault();
  const form = new FormData(elements.userForm);
  const role = form.get("role");
  const residentId = ["resident", "family"].includes(role) ? form.get("residentId") : null;
  elements.userMessage.textContent = "Creando usuario…";
  try {
    await SeniorCareAuth.api("/api/identity/users", {
      method: "POST",
      body: {
        username: form.get("username"),
        displayName: form.get("displayName"),
        password: form.get("password"),
        role,
        residentId: residentId || null
      }
    });
    elements.userForm.reset();
    updateResidentLinkVisibility();
    elements.userMessage.textContent = "Usuario creado correctamente.";
    await loadUsers();
  } catch (error) {
    elements.userMessage.textContent = error.message;
  }
}

function populateResidentLinks() {
  if (!elements.residentLink) return;
  elements.residentLink.innerHTML = '<option value="">Seleccionar residente</option>' + state.residents.map((resident) => `<option value="${resident.id}">${escapeHtml(resident.fullName)}</option>`).join("");
}

function updateResidentLinkVisibility() {
  const field = $("#residentLinkField");
  if (!field) return;
  const needsLink = ["resident", "family"].includes($("#userRole").value);
  field.classList.toggle("is-hidden", !needsLink);
  elements.residentLink.required = needsLink;
}
