// Phase W2 browser host: boots the .NET wasm runtime and provides the
// 'webgl.js' import module backing OpenRA.WasmProbe.Browser.WebGL ([JSImport]).
// Handles are ints into a JS-side table so the managed side never touches
// JSObject marshalling. See WASM-PORT-PLAN.md.
// Deliberately NOT cache-busted with ?v=: dotnet.js derives the URLs of the
// runtime's own sub-modules from its own, so the query rides along to
// dotnet.runtime.*.js and dotnet.native.*.js and their fetches fail. Every
// other _framework file is already content-hashed by the build, so versioning
// this one bought nothing; index.html's versioned main.js is what actually
// prevents a stale/fresh mix.
import { dotnet } from './_framework/dotnet.js';

const logEl = document.getElementById('log');
const log = line => { logEl.textContent += '\n' + line; };

const canvas = document.getElementById('canvas');

// Build id is logged rather than drawn on screen: still useful when someone
// reports a problem, without putting developer chrome over the game.
console.log('[play] build __BUILD_ID__');

let gl = null;
const handles = new Map();
let nextHandle = 1;
const keep = obj => { handles.set(nextHandle, obj); return nextHandle++; };

const qmode = new URLSearchParams(location.search).get('mode');
const bootMode = (qmode === 'play' || qmode === 'autopilot') ? qmode : 'probe';

// Terrain toggle: which reskinned ysmir-*.oramap to launch. Persists across
// reloads via the query string (see the #terrain button below), not state.
const qterrain = new URLSearchParams(location.search).get('terrain');
const terrainMode = (qterrain === 'lunar' || qterrain === 'mars') ? qterrain : 'earth';

// Testing override: ?dpr=2 forces a devicePixelRatio, letting a standard
// display session exercise the exact HiDPI code path that a Retina
// laptop/iPad (dpr 2) hits for real. Real users never set this.
const dprOverride = parseFloat(new URLSearchParams(location.search).get('dpr'));
const effectiveDpr = () => dprOverride || window.devicePixelRatio || 1;

