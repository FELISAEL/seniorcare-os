async function loadUsers() {
  if (!elements.userList) return;
  state.users = await SeniorCareAuth.api("/api/identity/users");
  elements.userList.innerHTML = state.users.map(userItemMarkup).join("");

  if (isAdmin && !elements.userList.dataset.bound) {
    elements.userList.addEventListener("click", handleUserListClick);
    elements.userList.dataset.bound = "true";
  }
}

function userItemMarkup(user) {
  const actions = isAdmin
    ? `<div class="user-actions">
        <button type="button" class="icon-button icon-button--edit" data-action="edit" data-user-id="${user.id}" title="Editar" aria-label="Editar usuario">✏️</button>
        <button type="button" class="icon-button icon-button--save is-hidden" data-action="save" data-user-id="${user.id}" title="Guardar" aria-label="Guardar cambios">💾</button>
        <button type="button" class="icon-button icon-button--delete" data-action="delete" data-user-id="${user.id}" title="Eliminar" aria-label="Eliminar usuario">🗑️</button>
      </div>`
    : "";

  return `<article class="user-item" data-user-id="${user.id}" data-username="${escapeAttribute(user.username)}">
    <div class="user-item-body">
      <div class="user-item-view" data-role="view">
        <strong>${escapeHtml(user.displayName)}</strong>
        <p>@${escapeHtml(user.username)} · ${escapeHtml(roleLabel(user.role))}</p>
      </div>
      <div class="user-item-edit is-hidden" data-role="edit">
        <input type="text" class="user-edit-name" value="${escapeAttribute(user.displayName)}" minlength="3" maxlength="160" aria-label="Nombre completo">
        <select class="user-edit-role" aria-label="Rol">
          <option value="caregiver" ${user.role === "caregiver" ? "selected" : ""}>Cuidador</option>
          <option value="resident" ${user.role === "resident" ? "selected" : ""}>Adulto mayor</option>
          <option value="family" ${user.role === "family" ? "selected" : ""}>Familiar</option>
          <option value="admin" ${user.role === "admin" ? "selected" : ""}>Administrador</option>
        </select>
      </div>
    </div>
    <span class="user-item-status">${user.isActive ? "Activo" : "Inactivo"}</span>
    ${actions}
  </article>`;
}

function handleUserListClick(event) {
  const button = event.target.closest("[data-action]");
  if (!button) return;

  const card = button.closest(".user-item");
  if (!card) return;

  const userId = button.dataset.userId;
  const action = button.dataset.action;

  if (action === "edit") {
    setCardEditMode(card, true);
  } else if (action === "save") {
    saveUserEdits(card, userId);
  } else if (action === "delete") {
    askDeleteConfirmation(card, userId);
  }
}

function setCardEditMode(card, isEditing) {
  card.querySelector('[data-role="view"]').classList.toggle("is-hidden", isEditing);
  card.querySelector('[data-role="edit"]').classList.toggle("is-hidden", !isEditing);
  card.querySelector('[data-action="edit"]').classList.toggle("is-hidden", isEditing);
  card.querySelector('[data-action="save"]').classList.toggle("is-hidden", !isEditing);
}

async function saveUserEdits(card, userId) {
  const displayName = card.querySelector(".user-edit-name").value.trim();
  const role = card.querySelector(".user-edit-role").value;

  try {
    await SeniorCareAuth.api(`/api/identity/users/${userId}`, {
      method: "PUT",
      body: { displayName, role }
    });
    showToast("Usuario actualizado correctamente.");
    await loadUsers();
  } catch (error) {
    showToast(error.message, true);
  }
}

// Abre el <dialog> propio del sistema (no el confirm() del navegador) y
// espera a que la persona elija "Cancelar" o "Sí, eliminar".
function askDeleteConfirmation(card, userId) {
  const dialog = document.getElementById("deleteUserDialog");
  if (!dialog) return;

  const username = card.dataset.username;
  const messageEl = document.getElementById("deleteUserMessage");
  if (messageEl) {
    messageEl.textContent = `Se eliminará a @${username}. Esta acción no se puede deshacer.`;
  }

  const onClose = async () => {
    dialog.removeEventListener("close", onClose);
    if (dialog.returnValue === "default") {
      await deleteUser(card, userId);
    }
  };

  dialog.addEventListener("close", onClose);
  dialog.showModal();
}

async function deleteUser(card, userId) {
  try {
    await SeniorCareAuth.api(`/api/identity/users/${userId}`, { method: "DELETE" });
    showToast("Usuario eliminado correctamente.");
    await loadUsers();
  } catch (error) {
    showToast(error.message, true);
  }
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