// Menú lateral deslizante del teléfono (solo panel de Administración): el botón de la barra
// superior lo abre y lo cierra. También se cierra al tocar fuera, con Escape o al elegir
// una sección. Mientras está abierto, el contenido no recibe foco ni se desplaza.

(function () {
  function initAdminMenu() {
    const toggle = document.getElementById("menuToggle");
    const sidebar = document.getElementById("adminSidebar");
    const overlay = document.getElementById("menuOverlay");
    const content = document.querySelector(".content");
    if (!toggle || !sidebar || !overlay || !content) return;

    const phoneLayout = window.matchMedia("(max-width: 720px)");
    const isOpen = () => sidebar.classList.contains("is-open");

    function setMenuOpen(open, { returnFocus = false } = {}) {
      sidebar.classList.toggle("is-open", open);
      overlay.hidden = !open;
      content.inert = open;
      document.body.classList.toggle("menu-open", open);
      toggle.setAttribute("aria-expanded", String(open));
      toggle.setAttribute("aria-label", open ? "Cerrar menú" : "Abrir menú");
      if (open) (sidebar.querySelector(".nav-link.is-active") || sidebar.querySelector(".nav-link")).focus();
      else if (returnFocus) toggle.focus();
    }

    toggle.addEventListener("click", () => setMenuOpen(!isOpen()));
    overlay.addEventListener("click", () => setMenuOpen(false, { returnFocus: true }));
    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape" && isOpen()) setMenuOpen(false, { returnFocus: true });
    });
    // Al elegir una sección desde el menú, la nueva vista se muestra desde el principio.
    sidebar.querySelectorAll(".nav-link").forEach((link) => {
      link.addEventListener("click", () => {
        if (!isOpen()) return;
        setMenuOpen(false, { returnFocus: true });
        window.scrollTo(0, 0);
      });
    });
    // Al pasar a tablet o escritorio el menú vuelve a ser fijo: se cierra el estado del teléfono.
    phoneLayout.addEventListener("change", () => {
      if (!phoneLayout.matches && isOpen()) setMenuOpen(false);
    });
  }

  document.addEventListener("DOMContentLoaded", initAdminMenu);
})();