const webgl = {
	hasDocument: () => true,
	getBootMode: () => bootMode,
	getTerrainMode: () => terrainMode,
	getPersistedData: key => window.localStorage.getItem(key) || "",
	setPersistedData: (key, value) => window.localStorage.setItem(key, value),

	// Phase W3a: text fetch for the VFS-over-HTTP direction.
	fetchText: async url => {
		const r = await fetch(url);
		if (!r.ok)
			throw new Error(`fetch ${url} -> HTTP ${r.status}`);
		return r.text();
	},

	// Phase W3i-b: binary fetch as base64 (fonts, PNGs, later .mix content).
	fetchBase64: async url => {
		const r = await fetch(url);
		if (!r.ok)
			throw new Error(`fetch ${url} -> HTTP ${r.status}`);
		const buf = new Uint8Array(await r.arrayBuffer());
		let s = '';
		const chunk = 0x8000;
		for (let i = 0; i < buf.length; i += chunk)
			s += String.fromCharCode.apply(null, buf.subarray(i, i + chunk));
		return btoa(s);
	},

	// w/h are physical pixels (the engine's "native" resolution: logical
	// window size * devicePixelRatio, computed C#-side from getWindowSize +
	// getDevicePixelRatio) -- a HiDPI backing buffer for crisp text/art,
	// same convention desktop OpenRA uses on Retina displays. The element is
	// then displayed at the smaller logical CSS size via style.width/height,
	// which is what makes the extra buffer density actually sharpen the
	// picture instead of just rendering a bigger picture.
	init: (w, h) => {
		canvas.width = w;
		canvas.height = h;
		canvas.style.width = (w / effectiveDpr()) + 'px';
		canvas.style.height = (h / effectiveDpr()) + 'px';
		gl = canvas.getContext('webgl2', { preserveDrawingBuffer: true });
		return gl ? 1 : 0;
	},

	getDevicePixelRatio: () => effectiveDpr(),

	clearColor: (r, g, b, a) => gl.clearColor(r, g, b, a),
	clear: () => gl.clear(gl.COLOR_BUFFER_BIT),

	compileProgram: (vsSource, fsSource) => {
		const compile = (type, source) => {
			const s = gl.createShader(type);
			gl.shaderSource(s, source);
			gl.compileShader(s);
			if (!gl.getShaderParameter(s, gl.COMPILE_STATUS)) {
				console.error('[webgl] shader compile failed:', gl.getShaderInfoLog(s));
				return null;
			}
			return s;
		};
		const vs = compile(gl.VERTEX_SHADER, vsSource);
		const fs = compile(gl.FRAGMENT_SHADER, fsSource);
		if (!vs || !fs)
			return 0;
		const p = gl.createProgram();
		gl.attachShader(p, vs);
		gl.attachShader(p, fs);
		gl.linkProgram(p);
		if (!gl.getProgramParameter(p, gl.LINK_STATUS)) {
			console.error('[webgl] program link failed:', gl.getProgramInfoLog(p));
			return 0;
		}
		return keep(p);
	},

	useProgram: p => gl.useProgram(handles.get(p)),
	createBuffer: () => keep(gl.createBuffer()),
	bindArrayBuffer: b => gl.bindBuffer(gl.ARRAY_BUFFER, handles.get(b)),
	bufferData: data => gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(data), gl.STATIC_DRAW),

	attrib: (p, name, size, stride, offset) => {
		const loc = gl.getAttribLocation(handles.get(p), name);
		gl.enableVertexAttribArray(loc);
		gl.vertexAttribPointer(loc, size, gl.FLOAT, false, stride, offset);
	},

	createTexture: () => keep(gl.createTexture()),
	bindTexture: t => gl.bindTexture(gl.TEXTURE_2D, handles.get(t)),

	texImage2D: (w, h, rgba) => {
		gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, w, h, 0, gl.RGBA, gl.UNSIGNED_BYTE, new Uint8Array(rgba));
		gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
		gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
	},

	// RGBA32F is a core WebGL2 sampling format (no extension needed for
	// texImage2D/sampling -- only rendering INTO one needs EXT_color_buffer_float).
	// Forced to NEAREST: OES_texture_float_linear isn't guaranteed, and palette/
	// lookup-style float data (e.g. HardwarePalette's color shifts) wants exact
	// texel reads, not interpolation, anyway.
	texImage2DFloat: (w, h, data) => {
		gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA32F, w, h, 0, gl.RGBA, gl.FLOAT, new Float32Array(data));
		gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
		gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
	},

	// Mirrors the desktop platform's glCopyTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA8, ...):
	// copies straight from the currently-bound framebuffer into the currently-bound
	// texture on the GPU, allocating storage sized to match -- no CPU readback needed.
	copyTexImage2D: (x, y, w, h) => {
		gl.copyTexImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, x, y, w, h, 0);
		gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
		gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
	},

	drawArrays: (first, count) => gl.drawArrays(gl.TRIANGLE_STRIP, first, count),

	// Texture readback. WebGL2 is GLES-flavoured and has no glGetTexImage, so
	// take the same route OpenRA.Platforms.Default takes on Embedded profiles:
	// attach the texture to a scratch framebuffer and readPixels out of it,
	// restoring the previously bound framebuffer afterwards. Returns BGRA to
	// match what the engine's SheetType.BGRA consumers expect.
	readTexturePixels: (t, w, h) => {
		const prevFb = gl.getParameter(gl.FRAMEBUFFER_BINDING);
		const fb = gl.createFramebuffer();
		gl.bindFramebuffer(gl.FRAMEBUFFER, fb);
		gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, handles.get(t), 0);
		const px = new Uint8Array(w * h * 4);
		gl.readPixels(0, 0, w, h, gl.RGBA, gl.UNSIGNED_BYTE, px);
		gl.bindFramebuffer(gl.FRAMEBUFFER, prevFb);
		gl.deleteFramebuffer(fb);
		for (let i = 0; i < px.length; i += 4) {
			const r = px[i];
			px[i] = px[i + 2];
			px[i + 2] = r;
		}

		return px;
	},

	readPixel: (x, y) => {
		const px = new Uint8Array(4);
		gl.readPixels(x, y, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, px);
		return Array.from(px);
	},

	getError: () => gl.getError(),

	// --- Phase W3c: full platform-layer surface (OpenRA.Platforms.Browser) ---
	getCanvasSize: () => [canvas.width, canvas.height],

	// The engine's WINDOW_WIDTH/HEIGHT (and everything the chrome YAML lays
	// out against) comes straight from this at boot -- used to size the
	// canvas to the real available viewport instead of a fixed 1024x768.
	//
	// window.innerWidth/innerHeight is NOT reliably final the instant this
	// module starts running: some browsers (mobile Safari/Chrome collapsing
	// their URL bar shortly after load, changing innerHeight; a host page
	// still settling its own layout) report a transient, too-small size
	// first. The engine only ever reads this once at boot and has no live
	// resize path (desktop OpenRA doesn't either), so a stale early read
	// means EVERY button for the rest of the session is laid out for a
	// window that no longer matches what's on screen -- clicks land exactly
	// where they should for the wrong, smaller canvas, so nothing the user
	// sees ever responds. Poll until two consecutive checks agree (or a
	// generous cap elapses) before committing to a size.
	// Resolves "width,height" (a plain string -- Task<int[]> isn't supported
	// by the JSImport source generator for async returns, only sync ones;
	// Task<string> is the same pattern fetchText/fetchBase64 already use).
	getWindowSize: () => new Promise(resolve => {
		// Two matching reads ~200ms apart is NOT enough evidence that the
		// layout has settled: a pane or tab often opens small and expands a
		// moment later (a phone collapsing its URL bar does the same), and
		// sampling during that quiet period locks the engine to the small
		// size for the whole session -- it renders into a corner of a much
		// larger canvas, because the size is read once and never revisited.
		// Require a longer unbroken run of identical reads AND a minimum
		// settle time before committing. Zero sizes never count as stable:
		// a hidden or not-yet-laid-out tab reports 0 and would otherwise be
		// treated as a legitimate answer.
		const POLL_MS = 100;
		const REQUIRED_STABLE = 4;   // ~400ms unchanged
		const MIN_SETTLE_MS = 600;
		const MAX_WAIT_MS = 4000;

		const started = Date.now();
		let last = [0, 0];
		let stableChecks = 0;
		const poll = () => {
			const now = [window.innerWidth, window.innerHeight];
			const elapsed = Date.now() - started;
			const usable = now[0] > 0 && now[1] > 0;

			if (usable && now[0] === last[0] && now[1] === last[1])
				stableChecks++;
			else
				stableChecks = 0;

			last = now;

			const settled = usable && stableChecks >= REQUIRED_STABLE && elapsed >= MIN_SETTLE_MS;
			if (settled) {
				resolve(`${now[0]},${now[1]}`);
				return;
			}

			if (elapsed >= MAX_WAIT_MS) {
				// Out of patience. If the window still reports nothing usable
				// (a tab that never got a layout), boot at a sane default
				// rather than waiting forever -- a playable default size beats
				// a page that never starts, and beats a 0x0 canvas.
				const [w, h] = usable ? now : [1024, 768];
				resolve(`${w},${h}`);
				return;
			}

			setTimeout(poll, POLL_MS);
		};

		poll();
	}),
	viewport: (x, y, w, h) => gl.viewport(x, y, w, h),
	clearAll: () => gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT),
	clearDepth: () => gl.clear(gl.DEPTH_BUFFER_BIT),
	depthEnable: () => { gl.enable(gl.DEPTH_TEST); gl.depthFunc(gl.LEQUAL); },
	depthDisable: () => gl.disable(gl.DEPTH_TEST),
	scissorEnable: (x, y, w, h) => { gl.enable(gl.SCISSOR_TEST); gl.scissor(x, y, w, h); },
	scissorDisable: () => gl.disable(gl.SCISSOR_TEST),

	// BlendMode enum order: None, Alpha, Additive, Subtractive, Multiply,
	// Multiplicative, DoubleMultiplicative, LowAdditive, Screen, Translucent.
	blendMode: mode => {
		if (mode === 0) { gl.disable(gl.BLEND); return; }
		gl.enable(gl.BLEND);
		gl.blendEquation(mode === 3 ? gl.FUNC_REVERSE_SUBTRACT : gl.FUNC_ADD);
		switch (mode) {
			case 1: gl.blendFunc(gl.ONE, gl.ONE_MINUS_SRC_ALPHA); break;
			case 2: case 3: gl.blendFunc(gl.ONE, gl.ONE); break;
			case 4: gl.blendFunc(gl.DST_COLOR, gl.ZERO); break;
			case 5: gl.blendFunc(gl.DST_COLOR, gl.ONE_MINUS_SRC_ALPHA); break;
			case 6: gl.blendFunc(gl.DST_COLOR, gl.SRC_COLOR); break;
			case 7: gl.blendFunc(gl.SRC_ALPHA, gl.ONE); break;
			case 8: gl.blendFunc(gl.ONE_MINUS_DST_COLOR, gl.ONE); break;
			case 9: gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA); break;
			default: gl.blendFunc(gl.ONE, gl.ONE_MINUS_SRC_ALPHA); break;
		}
	},

	bufferDataSize: (size, dynamic) =>
		gl.bufferData(gl.ARRAY_BUFFER, size, dynamic ? gl.DYNAMIC_DRAW : gl.STATIC_DRAW),
	bufferDataBytes: (bytes, dynamic) =>
		gl.bufferData(gl.ARRAY_BUFFER, new Uint8Array(bytes), dynamic ? gl.DYNAMIC_DRAW : gl.STATIC_DRAW),
	bufferSubDataBytes: (byteOffset, bytes) =>
		gl.bufferSubData(gl.ARRAY_BUFFER, byteOffset, new Uint8Array(bytes)),
	bindElementBuffer: b => gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, handles.get(b)),
	elementBufferData: indices =>
		gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, new Uint32Array(indices), gl.STATIC_DRAW),

	attribPointer: (p, name, components, glType, stride, offset) => {
		const loc = gl.getAttribLocation(handles.get(p), name);
		if (loc < 0) return;
		gl.enableVertexAttribArray(loc);
		gl.vertexAttribPointer(loc, components, glType, false, stride, offset);
	},
	attribIPointer: (p, name, components, glType, stride, offset) => {
		const loc = gl.getAttribLocation(handles.get(p), name);
		if (loc < 0) return;
		gl.enableVertexAttribArray(loc);
		gl.vertexAttribIPointer(loc, components, glType, stride, offset);
	},

	getUniform: (p, name) => {
		const loc = gl.getUniformLocation(handles.get(p), name);
		return loc ? keep(loc) : 0;
	},
	uniform1i: (loc, v) => { if (loc) gl.uniform1i(handles.get(loc), v); },
	uniform1f: (loc, v) => { if (loc) gl.uniform1f(handles.get(loc), v); },
	uniform2f: (loc, x, y) => { if (loc) gl.uniform2f(handles.get(loc), x, y); },
	uniform3f: (loc, x, y, z) => { if (loc) gl.uniform3f(handles.get(loc), x, y, z); },
	uniform1fv: (loc, v) => { if (loc) gl.uniform1fv(handles.get(loc), new Float32Array(v)); },
	uniformMatrix4fv: (loc, v) => { if (loc) gl.uniformMatrix4fv(handles.get(loc), false, new Float32Array(v)); },
	activeTexture: unit => gl.activeTexture(gl.TEXTURE0 + unit),
	texFilter: linear => {
		const f = linear ? gl.LINEAR : gl.NEAREST;
		gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, f);
		gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, f);
	},

	createFramebufferTex: (w, h) => {
		const tex = gl.createTexture();
		gl.bindTexture(gl.TEXTURE_2D, tex);
		gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, w, h, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
		gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
		gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
		const fb = gl.createFramebuffer();
		gl.bindFramebuffer(gl.FRAMEBUFFER, fb);
		gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, tex, 0);
		gl.bindFramebuffer(gl.FRAMEBUFFER, null);
		return [keep(fb), keep(tex)];
	},
	bindFramebuffer: fb => gl.bindFramebuffer(gl.FRAMEBUFFER, fb ? handles.get(fb) : null),

	drawArraysMode: (mode, first, count) => gl.drawArrays(mode, first, count),
	drawElementsBytes: (count, byteOffset) =>
		gl.drawElements(gl.TRIANGLES, count, gl.UNSIGNED_INT, byteOffset),

	// --- Phase W4b: Web Audio (int handles; suspended until user gesture) ---
	audioInit: () => {
		if (audio.ctx)
			return 1;
		try {
			audio.ctx = new AudioContext();
			audio.master = audio.ctx.createGain();
			audio.master.connect(audio.ctx.destination);
			// Resume on first user gesture (browser autoplay policy).
			const resume = () => { audio.ctx.resume(); };
			window.addEventListener('pointerdown', resume, { once: true });
			window.addEventListener('keydown', resume, { once: true });
			return 1;
		} catch {
			return 0;
		}
	},
	audioState: () => audio.ctx ? audio.ctx.state : 'unavailable',
	audioMasterVolume: v => { if (audio.master) audio.master.gain.value = v; },

	audioCreateBuffer: (channels, sampleBits, sampleRate, pcm) => {
		const bytes = new Uint8Array(pcm);
		const bytesPer = sampleBits / 8;
		const frames = Math.max(1, (bytes.length / bytesPer / channels) | 0);
		const buffer = audio.ctx.createBuffer(channels, frames, sampleRate);
		for (let ch = 0; ch < channels; ch++) {
			const out = buffer.getChannelData(ch);
			if (sampleBits === 16) {
				const s16 = new Int16Array(bytes.buffer, bytes.byteOffset, (bytes.length / 2) | 0);
				for (let i = 0; i < frames; i++)
					out[i] = (s16[i * channels + ch] || 0) / 32768;
			} else {
				for (let i = 0; i < frames; i++)
					out[i] = ((bytes[i * channels + ch] || 128) - 128) / 128;
			}
		}
		return keep(buffer);
	},

	audioPlay: (bufferHandle, loop, volume, pan) => {
		const src = audio.ctx.createBufferSource();
		src.buffer = handles.get(bufferHandle);
		src.loop = loop;
		const gain = audio.ctx.createGain();
		gain.gain.value = volume;
		const panner = audio.ctx.createStereoPanner();
		panner.pan.value = pan;
		src.connect(gain).connect(panner).connect(audio.master);
		const entry = { src, gain, panner, ended: false, startedAt: audio.ctx.currentTime };
		src.onended = () => { entry.ended = true; };
		src.start();
		return keep(entry);
	},

	audioSetVolume: (h, v) => { const e = handles.get(h); if (e) e.gain.gain.value = v; },
	audioSetPan: (h, p) => { const e = handles.get(h); if (e) e.panner.pan.value = p; },
	audioPause: (h, paused) => { const e = handles.get(h); if (e) e.gain.gain.value = paused ? 0 : 1; },
	audioStop: (h) => { const e = handles.get(h); if (e && !e.ended) { try { e.src.stop(); } catch { } } },
	audioComplete: h => { const e = handles.get(h); return e ? e.ended : true; },
	audioSeekSeconds: h => {
		const e = handles.get(h);
		return e && !e.ended ? audio.ctx.currentTime - e.startedAt : 0;
	},

	// --- Phase W3d: Canvas2D glyph rasterization (1 byte/px alpha, FreeType
	// conventions: Offset = (bearingX, -ascent)) ---
	measureGlyph: (ch, px) => {
		glyphCtx.font = `${px}px sans-serif`;
		const m = glyphCtx.measureText(ch);
		const left = Math.ceil(m.actualBoundingBoxLeft);
		const ascent = Math.ceil(m.actualBoundingBoxAscent);
		const w = Math.max(1, left + Math.ceil(m.actualBoundingBoxRight));
		const h = Math.max(1, ascent + Math.ceil(m.actualBoundingBoxDescent));
		return [w, h, -left, -ascent, Math.round(m.width * 100)];
	},

	rasterizeGlyph: (ch, px) => {
		glyphCtx.font = `${px}px sans-serif`;
		const m = glyphCtx.measureText(ch);
		const left = Math.ceil(m.actualBoundingBoxLeft);
		const ascent = Math.ceil(m.actualBoundingBoxAscent);
		const w = Math.max(1, left + Math.ceil(m.actualBoundingBoxRight));
		const h = Math.max(1, ascent + Math.ceil(m.actualBoundingBoxDescent));
		glyphCtx.clearRect(0, 0, glyphCanvas.width, glyphCanvas.height);
		glyphCtx.fillStyle = '#fff';
		glyphCtx.fillText(ch, left, ascent);
		const rgba = glyphCtx.getImageData(0, 0, w, h).data;
		const alpha = new Uint8Array(w * h);
		for (let i = 0; i < alpha.length; i++)
			alpha[i] = rgba[i * 4 + 3];
		return alpha;
	},

	// --- Phase W3d: input pump. Records are 8 doubles:
	// mouse: [1, kind(0 down/1 move/2 up/3 scroll), button, x, y, dx, dy, mods]
	// key:   [2, kind(0 down/1 up), keycode, mods, charCode, repeat, 0, 0] ---
	pumpEvents: () => {
		const flat = new Array(inputQueue.length * 8);
		for (let i = 0; i < inputQueue.length; i++)
			for (let j = 0; j < 8; j++)
				flat[i * 8 + j] = inputQueue[i][j];
		inputQueue.length = 0;
		return flat;
	},

	// Gate helper: dispatch real DOM events so the listener->queue->pump chain
	// is exercised end to end.
	synthesizeTestInput: () => {
		const r = canvas.getBoundingClientRect();
		const opts = { bubbles: true, clientX: r.left + 30, clientY: r.top + 40, button: 0 };
		canvas.dispatchEvent(new MouseEvent('mousedown', opts));
		canvas.dispatchEvent(new MouseEvent('mouseup', opts));
		window.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: 'a' }));
	},
};

