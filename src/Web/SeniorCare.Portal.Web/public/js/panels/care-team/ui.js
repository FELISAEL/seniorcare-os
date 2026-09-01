function showToast(message, isError = false) {
  elements.toast.textContent = message;
  elements.toast.style.background = isError ? "#a21f26" : "#102a43";
  elements.toast.classList.add("is-visible");
  window.clearTimeout(showToast.timeout);
  showToast.timeout = window.setTimeout(() => elements.toast.classList.remove("is-visible"), 4_000);
}

function roleLabel(role) {
  return ({ admin: "Administrador", caregiver: "Cuidador", resident: "Adulto mayor", family: "Familiar" })[role] || role;
}
function formatTime(value) { const [hours, minutes] = String(value).split(":"); const date = new Date(); date.setHours(Number(hours), Number(minutes), 0, 0); return new Intl.DateTimeFormat("es-GT", { hour: "numeric", minute: "2-digit" }).format(date); }
function formatDate(value) { return new Intl.DateTimeFormat("es-GT", { day: "numeric", month: "short" }).format(new Date(`${value}T12:00:00`)); }
function formatDateTime(value) { return new Intl.DateTimeFormat("es-GT", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" }).format(new Date(value)); }
function escapeHtml(value) { return String(value).replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#039;"); }
function escapeAttribute(value) { return escapeHtml(value).replaceAll("`", "&#096;"); }
