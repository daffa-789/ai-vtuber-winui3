import { app, BrowserWindow, globalShortcut, Menu, nativeImage, shell, Tray } from "electron";
import { spawn } from "node:child_process";
import { existsSync, mkdirSync, readdirSync } from "node:fs";
import { join, resolve } from "node:path";
let window, tray, server;
let quitting = false;
const repoRoot = resolve(__dirname, "../..");
const TANDA_SOFTWARE = "--sw-software";
function modeSoftware() {
  return process.argv.includes(TANDA_SOFTWARE) || process.env.SW_SOFTWARE === "1";
}
function hentikanServer() {
  if (server && server.exitCode === null) server.kill();
}
function gagalKarenaGpu(error) {
  const pesan = error instanceof Error ? error.message : String(error);
  return /ERR_FAILED|ERR_NETWORK_CHANGED|ERR_CONNECTION/.test(pesan) || /\(-2\)/.test(pesan);
}
app.commandLine.appendSwitch("autoplay-policy", "no-user-gesture-required");
app.commandLine.appendSwitch("enable-features", "SharedArrayBuffer");

if (modeSoftware()) {
  app.commandLine.appendSwitch("disable-gpu");
  app.commandLine.appendSwitch("enable-unsafe-swiftshader");
}
function iconPath() {
  return app.isPackaged ? join(process.resourcesPath, "icon.png") : resolve(__dirname, "../../resources/icon.png");
}
function runtimeNode() {
  if (app.isPackaged) return { exe: process.execPath, env: { ELECTRON_RUN_AS_NODE: "1" } };
  const kandidat = [
    process.env.ProgramFiles ? join(process.env.ProgramFiles, "nodejs", "node.exe") : "",
    process.env["ProgramFiles(x86)"] ? join(process.env["ProgramFiles(x86)"], "nodejs", "node.exe") : "",
    "C:\\Program Files\\nodejs\\node.exe"
  ].filter(Boolean);
  for (const path of kandidat) if (existsSync(path)) return { exe: path, env: {} };
  return { exe: "node", env: {} };
}
function serverEntry() {
  const bundelDev = join(repoRoot, "out", "server", "main.js");
  if (!app.isPackaged && existsSync(bundelDev)) return bundelDev;
  return app.isPackaged ? join(process.resourcesPath, "server", "main.js") : join(repoRoot, "apps", "server-node", "src", "main.js");
}
function adaModelGguf(root) {
  const jelajah = (dir, kedalaman) => {
    if (!existsSync(dir)) return false;
    for (const item of readdirSync(dir, { withFileTypes: true })) {
      const path = join(dir, item.name);
      if (item.isFile() && item.name.toLowerCase().endsWith(".gguf")) return true;
      if (item.isDirectory() && kedalaman > 0 && jelajah(path, kedalaman - 1)) return true;
    }
    return false;
  };
  return jelajah(join(root, "model"), 2) || jelajah(join(root, "assets", "llm"), 2);
}
function jalankanServer(root, stub) {
  return new Promise((beres, gagal) => {
    const entry = serverEntry();
    if (!existsSync(entry)) {
      gagal(new Error(`skrip server Node belum ada: ${entry}. Jalankan "npm run server:bundle" lebih dulu.`));
      return;
    }
    const argumenServer = [
      "-root",
      root,
      "-port",
      "0",
      "-host",
      "127.0.0.1",
      "-assets",
      join(root, "assets"),
      "-vault",
      join(root, "silver_wolf_memory")
    ];
    if (!app.isPackaged) {
      const staticRoot = process.env.ELECTRON_RENDERER_URL ? void 0 : join(__dirname, "../renderer");
      if (staticRoot) argumenServer.push("-static", staticRoot);
    }
    const argumen = [entry, ...argumenServer];
    const { exe, env } = runtimeNode();
    const anak = spawn(exe, argumen, {
      // `tsx` diresolusi dari `node_modules` akar repo, jadi cwd harus di sana.
      cwd: app.isPackaged ? root : repoRoot,
      windowsHide: true,
      // Mode tiruan dipakai saat belum ada model: UI tetap hidup dan bisa diuji.
      env: { ...process.env, ...env, ...stub ? { VTUBER_STUB: "ya" } : {} },
      stdio: ["ignore", "pipe", "pipe"]
    });
    server = anak;
    let penampung = "";
    const selesai = (fn) => {
      clearTimeout(waktuHabis);
      anak.stdout?.off("data", saatData);
      fn();
    };
    const saatData = (data) => {
      penampung += data.toString("utf8");
      const baris = penampung.split(/\r?\n/);
      penampung = baris.pop() ?? "";
      for (const mentah of baris) {
        const teks = mentah.trim();
        if (!teks) continue;
        console.log(`[server] ${teks}`);
        const cocok = /^port=(\d+)$/.exec(teks);
        if (cocok?.[1]) selesai(() => beres(Number(cocok[1])));
      }
    };
    const waktuHabis = setTimeout(() => selesai(() => gagal(new Error("server Node tidak melaporkan port dalam 60 detik"))), 6e4);
    anak.stdout?.on("data", saatData);
    anak.stderr?.on("data", (data) => console.error(`[server] ${String(data).trimEnd()}`));
    anak.on("error", (error) => selesai(() => gagal(error)));
    anak.on("exit", (kode) => {
      console.warn(`[server] berhenti dengan kode ${kode}`);
      selesai(() => gagal(new Error(`server Node berhenti lebih awal (kode ${kode})`)));
    });
  });
}

