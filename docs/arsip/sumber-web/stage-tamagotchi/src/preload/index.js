const { contextBridge } = require("electron");

contextBridge.exposeInMainWorld("silverWolfDesktop", {
  platform: process.platform,
  versions: process.versions
});
