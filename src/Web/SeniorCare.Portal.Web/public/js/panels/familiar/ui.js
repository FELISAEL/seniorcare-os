function initializeFamilyNavigation() {
  const tablist = $(".family-tabs");
  const tabs = Array.from(tablist.querySelectorAll('[role="tab"]'));
  const mobileLayout = window.matchMedia("(max-width: 1050px)");
  const updateOrientation = () => tablist.setAttribute(
    "aria-orientation", mobileLayout.matches ? "horizontal" : "vertical"
  );

  updateOrientation();
  mobileLayout.addEventListener("change", updateOrientation);
  tabs.forEach((tab) => {
    tab.addEventListener("click", () => selectFamilyView(tab, tabs));
    tab.addEventListener("keydown", (event) => navigateFamilyTabs(event, tabs));
  });
  selectFamilyView(tabs[0], tabs);
}

function selectFamilyView(selectedTab, tabs) {
  tabs.forEach((tab) => {
    const selected = tab === selectedTab;
    tab.classList.toggle("is-active", selected);
    tab.setAttribute("aria-selected", String(selected));
    tab.tabIndex = selected ? 0 : -1;
    document.getElementById(tab.getAttribute("aria-controls")).hidden = !selected;
  });
  $(".content").scrollTop = 0;
}

function navigateFamilyTabs(event, tabs) {
  const vertical = event.currentTarget.parentElement.getAttribute("aria-orientation") === "vertical";
  const previousKey = vertical ? "ArrowUp" : "ArrowLeft";
  const nextKey = vertical ? "ArrowDown" : "ArrowRight";
  const currentIndex = tabs.indexOf(event.currentTarget);
  let nextIndex;

  if (event.key === "Home") nextIndex = 0;
  else if (event.key === "End") nextIndex = tabs.length - 1;
  else if (event.key === previousKey) nextIndex = (currentIndex - 1 + tabs.length) % tabs.length;
  else if (event.key === nextKey) nextIndex = (currentIndex + 1) % tabs.length;
  else return;

  event.preventDefault();
  tabs[nextIndex].focus({ preventScroll: true });
  selectFamilyView(tabs[nextIndex], tabs);
}

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