async function createWindow(url) {
  const preloadCandidates = [
    join(__dirname, "../preload/index.cjs"),
    join(__dirname, "../preload/index.js"),
    join(__dirname, "../preload/index.mjs")
  ];
  const preloadPath = preloadCandidates.find(existsSync) ?? join(__dirname, "../preload/index.cjs");
  window = new BrowserWindow({
    width: 1180,
    height: 760,
    minWidth: 780,
    minHeight: 560,
    show: false,
    backgroundColor: "#080b12",
    title: "Silver Wolf",
    icon: iconPath(),
    autoHideMenuBar: true,
    webPreferences: { preload: preloadPath, contextIsolation: true, sandbox: true, nodeIntegration: false }
  });
  window.once("ready-to-show", () => window?.show());
  window.on("close", (event) => {
    if (!quitting) {
      event.preventDefault();
      window?.hide();
    }
  });
  window.webContents.setWindowOpenHandler(({ url: target }) => {
    if (/^https?:/.test(target)) void shell.openExternal(target);
    return { action: "deny" };
  });
  window.webContents.on("console-message", (_event, level, message) => {
    const levelStr = level === 3 ? "ERROR" : level === 2 ? "WARN" : "LOG";
    console.log(`[renderer:${levelStr}] ${message}`);
  });
  if (!app.isPackaged) {
    window.webContents.openDevTools({ mode: "detach" });
  }
  try {
    await window.loadURL(url);
  } catch (error) {
    if (!modeSoftware() && gagalKarenaGpu(error)) {
      console.warn("[jendela] proses GPU tidak bisa dipakai, meluncur ulang dengan rendering software");
      hentikanServer();
      app.relaunch({ args: [...process.argv.slice(1), TANDA_SOFTWARE] });
      app.exit(0);
      return;
    }
    throw error;
  }
}
function installTray() {
  const image = nativeImage.createFromPath(iconPath()).resize({ width: 20, height: 20 });
  tray = new Tray(image);
  tray.setToolTip("Silver Wolf");
  tray.setContextMenu(Menu.buildFromTemplate([
    { label: "Tampilkan Silver Wolf", click: () => {
      window?.show();
      window?.focus();
    } },
    { label: "Sembunyikan", click: () => window?.hide() },
    { label: "Buka folder model & konfigurasi", click: () => void shell.openPath(app.getPath("userData")) },
    { type: "separator" },
    { label: "Keluar", click: () => {
      quitting = true;
      app.quit();
    } }
  ]));
  tray.on("double-click", () => {
    window?.show();
    window?.focus();
  });
}
async function boot() {
  const root = app.isPackaged ? app.getPath("userData") : repoRoot;
  if (app.isPackaged) for (const folder of ["assets/piper", "assets/encoders", "assets/voices/silverwolf", "assets/live2d/silverwolf", "model", "bin/llama", "silver_wolf_memory"]) {
    const path = join(root, folder);
    if (!existsSync(path)) mkdirSync(path, { recursive: true });
  }
  const punyaEnv = existsSync(join(root, ".env"));
  const stub = app.isPackaged && (!punyaEnv || !adaModelGguf(root));
  const rendererUrl = process.env.ELECTRON_RENDERER_URL;
  const port = await jalankanServer(root, stub);
  const alamatRenderer = rendererUrl ? new URL(rendererUrl) : void 0;
  alamatRenderer?.searchParams.set("api", `http://127.0.0.1:${port}`);
  await createWindow(alamatRenderer?.toString() ?? `http://127.0.0.1:${port}/`);
  installTray();
  globalShortcut.register("CommandOrControl+Shift+S", () => window?.isVisible() ? window.hide() : window?.show());
  globalShortcut.register("F12", () => window?.webContents.toggleDevTools());
}
if (!app.requestSingleInstanceLock()) app.quit();
else {
  app.on("second-instance", () => {
    window?.show();
    window?.focus();
  });
  app.whenReady().then(boot).catch((error) => {
    console.error(error);
    app.quit();
  });
  app.on("before-quit", () => {
    quitting = true;
  });
  app.on("will-quit", () => {
    globalShortcut.unregisterAll();
    hentikanServer();
  });
  app.on("window-all-closed", () => {
  });
}
