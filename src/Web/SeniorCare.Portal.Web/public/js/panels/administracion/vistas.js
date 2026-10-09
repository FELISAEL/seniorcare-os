// Controla el menú lateral del panel de administración: cada opción muestra
// solo su tarjeta y oculta las demás, en vez de saltar hacia abajo en la
// misma página. También controla las sub-pestañas dentro de Usuarios,
// Adultos mayores y Analítica (cada una funciona de forma independiente).

(function () {
  function initSidebarViews() {
    const navLinks = document.querySelectorAll(".sidebar .nav-link");
    const views = document.querySelectorAll("[data-view]");
    if (!navLinks.length || !views.length) return;

    function showView(target) {
      views.forEach((view) => {
        view.classList.toggle("is-hidden", view.dataset.view !== target);
      });
      navLinks.forEach((link) => {
        const linkTarget = (link.getAttribute("href") || "").replace("#", "");
        link.classList.toggle("is-active", linkTarget === target);
      });
    }

    navLinks.forEach((link) => {
      link.addEventListener("click", (event) => {
        const target = (link.getAttribute("href") || "").replace("#", "");
        if (!target || !document.querySelector(`[data-view="${target}"]`)) return;
        event.preventDefault();
        showView(target);
        window.history.replaceState(null, "", `#${target}`);
      });
    });

    const initialTarget = (window.location.hash || "").replace("#", "");
    showView(
      document.querySelector(`[data-view="${initialTarget}"]`)
        ? initialTarget
        : "overview"
    );
  }

  // Cada bloque ".subtabs" controla solo las ".subtab-panel" que están
  // dentro de su mismo contenedor padre, para que Usuarios, Adultos
  // mayores y Analítica no se afecten entre sí.
  function initSubtabs() {
    const subtabGroups = document.querySelectorAll(".subtabs");

    subtabGroups.forEach((group) => {
      const container = group.parentElement;
      if (!container) return;

      const links = group.querySelectorAll(".subtab-link");
      const panels = container.querySelectorAll("[data-subtab-panel]");
      if (!links.length || !panels.length) return;

      links.forEach((link) => {
        link.addEventListener("click", () => {
          const target = link.dataset.subtab;
          panels.forEach((panel) => {
            panel.classList.toggle(
              "is-hidden",
              panel.dataset.subtabPanel !== target
            );
          });
          links.forEach((otherLink) => {
            otherLink.classList.toggle("is-active", otherLink === link);
          });
        });
      });
    });
  }

  document.addEventListener("DOMContentLoaded", () => {
    initSidebarViews();
    initSubtabs();
  });
})();