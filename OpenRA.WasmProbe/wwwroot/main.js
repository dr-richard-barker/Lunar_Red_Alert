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
};

try {
	const { setModuleImports } = await dotnet.create();
	setModuleImports('webgl.js', webgl);
	log('runtime created; running probe…');
	await dotnet.run();
	log('probe finished — see console for [probe] lines');
} catch (err) {
	console.error('[probe] FAILED:', err);
	log('FAILED: ' + err);
}
