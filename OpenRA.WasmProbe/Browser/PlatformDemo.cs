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
using System.Runtime.InteropServices;
using OpenRA.Graphics;
using OpenRA.Platforms.Browser;
using OpenRA.Primitives;

namespace OpenRA.WasmProbe
{
	// Phase W3c milestone: render through OpenRA's OWN platform contracts —
	// IPlatform -> IPlatformWindow -> IGraphicsContext -> IShader/IVertexBuffer/
	// ITexture -> DrawPrimitives — with the frame pixel-verified from C#.
	// This is the exact call shape the engine's Renderer uses on desktop.
	internal static class PlatformDemo
	{
		[StructLayout(LayoutKind.Sequential)]
		struct ProbeVertex(float x, float y, float u, float v)
		{
			public float X = x, Y = y, U = u, V = v;
		}

		sealed class ProbeShaderBindings : IShaderBindings
		{
			public string VertexShaderName => "probe";
			public string FragmentShaderName => "probe";
			public int Stride => 16;

			public string VertexShaderCode =>
				"#version {VERSION}\n" +
				"in vec2 aPos; in vec2 aUV; out vec2 vUV;\n" +
				"void main() { gl_Position = vec4(aPos, 0.0, 1.0); vUV = aUV; }";

			public string FragmentShaderCode =>
				"#version {VERSION}\n" +
				"precision mediump float;\n" +
				"in vec2 vUV; out vec4 fragColor; uniform sampler2D uTex;\n" +
				"void main() { fragColor = texture(uTex, vUV); }";

			public ShaderVertexAttribute[] Attributes { get; } =
			[
				new("aPos", ShaderVertexAttributeType.Float, 2, 0),
				new("aUV", ShaderVertexAttributeType.Float, 2, 8),
			];
		}

		public static void Run()
		{
			IPlatform platform = new BrowserPlatform();
			using var window = platform.CreateWindow(
				new Size(256, 256), WindowMode.Windowed, 1f, 8192, 8192, 0, GLProfile.Embedded);

			var context = window.Context;
			Console.WriteLine($"[probe] engine platform up: {window.GLProfile} / {context.GLVersion}");

			// Full-screen quad as two triangles, through the engine contracts.
			var vertices = context.CreateVertices<ProbeVertex>(6);
			vertices[0] = new ProbeVertex(-1, -1, 0, 0);
			vertices[1] = new ProbeVertex(1, -1, 1, 0);
			vertices[2] = new ProbeVertex(-1, 1, 0, 1);
			vertices[3] = new ProbeVertex(-1, 1, 0, 1);
			vertices[4] = new ProbeVertex(1, -1, 1, 0);
			vertices[5] = new ProbeVertex(1, 1, 1, 1);

			using var vertexBuffer = context.CreateVertexBuffer(vertices);

			// ITexture.SetData expects BGRA32 bytes (matching Sheet.cs's packing
			// convention and the desktop platform's GL_BGRA upload) -- these four
			// texels are byte-order-swapped from their intended magenta/yellow/
			// cyan/grey so the quadrant assertions below check genuine rendered
			// colour, not raw upload bytes.
			var texture = context.CreateTexture();
			texture.SetData(
			[
				255, 0, 255, 255, 0, 255, 255, 255,
				255, 255, 0, 255, 64, 64, 64, 255,
			], 2, 2);

			var shader = context.CreateShader(new ProbeShaderBindings());
			shader.SetTexture("uTex", texture);

			context.Clear();
			vertexBuffer.Bind();
			shader.Bind();
			shader.PrepareRender();
			context.SetBlendMode(BlendMode.None);
			context.DrawPrimitives(PrimitiveType.TriangleList, 0, 6);

			// Quadrants (readPixels origin bottom-left): magenta, yellow / cyan, grey.
			AssertPixel(64, 64, 255, 0, 255, "bottom-left=magenta");
			AssertPixel(192, 64, 255, 255, 0, "bottom-right=yellow");
			AssertPixel(64, 192, 0, 255, 255, "top-left=cyan");
			AssertPixel(192, 192, 64, 64, 64, "top-right=grey");

			Console.WriteLine("[probe] W3c SUCCESS: frame drawn via OpenRA IPlatform/IGraphicsContext contracts, 4 pixels verified");
		}

		static void AssertPixel(int x, int y, int r, int g, int b, string what)
		{
			var p = WebGL.ReadPixel(x, y);
			if (p[0] != r || p[1] != g || p[2] != b)
				throw new InvalidOperationException($"Platform pixel check failed ({what}): got [{p[0]},{p[1]},{p[2]}] expected [{r},{g},{b}]");
		}
	}
}
