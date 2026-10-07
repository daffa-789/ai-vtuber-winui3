import { defineConfig } from "unocss";
import presetAttributify from "@unocss/preset-attributify";
import presetUno from "@unocss/preset-uno";
var stdin_default = defineConfig({
  presets: [
    presetUno(),
    presetAttributify()
  ],
  theme: {
    colors: {
      malam: "#0d1119",
      lampu: "#e8b673",
      kertas: "#f4ece0",
      tinta: "#1b1a17",
      redup: "#6b6459",
      garis: "#3a3730",
      sukses: "#7fb069",
      bahaya: "#c8553d"
    }
  },
  shortcuts: {
    "panel": "bg-malam/92 text-kertas rounded-lg border border-garis shadow-lg",
    "tombol": "px-3 py-1 rounded border border-garis hover:border-lampu transition-colors"
  }
});
export {
  stdin_default as default
};
