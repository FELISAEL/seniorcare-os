(function () {
  "use strict";

  function messageFrom(error, fallback = "No se pudo completar la operación.") {
    if (!error) return fallback;
    if (typeof error === "string") return error;
    return error.message || fallback;
  }

  function notify(message, options = {}) {
    const target = options.target
      ? document.querySelector(options.target)
      : document.querySelector("#toast, #loginMessage, [data-error-message]");

    if (!target) return;
    target.textContent = message;

    if (target.id === "toast") {
      target.style.background = options.isError === false ? "#102a43" : "#9d1f25";
      target.classList.add("is-visible");
      window.clearTimeout(notify.timeout);
      notify.timeout = window.setTimeout(() => target.classList.remove("is-visible"), 5000);
    }
  }

  function report(error, context = "frontend", options = {}) {
    const message = messageFrom(error);
    console.error(`[SeniorCare:${context}]`, error);
    if (!options.silent) notify(message, { ...options, isError: true });
    return message;
  }

  async function run(task, options = {}) {
    try {
      return await task();
    } catch (error) {
      report(error, options.context || "tarea", options);
      return null;
    }
  }

  async function allSettled(tasks, context = "carga") {
    const results = await Promise.allSettled(tasks);
    results.forEach((result) => {
      if (result.status === "rejected") report(result.reason, context, { silent: true });
    });
    return results;
  }

  window.addEventListener("error", (event) => {
    report(event.error || new Error(event.message), "error-global", { silent: true });
  });

  window.addEventListener("unhandledrejection", (event) => {
    report(event.reason, "promesa-no-controlada", { silent: true });
  });

  window.SeniorCareErrors = { messageFrom, notify, report, run, allSettled };
})();