// Web Audio state (Phase W4b).
const audio = { ctx: null, master: null };

// Glyph scratch canvas (Phase W3d).
const glyphCanvas = document.createElement('canvas');
glyphCanvas.width = 256;
glyphCanvas.height = 256;
const glyphCtx = glyphCanvas.getContext('2d', { willReadFrequently: true });

// Input capture (Phase W3d). Engine Modifiers: Shift=1, Alt=2, Ctrl=4, Meta=8.
// Keycodes follow the engine's SDL-style enum: lowercase ascii for printable
// keys; arrows are scancode | (1<<30) (pinned from OpenRA.Game Keycode.cs).
const inputQueue = [];
const mods = e => (e.shiftKey ? 1 : 0) | (e.altKey ? 2 : 0) | (e.ctrlKey ? 4 : 0) | (e.metaKey ? 8 : 0);
const keycodeOf = e => {
	if (e.key.length === 1) {
		const c = e.key.toLowerCase().charCodeAt(0);
		if (c >= 32 && c < 127) return c;
	}
	switch (e.key) {
		case 'Enter': return 13;
		case 'Escape': return 27;
		case 'ArrowRight': return 79 | (1 << 30);
		case 'ArrowLeft': return 80 | (1 << 30);
		case 'ArrowDown': return 81 | (1 << 30);
		case 'ArrowUp': return 82 | (1 << 30);
		default: return 0;
	}
};
const buttonFlag = b => (b === 0 ? 1 : b === 2 ? 2 : b === 1 ? 4 : 0);

