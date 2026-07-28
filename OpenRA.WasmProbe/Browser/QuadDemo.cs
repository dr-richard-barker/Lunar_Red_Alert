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

namespace OpenRA.WasmProbe
{
	// Phase W2 milestone: drive WebGL2 from managed code — clear, draw a
	// textured full-screen quad, then read pixels BACK from the framebuffer and
	// assert the expected quadrant colors. GLSL ES 300 matches the engine's
	// GLProfile.Embedded path, which is what OpenRA.Platforms.Browser will use.
	internal static class QuadDemo
	{
		const string VertexSource = "#version 300 es\n" +
			"in vec2 aPos; in vec2 aUV; out vec2 vUV;\n" +
			"void main() { gl_Position = vec4(aPos, 0.0, 1.0); vUV = aUV; }";

		const string FragmentSource = "#version 300 es\n" +
			"precision mediump float;\n" +
			"in vec2 vUV; out vec4 color; uniform sampler2D uTex;\n" +
			"void main() { color = texture(uTex, vUV); }";

		public static void Run()
		{
			const int W = 256, H = 256;
			if (WebGL.Init(W, H) == 0)
				throw new InvalidOperationException("WebGL2 context creation failed");

			var program = WebGL.CompileProgram(VertexSource, FragmentSource);
			if (program == 0)
				throw new InvalidOperationException("GLSL ES 300 shader compile/link failed");

			WebGL.UseProgram(program);

			// Full-screen triangle strip: x, y, u, v per vertex (stride 16 bytes).
			var buffer = WebGL.CreateBuffer();
			WebGL.BindArrayBuffer(buffer);
			WebGL.BufferData([-1, -1, 0, 0, 1, -1, 1, 0, -1, 1, 0, 1, 1, 1, 1, 1]);
			WebGL.Attrib(program, "aPos", 2, 16, 0);
			WebGL.Attrib(program, "aUV", 2, 16, 8);

			// 2x2 texture, NEAREST: texel rows from UV origin — red, green / blue, white.
			var texture = WebGL.CreateTexture();
			WebGL.BindTexture(texture);
			WebGL.TexImage2D(2, 2,
			[
				255, 0, 0, 255, 0, 255, 0, 255,
				0, 0, 255, 255, 255, 255, 255, 255,
			]);

			WebGL.ClearColor(0, 0, 0, 1);
			WebGL.Clear();
			WebGL.DrawArrays(0, 4);

			var error = WebGL.GetError();
			if (error != 0)
				throw new InvalidOperationException($"GL error after draw: 0x{error:X}");

			// readPixels origin is bottom-left; UV (0,0) maps there. Quadrants:
			AssertPixel(64, 64, 255, 0, 0, "bottom-left=red");
			AssertPixel(192, 64, 0, 255, 0, "bottom-right=green");
			AssertPixel(64, 192, 0, 0, 255, "top-left=blue");
			AssertPixel(192, 192, 255, 255, 255, "top-right=white");

			Console.WriteLine("[probe] W2 SUCCESS: WebGL2 textured quad drawn from managed code, 4 quadrant pixels verified");
		}

		static void AssertPixel(int x, int y, int r, int g, int b, string what)
		{
			var p = WebGL.ReadPixel(x, y);
			if (p[0] != r || p[1] != g || p[2] != b)
				throw new InvalidOperationException($"Pixel check failed ({what}): got [{p[0]},{p[1]},{p[2]}] expected [{r},{g},{b}]");
			Console.WriteLine($"[probe] pixel {what} OK");
		}
	}
}
