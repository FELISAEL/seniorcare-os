// Menú lateral deslizante del teléfono: el botón de la barra superior lo abre y lo cierra.
// También se cierra al tocar fuera, con Escape o al elegir una sección.
document.addEventListener("DOMContentLoaded", initializeFamilyMenu);

function initializeFamilyMenu() {
  const toggle = $("#menuToggle");
  const sidebar = $("#familySidebar");
  const overlay = $("#menuOverlay");
  const content = $(".content");
  const phoneLayout = window.matchMedia("(max-width: 720px)");
  const isOpen = () => sidebar.classList.contains("is-open");

  function setMenuOpen(open, { returnFocus = false } = {}) {
    sidebar.classList.toggle("is-open", open);
    overlay.hidden = !open;
    content.inert = open;
    toggle.setAttribute("aria-expanded", String(open));
    toggle.setAttribute("aria-label", open ? "Cerrar menú" : "Abrir menú");
    if (open) sidebar.querySelector('[role="tab"][aria-selected="true"]').focus();
    else if (returnFocus) toggle.focus();
  }

  toggle.addEventListener("click", () => setMenuOpen(!isOpen()));
  overlay.addEventListener("click", () => setMenuOpen(false, { returnFocus: true }));
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && isOpen()) setMenuOpen(false, { returnFocus: true });
  });
  sidebar.querySelectorAll('[role="tab"]').forEach((tab) => {
    tab.addEventListener("click", () => {
      if (isOpen()) setMenuOpen(false, { returnFocus: true });
    });
  });
  // Al pasar a tablet o escritorio el menú vuelve a ser fijo: se cierra el estado del teléfono.
  phoneLayout.addEventListener("change", () => {
    if (!phoneLayout.matches && isOpen()) setMenuOpen(false);
  });
}