// Map client coordinates to the engine's logical/effective coordinate space
// (WINDOW_WIDTH/HEIGHT -- what every chrome YAML X/Y is expressed in), NOT
// the physical pixel buffer: canvas.width is now devicePixelRatio times
// larger than its CSS display size (see init/getDevicePixelRatio above), so
// a raw CSS-relative offset already lands exactly in logical space without
// any further scaling. Multiplying by canvas.width/rect.width here would
// instead produce physical-pixel coordinates and misalign every click by
// exactly the DPI scale factor. offsetX is padding-box relative and skews if
// the canvas ever gets a border, hence going through getBoundingClientRect().
const canvasXY = e => {
	const r = canvas.getBoundingClientRect();
	return [Math.round(e.clientX - r.left), Math.round(e.clientY - r.top)];
};
canvas.addEventListener('mousedown', e => { const [x, y] = canvasXY(e); inputQueue.push([1, 0, buttonFlag(e.button), x, y, 0, 0, mods(e)]); });
canvas.addEventListener('mousemove', e => { const [x, y] = canvasXY(e); inputQueue.push([1, 1, 0, x, y, e.movementX, e.movementY, mods(e)]); });
canvas.addEventListener('mouseup', e => { const [x, y] = canvasXY(e); inputQueue.push([1, 2, buttonFlag(e.button), x, y, 0, 0, mods(e)]); });
canvas.addEventListener('wheel', e => { const [x, y] = canvasXY(e); inputQueue.push([1, 3, 0, x, y, 0, Math.sign(-e.deltaY), mods(e)]); });
canvas.addEventListener('contextmenu', e => e.preventDefault());
window.addEventListener('keydown', e => inputQueue.push([2, 0, keycodeOf(e), mods(e), e.key.length === 1 ? e.key.charCodeAt(0) : 0, e.repeat ? 1 : 0, 0, 0]));
window.addEventListener('keyup', e => inputQueue.push([2, 1, keycodeOf(e), mods(e), 0, 0, 0, 0]));

