function showToast(message, isError = false) {
  const toast = $("#toast");
  toast.textContent = message;
  toast.style.background = isError ? "#a21f26" : "#102a43";
  toast.classList.add("is-visible");
  window.clearTimeout(showToast.timeout);
  showToast.timeout = window.setTimeout(() => toast.classList.remove("is-visible"), 4_000);
}
function formatTime(value) { const [h, m] = String(value).split(":"); const d = new Date(); d.setHours(Number(h), Number(m), 0, 0); return new Intl.DateTimeFormat("es-GT", { hour: "numeric", minute: "2-digit" }).format(d); }
function formatDateTime(value) { return new Intl.DateTimeFormat("es-GT", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" }).format(new Date(value)); }
function escapeHtml(value) { return String(value).replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#039;"); }
function escapeAttribute(value) { return escapeHtml(value).replaceAll("`", "&#096;"); }
