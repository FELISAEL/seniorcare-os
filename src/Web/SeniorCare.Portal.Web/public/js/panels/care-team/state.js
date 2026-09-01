const state = {
  session: null,
  residents: [],
  alerts: [],
  metrics: [],
  videoRooms: [],
  users: [],
  selectedResident: null
};

const expectedRole = document.body.dataset.expectedRole;
const isAdmin = expectedRole === "admin";
const $ = (selector) => document.querySelector(selector);

const elements = {
  operatorName: $("#operatorName"),
  residentCount: $("#residentCount"),
  alertCount: $("#alertCount"),
  adherence: $("#adherence"),
  doseSummary: $("#doseSummary"),
  etlTime: $("#etlTime"),
  noAlerts: $("#noAlerts"),
  alertList: $("#alertList"),
  residentList: $("#residentList"),
  scheduleList: $("#scheduleList"),
  selectedResidentTitle: $("#selectedResidentTitle"),
  medicationForm: $("#medicationForm"),
  medicationMessage: $("#medicationMessage"),
  metricsList: $("#metricsList"),
  videoList: $("#videoList"),
  toast: $("#toast"),
  userList: $("#userList"),
  userForm: $("#userForm"),
  userMessage: $("#userMessage"),
  residentLink: $("#residentLink")
};