// Touch support (iPad/touchscreen): the engine's RTS controls assume separate
// left (select/move) and right (attack-move/cancel) mouse buttons, which touch
// has no equivalent of. Map a quick tap to a left click, a long-press held in
// place to a right click, and a touch dragged past a small tolerance to a
// left-button drag (marquee-select), same as the mouse path above.
const TOUCH_LONGPRESS_MS = 450;
const TOUCH_MOVE_TOLERANCE = 10;
let touch = null;

// Same logical-space reasoning as canvasXY above -- no buffer-size scaling.
const touchXY = t => {
	const r = canvas.getBoundingClientRect();
	return [Math.round(t.clientX - r.left), Math.round(t.clientY - r.top)];
};

canvas.addEventListener('touchstart', e => {
	e.preventDefault();
	if (e.touches.length === 1) {
		const t = e.touches[0];
		const [x, y] = touchXY(t);
		touch = { fingers: 1, x, y, clientX: t.clientX, clientY: t.clientY, dragging: false, longPressed: false, timer: null };
		touch.timer = setTimeout(() => {
			if (!touch || touch.fingers !== 1 || touch.dragging) return;
			touch.longPressed = true;
			inputQueue.push([1, 0, buttonFlag(2), touch.x, touch.y, 0, 0, 0]);
			inputQueue.push([1, 2, buttonFlag(2), touch.x, touch.y, 0, 0, 0]);
		}, TOUCH_LONGPRESS_MS);
	} else if (e.touches.length === 2) {
		if (touch && touch.timer) clearTimeout(touch.timer);
		const [x0, y0] = touchXY(e.touches[0]);
		const [x1, y1] = touchXY(e.touches[1]);
		const cx = Math.round((x0 + x1) / 2);
		const cy = Math.round((y0 + y1) / 2);
		touch = { fingers: 2, x: cx, y: cy, dragging: true };
		inputQueue.push([1, 0, buttonFlag(2), cx, cy, 0, 0, 0]);
	}
}, { passive: false });

