#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace OpenRA.WasmProbe
{
	// Phase W2: managed-side WebGL2 bindings. The JS half lives in
	// wwwroot/main.js (browser, real WebGL2) and noderun.mjs (Node, stub that
	// reports no DOM). Handles are ints into a JS-side table, so no JSObject
	// marshalling is needed. This is the seed of OpenRA.Platforms.Browser's
	// IGraphicsContext implementation (see WASM-PORT-PLAN.md).
	internal static partial class WebGL
	{
		[JSImport("getPersistedData", "webgl.js")]
		internal static partial string GetPersistedData(string key);

		[JSImport("setPersistedData", "webgl.js")]
		internal static partial void SetPersistedData(string key, string value);

		[JSImport("hasDocument", "webgl.js")]
		internal static partial bool HasDocument();

		// Phase W3: async text fetch. Browser host implements this over fetch();
		// the Node host reads from disk. This is the seed of the VFS-over-HTTP
		// layer that will stage mod packages before engine init.
		[JSImport("fetchText", "webgl.js")]
		internal static partial Task<string> FetchText(string url);

		// Phase W3d gate helper: dispatches real DOM mouse/key events.
		[JSImport("synthesizeTestInput", "webgl.js")]
		internal static partial void SynthesizeTestInput();

		// Phase W3i-b: binary fetch via base64 (fonts, PNGs, later .mix).
		[JSImport("fetchBase64", "webgl.js")]
		internal static partial Task<string> FetchBase64(string url);

		// Phase W5: "probe" (CI gate ladder) or "play" (deployed page boot).
		[JSImport("getBootMode", "webgl.js")]
		internal static partial string GetBootMode();

		internal static async Task<byte[]> FetchBinary(string url)
		{
			return Convert.FromBase64String(await FetchBase64(url));
		}

		[JSImport("init", "webgl.js")]
		internal static partial int Init(int width, int height);

		[JSImport("clearColor", "webgl.js")]
		internal static partial void ClearColor(double r, double g, double b, double a);

		[JSImport("clear", "webgl.js")]
		internal static partial void Clear();

		[JSImport("compileProgram", "webgl.js")]
		internal static partial int CompileProgram(string vertexSource, string fragmentSource);

		[JSImport("useProgram", "webgl.js")]
		internal static partial void UseProgram(int program);

		[JSImport("createBuffer", "webgl.js")]
		internal static partial int CreateBuffer();

		[JSImport("bindArrayBuffer", "webgl.js")]
		internal static partial void BindArrayBuffer(int buffer);

		[JSImport("bufferData", "webgl.js")]
		internal static partial void BufferData(double[] data);

		[JSImport("attrib", "webgl.js")]
		internal static partial void Attrib(int program, string name, int size, int stride, int offset);

		[JSImport("createTexture", "webgl.js")]
		internal static partial int CreateTexture();

		[JSImport("bindTexture", "webgl.js")]
		internal static partial void BindTexture(int texture);

		[JSImport("texImage2D", "webgl.js")]
		internal static partial void TexImage2D(int width, int height, byte[] rgbaPixels);

		[JSImport("drawArrays", "webgl.js")]
		internal static partial void DrawArrays(int first, int count);

		[JSImport("readPixel", "webgl.js")]
		internal static partial int[] ReadPixel(int x, int y);

		[JSImport("getError", "webgl.js")]
		internal static partial int GetError();
	}
}
