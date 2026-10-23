// A color picker laid out like the Windows app's: a color area (hue across, saturation down),
// a brightness slider, a hex box and Red / Green / Blue boxes.

export function rgbToHsv([r, g, b]) {
  const max = Math.max(r, g, b);
  const d = max - Math.min(r, g, b);
  let h = 0;
  if (d) {
    if (max === r) h = ((g - b) / d) % 6;
    else if (max === g) h = (b - r) / d + 2;
    else h = (r - g) / d + 4;
    h = (h * 60 + 360) % 360;
  }
  return { h, s: max ? d / max : 0, v: max / 255 };
}

export function hsvToRgb({ h, s, v }) {
  const f = (n) => {
    const k = (n + h / 60) % 6;
    return Math.round(255 * (v - v * s * Math.max(0, Math.min(k, 4 - k, 1))));
  };
  return [f(5), f(3), f(1)];
}

export const toHex = (rgb) => "#" + rgb.map((c) => c.toString(16).padStart(2, "0")).join("").toUpperCase();

export function parseHex(text) {
  const m = /^#?([0-9a-f]{6})$/i.exec(text.trim());
  if (!m) return null;
  return [0, 2, 4].map((i) => parseInt(m[1].slice(i, i + 2), 16));
}

export class ColorPicker {
  #hsv = { h: 0, s: 1, v: 1 };
  #rgb = [255, 0, 0];

  /** onChange(rgb) is called whenever the person picks a color. */
  constructor(root, onChange) {
    this.onChange = onChange;
    root.classList.add("picker");
    root.innerHTML = `
      <div class="picker-top">
        <div class="spectrum" tabindex="0" role="slider" aria-label="Color area: hue and saturation">
          <div class="spectrum-white"></div><div class="marker"></div>
        </div>
        <div class="preview" aria-hidden="true"></div>
      </div>
      <input type="range" class="brightness" min="0" max="100" aria-label="Brightness">
      <div class="picker-fields">
        <label class="hex"><span>Hex</span><input type="text" maxlength="7" spellcheck="false" aria-label="Hex color"></label>
        <label><span>Red</span><input type="number" min="0" max="255" data-i="0"></label>
        <label><span>Green</span><input type="number" min="0" max="255" data-i="1"></label>
        <label><span>Blue</span><input type="number" min="0" max="255" data-i="2"></label>
      </div>`;
    this.spectrum = root.querySelector(".spectrum");
    this.marker = root.querySelector(".marker");
    this.preview = root.querySelector(".preview");
    this.brightness = root.querySelector(".brightness");
    this.hex = root.querySelector(".hex input");
    this.channels = [...root.querySelectorAll("input[data-i]")];

    const pick = (event) => {
      const box = this.spectrum.getBoundingClientRect();
      const x = Math.min(1, Math.max(0, (event.clientX - box.left) / box.width));
      const y = Math.min(1, Math.max(0, (event.clientY - box.top) / box.height));
      // Picking in the area of a black color should show the color, so lift the brightness up.
      this.#setHsv({ h: x * 359.99, s: 1 - y, v: this.#hsv.v || 1 });
    };
    this.spectrum.addEventListener("pointerdown", (event) => {
      this.spectrum.setPointerCapture(event.pointerId);
      pick(event);
    });
    this.spectrum.addEventListener("pointermove", (event) => {
      if (this.spectrum.hasPointerCapture(event.pointerId)) pick(event);
    });
    this.spectrum.addEventListener("keydown", (event) => {
      const step = { ArrowLeft: [-5, 0], ArrowRight: [5, 0], ArrowUp: [0, 0.05], ArrowDown: [0, -0.05] }[event.key];
      if (!step) return;
      event.preventDefault();
      const { h, s, v } = this.#hsv;
      this.#setHsv({ h: Math.min(359.99, Math.max(0, h + step[0])), s: Math.min(1, Math.max(0, s + step[1])), v });
    });
    this.brightness.addEventListener("input", () => this.#setHsv({ ...this.#hsv, v: this.brightness.value / 100 }));
    this.hex.addEventListener("change", () => {
      const rgb = parseHex(this.hex.value);
      if (rgb) this.#setRgb(rgb);
      else this.#render();
    });
    for (const input of this.channels) {
      input.addEventListener("change", () => {
        const value = Math.round(Number(input.value));
        if (!Number.isFinite(value)) return this.#render();
        const rgb = [...this.#rgb];
        rgb[Number(input.dataset.i)] = Math.min(255, Math.max(0, value));
        this.#setRgb(rgb);
      });
    }
    this.#render();
  }

  /** Shows a color without reporting a change (used when switching profiles). */
  set(rgb) {
    this.#rgb = [...rgb];
    const hsv = rgbToHsv(rgb);
    // Keep the hue and saturation of a black or gray color, so the area doesn't jump.
    if (hsv.v === 0) hsv.h = this.#hsv.h, hsv.s = this.#hsv.s;
    else if (hsv.s === 0) hsv.h = this.#hsv.h;
    this.#hsv = hsv;
    this.#render();
  }

  #setHsv(hsv) {
    this.#hsv = hsv;
    this.#rgb = hsvToRgb(hsv);
    this.#render();
    this.onChange([...this.#rgb]);
  }

  #setRgb(rgb) {
    this.set(rgb);
    this.onChange([...this.#rgb]);
  }

  #render() {
    const { h, s, v } = this.#hsv;
    this.marker.style.left = `${(h / 360) * 100}%`;
    this.marker.style.top = `${(1 - s) * 100}%`;
    this.spectrum.style.setProperty("--dim", String(1 - v));
    this.spectrum.setAttribute("aria-valuetext", toHex(this.#rgb));
    this.preview.style.background = toHex(this.#rgb);
    const full = toHex(hsvToRgb({ h, s, v: 1 }));
    this.brightness.style.background = `linear-gradient(to right, #000, ${full})`;
    this.brightness.value = String(Math.round(v * 100));
    if (document.activeElement !== this.hex) this.hex.value = toHex(this.#rgb);
    this.channels.forEach((input, i) => (input.value = String(this.#rgb[i])));
  }
}