canvas.addEventListener('touchmove', e => {
	e.preventDefault();
	if (!touch) return;
	
	if (touch.fingers === 1 && e.touches.length === 1) {
		const t = e.touches[0];
		const [x, y] = touchXY(t);
		if (!touch.dragging && !touch.longPressed &&
			(Math.abs(t.clientX - touch.clientX) > TOUCH_MOVE_TOLERANCE || Math.abs(t.clientY - touch.clientY) > TOUCH_MOVE_TOLERANCE)) {
			clearTimeout(touch.timer);
			touch.dragging = true;
			inputQueue.push([1, 0, buttonFlag(0), touch.x, touch.y, 0, 0, 0]);
		}
		if (touch.dragging) {
			touch.x = x; touch.y = y;
			inputQueue.push([1, 1, 0, x, y, 0, 0, 0]);
		}
	} else if (touch.fingers === 2 && e.touches.length === 2) {
		const [x0, y0] = touchXY(e.touches[0]);
		const [x1, y1] = touchXY(e.touches[1]);
		const cx = Math.round((x0 + x1) / 2);
		const cy = Math.round((y0 + y1) / 2);
		touch.x = cx; touch.y = cy;
		inputQueue.push([1, 1, 0, cx, cy, 0, 0, 0]);
	}
}, { passive: false });

