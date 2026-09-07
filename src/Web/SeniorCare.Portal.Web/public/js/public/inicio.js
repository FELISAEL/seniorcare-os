/* ==========================================================
   SeniorCare OS
   Inicio público

   Único comportamiento de la portada: el panel de
   configuración / accesibilidad (diálogo lateral).
   No hay lógica de acceso: la única acción para entrar
   es el enlace "Iniciar sesión".
========================================================== */

(function () {
  "use strict";

  var STORE_KEY = "sc-inicio-accesibilidad";

  var RATES = { slow: 0.75, normal: 1, fast: 1.4 };
  var RATE_LABEL = { slow: "lenta", normal: "normal", fast: "rápida" };

  var DEFAULTS = {
    contrast: false,
    largeText: false,
    voice: false,
    readOptions: false,
    rate: "normal"
  };

  var root = document.documentElement;

  var toggle = document.querySelector("#configToggle");
  var backdrop = document.querySelector("#configBackdrop");
  var panel = document.querySelector("#configPanel");
  var closeButton = document.querySelector("#configClose");
  var status = document.querySelector("#configStatus");

  var optContrast = document.querySelector("#optContrast");
  var optLargeText = document.querySelector("#optLargeText");
  var optVoice = document.querySelector("#optVoice");
  var optReadOptions = document.querySelector("#optReadOptions");
  var optTestVoice = document.querySelector("#optTestVoice");
  var optReset = document.querySelector("#optReset");
  var speedButtons = Array.prototype.slice.call(
    document.querySelectorAll(".config-speed__option")
  );

  if (!toggle || !panel || !backdrop || !closeButton) {
    return;
  }

  var prefs = clone(DEFAULTS);

  var lastFocused = null;
  var pendingRead = null;

  var voiceSupported =
    "speechSynthesis" in window &&
    typeof window.SpeechSynthesisUtterance === "function";

  var voices = [];
  var chosenVoice = null;

  /* --------------------------------------------------------
     Utilidades
  -------------------------------------------------------- */

  function clone(obj) {
    return JSON.parse(JSON.stringify(obj));
  }

  function labelOf(element) {
    if (!element) return "";
    var aria = element.getAttribute("aria-label");
    if (aria) return aria.trim();
    return (element.textContent || "").replace(/\s+/g, " ").trim();
  }

  /* --------------------------------------------------------
     Persistencia (solo preferencias de accesibilidad)
  -------------------------------------------------------- */

  function loadPrefs() {
    try {
      var raw = window.localStorage.getItem(STORE_KEY);
      if (!raw) return;
      var parsed = JSON.parse(raw);
      if (!parsed || typeof parsed !== "object") return;

      prefs.contrast = parsed.contrast === true;
      prefs.largeText = parsed.largeText === true;
      prefs.voice = parsed.voice === true;
      prefs.readOptions = parsed.readOptions === true;
      if (RATES.hasOwnProperty(parsed.rate)) prefs.rate = parsed.rate;
    } catch (error) {
      /* Sin almacenamiento: se usan los valores por defecto. */
    }
  }

  function savePrefs() {
    try {
      window.localStorage.setItem(
        STORE_KEY,
        JSON.stringify({
          contrast: prefs.contrast,
          largeText: prefs.largeText,
          voice: prefs.voice,
          readOptions: prefs.readOptions,
          rate: prefs.rate
        })
      );
    } catch (error) {
      /* Se ignora: la preferencia sigue activa en esta visita. */
    }
  }

  /* --------------------------------------------------------
     Voz
  -------------------------------------------------------- */

  function refreshVoices() {
    try {
      voices = window.speechSynthesis.getVoices() || [];
    } catch (error) {
      voices = [];
    }
    chosenVoice =
      firstVoice(/^es[-_]gt/i) ||
      firstVoice(/^es[-_]/i) ||
      firstVoice(/^es$/i) ||
      firstVoice(/es/i) ||
      null;
  }

  function firstVoice(pattern) {
    for (var i = 0; i < voices.length; i += 1) {
      if (pattern.test(voices[i].lang || "")) return voices[i];
    }
    return null;
  }

  function stopSpeech() {
    if (voiceSupported) {
      try { window.speechSynthesis.cancel(); } catch (error) { /* nada */ }
    }
  }

  function speak(text) {
    if (!voiceSupported || !prefs.voice || !text) return;
    stopSpeech();
    var utterance = new window.SpeechSynthesisUtterance(text);
    utterance.lang = (chosenVoice && chosenVoice.lang) || "es-ES";
    if (chosenVoice) utterance.voice = chosenVoice;
    utterance.rate = RATES[prefs.rate] || 1;
    try { window.speechSynthesis.speak(utterance); } catch (error) { /* nada */ }
  }

  /* --------------------------------------------------------
     Anuncios (una sola vez, sin duplicar la lectura de foco)
  -------------------------------------------------------- */

  function announce(text) {
    if (pendingRead) {
      window.clearTimeout(pendingRead);
      pendingRead = null;
    }
    if (status) status.textContent = text;
    speak(text);
  }

  function setStatusSilently(text) {
    if (status) status.textContent = text;
  }

  /* --------------------------------------------------------
     Aplicar preferencias al documento y a ARIA
  -------------------------------------------------------- */

  function applyAll() {
    root.classList.toggle("sc-hc", prefs.contrast);
    root.classList.toggle("sc-text-lg", prefs.largeText);

    setChecked(optContrast, prefs.contrast);
    setChecked(optLargeText, prefs.largeText);
    setChecked(optVoice, prefs.voice);
    setChecked(optReadOptions, prefs.readOptions);

    speedButtons.forEach(function (button) {
      setChecked(button, button.getAttribute("data-rate") === prefs.rate);
    });

    updateVoiceDependents();
  }

  function setChecked(element, isChecked) {
    if (element) element.setAttribute("aria-checked", isChecked ? "true" : "false");
  }

  function updateVoiceDependents() {
    var enabled = prefs.voice;
    [optReadOptions, optTestVoice].forEach(function (element) {
      if (!element) return;
      element.disabled = !enabled;
      element.setAttribute("aria-disabled", enabled ? "false" : "true");
    });
  }

  /* --------------------------------------------------------
     Diálogo: abrir / cerrar, foco y contención de Tab
  -------------------------------------------------------- */

  function focusable() {
    var nodes = panel.querySelectorAll(
      'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
    );
    return Array.prototype.slice.call(nodes).filter(function (node) {
      return node.offsetWidth > 0 || node.offsetHeight > 0 || node === document.activeElement;
    });
  }

  function openPanel() {
    lastFocused = document.activeElement;
    backdrop.hidden = false;
    panel.hidden = false;
    document.body.classList.add("config-open");
    toggle.setAttribute("aria-expanded", "true");
    window.requestAnimationFrame(function () {
      closeButton.focus();
    });
  }

  function closePanel() {
    if (panel.hidden) return;
    panel.hidden = true;
    backdrop.hidden = true;
    document.body.classList.remove("config-open");
    toggle.setAttribute("aria-expanded", "false");
    if (lastFocused && typeof lastFocused.focus === "function") {
      lastFocused.focus();
    } else {
      toggle.focus();
    }
  }

  toggle.addEventListener("click", function () {
    if (panel.hidden) openPanel();
    else closePanel();
  });

  closeButton.addEventListener("click", closePanel);

  backdrop.addEventListener("click", closePanel);

  panel.addEventListener("keydown", function (event) {
    if (event.key === "Escape") {
      event.preventDefault();
      closePanel();
      return;
    }
    if (event.key !== "Tab") return;

    var items = focusable();
    if (!items.length) return;

    var first = items[0];
    var last = items[items.length - 1];
    var active = document.activeElement;

    if (event.shiftKey && (active === first || !panel.contains(active))) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && active === last) {
      event.preventDefault();
      first.focus();
    }
  });

  document.addEventListener("keydown", function (event) {
    if (event.key === "Escape" && !panel.hidden) {
      event.preventDefault();
      closePanel();
    }
  });

  /* --------------------------------------------------------
     Lectura de opciones al recibir foco (nunca con el ratón)
  -------------------------------------------------------- */

  panel.addEventListener("focusin", function (event) {
    if (!prefs.voice || !prefs.readOptions) return;
    var target = event.target;
    if (!target || !panel.contains(target)) return;
    var text = labelOf(target);
    if (!text) return;
    if (pendingRead) window.clearTimeout(pendingRead);
    pendingRead = window.setTimeout(function () {
      pendingRead = null;
      speak(text);
    }, 180);
  });

  /* --------------------------------------------------------
     Interruptores independientes
  -------------------------------------------------------- */

  optContrast.addEventListener("click", function () {
    prefs.contrast = !prefs.contrast;
    root.classList.toggle("sc-hc", prefs.contrast);
    setChecked(optContrast, prefs.contrast);
    savePrefs();
    announce(prefs.contrast ? "Alto contraste activado" : "Alto contraste desactivado");
  });

  optLargeText.addEventListener("click", function () {
    prefs.largeText = !prefs.largeText;
    root.classList.toggle("sc-text-lg", prefs.largeText);
    setChecked(optLargeText, prefs.largeText);
    savePrefs();
    announce(prefs.largeText ? "Texto grande activado" : "Texto grande desactivado");
  });

  optVoice.addEventListener("click", function () {
    prefs.voice = !prefs.voice;
    setChecked(optVoice, prefs.voice);
    updateVoiceDependents();
    savePrefs();

    if (prefs.voice) {
      announce("Modo de voz activado");
    } else {
      stopSpeech();
      setStatusSilently("Modo de voz desactivado");
    }
  });

  optReadOptions.addEventListener("click", function () {
    if (!prefs.voice) return;
    prefs.readOptions = !prefs.readOptions;
    setChecked(optReadOptions, prefs.readOptions);
    savePrefs();
    announce(
      prefs.readOptions
        ? "Lectura de opciones activada"
        : "Lectura de opciones desactivada"
    );
  });

  /* --------------------------------------------------------
     Velocidad de la voz
  -------------------------------------------------------- */

  speedButtons.forEach(function (button) {
    button.addEventListener("click", function () {
      var rate = button.getAttribute("data-rate");
      if (!RATES.hasOwnProperty(rate)) return;
      prefs.rate = rate;
      speedButtons.forEach(function (other) {
        setChecked(other, other === button);
      });
      savePrefs();
      announce("Velocidad " + RATE_LABEL[rate]);
    });
  });

  /* --------------------------------------------------------
     Probar voz
  -------------------------------------------------------- */

  optTestVoice.addEventListener("click", function () {
    if (!prefs.voice) return;
    announce("SeniorCare está listo para acompañarte");
  });

  /* --------------------------------------------------------
     Restaurar configuración
  -------------------------------------------------------- */

  optReset.addEventListener("click", function () {
    stopSpeech();
    prefs = clone(DEFAULTS);
    applyAll();
    savePrefs();
    setStatusSilently("Configuración restaurada");
  });

  /* --------------------------------------------------------
     Detener locución si la página se oculta o se cierra
  -------------------------------------------------------- */

  window.addEventListener("pagehide", stopSpeech);
  window.addEventListener("beforeunload", stopSpeech);

  /* --------------------------------------------------------
     Arranque
  -------------------------------------------------------- */

  if (voiceSupported) {
    refreshVoices();
    if (typeof window.speechSynthesis.addEventListener === "function") {
      window.speechSynthesis.addEventListener("voiceschanged", refreshVoices);
    }
  }

  loadPrefs();
  applyAll();

  panel.hidden = true;
  backdrop.hidden = true;
  toggle.setAttribute("aria-expanded", "false");
})();
