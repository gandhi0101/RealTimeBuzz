window.rtb = window.rtb || {};

window.rtb.localStorage = {
  get: (key) => {
    try {
      return window.localStorage.getItem(key);
    } catch {
      return null;
    }
  },
  set: (key, value) => {
    try {
      window.localStorage.setItem(key, value);
    } catch {
      return null;
    }
  }
};

window.rtb.scroll = {
  toBottom: (element) => {
    if (!element) return;
    element.scrollTop = element.scrollHeight;
  },
  distanceFromBottom: (element) => {
    if (!element) return 0;
    return element.scrollHeight - element.scrollTop - element.clientHeight;
  }
};

window.rtb.notifications = {
  permission: () => {
    if (!("Notification" in window)) return "unsupported";
    return Notification.permission;
  },
  requestPermission: async () => {
    if (!("Notification" in window)) return "unsupported";
    if (Notification.permission === "granted") return "granted";
    if (Notification.permission === "denied") return "denied";
    try {
      const permission = await Notification.requestPermission();
      return permission;
    } catch {
      return "denied";
    }
  },
  notify: (title, body) => {
    if (!("Notification" in window)) return;
    if (Notification.permission !== "granted") return;
    new Notification(title, { body });
  },
  isFocused: () => {
    if (typeof document === "undefined") return true;
    return document.hasFocus();
  }
};