canvas.addEventListener('touchend', e => {
	e.preventDefault();
	if (!touch) return;
	
	if (touch.fingers === 1) {
		clearTimeout(touch.timer);
		if (touch.dragging)
			inputQueue.push([1, 2, buttonFlag(0), touch.x, touch.y, 0, 0, 0]);
		else if (!touch.longPressed) {
			inputQueue.push([1, 0, buttonFlag(0), touch.x, touch.y, 0, 0, 0]);
			inputQueue.push([1, 2, buttonFlag(0), touch.x, touch.y, 0, 0, 0]);
		}
	} else if (touch.fingers === 2) {
		inputQueue.push([1, 2, buttonFlag(2), touch.x, touch.y, 0, 0, 0]);
	}
	
	if (e.touches.length === 0) {
		touch = null;
	}
}, { passive: false });

canvas.addEventListener('touchcancel', () => {
	if (touch && touch.timer) clearTimeout(touch.timer);
	if (touch && touch.fingers === 2) {
		inputQueue.push([1, 2, buttonFlag(2), touch.x, touch.y, 0, 0, 0]);
	}
	touch = null;
}, { passive: false });

try {
	const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet.create();
	setModuleImports('webgl.js', webgl);
	log('runtime created; running probe…');

	// runMain (NOT dotnet.run()): keeps the runtime alive after Main returns,
	// so requestAnimationFrame can keep calling the [JSExport] frame hook.
	await runMain(getConfig().mainAssemblyName, []);
	log('probe main finished — starting rAF frame loop (W3b)…');

	// Phase W3b: the browser owns the frame — requestAnimationFrame calls INTO
	// managed code each frame until FrameLoop.OnFrame returns false.
	const exports = await getAssemblyExports(getConfig().mainAssemblyName);

	// Autoplay toggle. ToggleAutoplay returns the resulting state, and returns
	// false without doing anything when there's no local player yet (menu,
	// lobby, spectator) -- so the label always reflects what actually happened
	// rather than what was requested.
	const autoplayEl = document.getElementById('autoplay');
	autoplayEl.addEventListener('click', () => {
		let on = false;
		try {
			on = exports.OpenRA.WasmProbe.GameLoop.ToggleAutoplay();
		} catch (err) {
			console.error('[play] autoplay toggle failed:', err);
		}

		autoplayEl.textContent = on ? 'AI: ON' : 'AI: OFF';
		autoplayEl.classList.toggle('on', on);
	});
	// ?autoplay=1 arms it for automated runs: the local player only exists once
	// a match starts, so keep retrying until the handover actually takes.
	if (new URLSearchParams(location.search).has('autoplay')) {
		const arm = setInterval(() => {
			let on = false;
			try {
				on = exports.OpenRA.WasmProbe.GameLoop.ToggleAutoplay();
			} catch { /* world not up yet */ }

			if (on) {
				clearInterval(arm);
				autoplayEl.textContent = 'AI: ON';
				autoplayEl.classList.add('on');
			}
		}, 2000);
	}

	// Terrain toggle: cycles Earth -> Lunar -> Mars -> Earth. This is a full
	// page reload with a different ?terrain= value, not a live hot-swap --
	// OpenRA doesn't support changing a running map's tileset.
	const terrainLabels = { earth: 'Terrain: Earth', lunar: 'Terrain: Lunar', mars: 'Terrain: Mars' };
	const terrainNext = { earth: 'lunar', lunar: 'mars', mars: 'earth' };
	const terrainEl = document.getElementById('terrain');
	terrainEl.textContent = terrainLabels[terrainMode];
	terrainEl.addEventListener('click', () => {
		const url = new URL(location.href);
		const next = terrainNext[terrainMode];
		if (next === 'earth')
			url.searchParams.delete('terrain');
		else
			url.searchParams.set('terrain', next);
		location.assign(url);
	});

	// Free Build toggle: keeps cash topped up every frame so production is
	// effectively free. ToggleFreeBuild returns false (does nothing) until a
	// local player exists, same "no-op until the match is up" pattern as
	// the autoplay toggle above.
	const freebuildEl = document.getElementById('freebuild');
	freebuildEl.addEventListener('click', () => {
		let on = false;
		try {
			on = exports.OpenRA.WasmProbe.GameLoop.ToggleFreeBuild();
		} catch (err) {
			console.error('[play] free build toggle failed:', err);
		}

		freebuildEl.textContent = on ? 'Free Build: ON' : 'Free Build: OFF';
		freebuildEl.classList.toggle('on', on);
	});

	// League Table logic
	const leagueOverlay = document.getElementById('league-table');
	const leagueCloseBtn = document.getElementById('league-close');
	if (leagueCloseBtn) {
		leagueCloseBtn.addEventListener('click', () => {
			leagueOverlay.style.display = 'none';
		});
	}

	const recordMatch = (stats) => {
		try {
			let history = JSON.parse(localStorage.getItem('lunar_red_alert_league') || '[]');
			stats.Score = stats.KillsCost + (stats.BuildingsKilled * 500) + stats.ArmyValue;
			history.push(stats);
			history.sort((a, b) => b.Score - a.Score);
			localStorage.setItem('lunar_red_alert_league', JSON.stringify(history));
			
			const tableBody = document.querySelector('#league-stats tbody');
			if (tableBody) {
				tableBody.innerHTML = '';
				history.forEach(row => {
					const tr = document.createElement('tr');
					const stateClass = row.WinState === 'Won' ? 'won' : (row.WinState === 'Lost' ? 'lost' : '');
					tr.innerHTML = `
						<td class="${stateClass}" style="text-align:left">${row.WinState}</td>
						<td>${row.Score.toLocaleString()}</td>
						<td>${row.UnitsKilled}</td>
						<td>${row.UnitsDead}</td>
						<td>${row.BuildingsKilled}</td>
						<td>${row.BuildingsDead}</td>
					`;
					tableBody.appendChild(tr);
				});
				leagueOverlay.style.display = 'flex';
			}
		} catch(e) {
			console.error("League table failed:", e);
		}
	};

	// Phase W4c: after the probe frame counter, hand the browser's frame to
	// the LIVE game loop — Game.PerformBrowserFrame per rAF, indefinitely
	// (the gate stops after its target frames; a real deployment never stops).
	const startGameLoop = () => {
		if (!exports.OpenRA.WasmProbe.GameLoop.IsReady()) {
			log('game loop not ready (menu boot incomplete) — skipping live loop');
			return;
		}

		log('starting LIVE game loop (Game.PerformBrowserFrame per rAF)…');
		
		let lastStats = null;
		let matchRecorded = false;
		// Throttle JSExport calls
		let frameCounter = 0;

		const onGameFrame = ts => {
			try {
				if (exports.OpenRA.WasmProbe.GameLoop.OnFrame(ts)) {
					frameCounter++;
					if (frameCounter % 30 === 0) { // Polling every ~30 frames
						const statsJson = exports.OpenRA.WasmProbe.GameLoop.GetEndGameStats();
						if (statsJson) {
							lastStats = JSON.parse(statsJson);
							if (lastStats.WinState !== "Undefined" && !matchRecorded) {
								recordMatch(lastStats);
								matchRecorded = true;
							}
						} else if (lastStats && !matchRecorded) {
							lastStats.WinState = "Quit";
							recordMatch(lastStats);
							lastStats = null;
						} else if (matchRecorded) {
							lastStats = null;
							matchRecorded = false;
						}
					}

					requestAnimationFrame(onGameFrame);
				}
				else {
					log('live loop gate complete — see console for [probe] W4c line');
				}
			} catch (err) {
				console.error('[probe] FAILED in live game loop:', err);
				log('FAILED: ' + err);
			}
		};
		requestAnimationFrame(onGameFrame);
	};

	if (bootMode === 'play') {
		startGameLoop();
	} else {
		const onFrame = ts => {
			try {
				if (exports.OpenRA.WasmProbe.FrameLoop.OnFrame(ts))
					requestAnimationFrame(onFrame);
				else {
					log('frame loop complete — see console for [probe] W3b line');
					startGameLoop();
				}
			} catch (err) {
				console.error('[probe] FAILED in frame loop:', err);
				log('FAILED: ' + err);
			}
		};
		requestAnimationFrame(onFrame);
	}
} catch (err) {
	console.error('[probe] FAILED:', err);
	log('FAILED: ' + err);
}
