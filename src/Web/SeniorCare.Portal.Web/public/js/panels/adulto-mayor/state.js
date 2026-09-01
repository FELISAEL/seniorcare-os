const state = {
  session: null,
  resident: null,
  medications: [],
  voiceEnabled: localStorage.getItem("seniorcare.voice") !== "off"
};

const elements = {
  residentName: document.querySelector("#residentName"),
  greeting: document.querySelector("#greeting"),
  currentDate: document.querySelector("#currentDate"),
  currentTime: document.querySelector("#currentTime"),
  connectionStatus: document.querySelector("#connectionStatus"),
  todaySummary: document.querySelector("#todaySummary"),
  medicationPanel: document.querySelector("#medicationPanel"),
  medicationList: document.querySelector("#medicationList"),
  emergencyDialog: document.querySelector("#emergencyDialog"),
  toast: document.querySelector("#toast"),
  toggleVoice: document.querySelector("#toggleVoice")
};
