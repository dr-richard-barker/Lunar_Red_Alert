// Phase W2 browser host: boots the .NET wasm runtime and provides the
// 'webgl.js' import module backing OpenRA.WasmProbe.Browser.WebGL ([JSImport]).
// Handles are ints into a JS-side table so the managed side never touches
// JSObject marshalling. See WASM-PORT-PLAN.md.
import { dotnet } from './_framework/dotnet.js';

const logEl = document.getElementById('log');
const log = line => { logEl.textContent += '\n' + line; };

const canvas = document.getElementById('canvas');
let gl = null;
const handles = new Map();
let nextHandle = 1;
const keep = obj => { handles.set(nextHandle, obj); return nextHandle++; };

const webgl = {
	hasDocument: () => true,

	// Phase W3a: text fetch for the VFS-over-HTTP direction.
	fetchText: async url => {
		const r = await fetch(url);
		if (!r.ok)
			throw new Error(`fetch ${url} -> HTTP ${r.status}`);
		return r.text();
	},

	init: (w, h) => {
		canvas.width = w;
		canvas.height = h;
		gl = canvas.getContext('webgl2', { preserveDrawingBuffer: true });
		return gl ? 1 : 0;
	},

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

	drawArrays: (first, count) => gl.drawArrays(gl.TRIANGLE_STRIP, first, count),

	readPixel: (x, y) => {
		const px = new Uint8Array(4);
		gl.readPixels(x, y, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, px);
		return Array.from(px);
	},

	getError: () => gl.getError(),

	// --- Phase W3c: full platform-layer surface (OpenRA.Platforms.Browser) ---
	getCanvasSize: () => [canvas.width, canvas.height],
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
};

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
	const onFrame = ts => {
		try {
			if (exports.OpenRA.WasmProbe.FrameLoop.OnFrame(ts))
				requestAnimationFrame(onFrame);
			else
				log('frame loop complete — see console for [probe] W3b line');
		} catch (err) {
			console.error('[probe] FAILED in frame loop:', err);
			log('FAILED: ' + err);
		}
	};
	requestAnimationFrame(onFrame);
} catch (err) {
	console.error('[probe] FAILED:', err);
	log('FAILED: ' + err);
}
