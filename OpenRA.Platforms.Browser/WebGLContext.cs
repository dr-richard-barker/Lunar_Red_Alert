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
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Platforms.Browser
{
	// Phase W3c: OpenRA's IGraphicsContext over WebGL2. Functional members are
	// real; members the boot path hasn't reached yet throw loudly rather than
	// pretend (see WASM-PORT-PLAN.md — no silent stubs).
	sealed class WebGLContext : IGraphicsContext
	{
		public string GLVersion => "WebGL 2.0 (GLES3)";

		public IVertexBuffer<T> CreateEmptyVertexBuffer<T>(int size) where T : struct
		{
			return new VertexBuffer<T>(size);
		}

		public IVertexBuffer<T> CreateVertexBuffer<T>(T[] data, bool dynamic = true) where T : struct
		{
			var buffer = new VertexBuffer<T>(data.Length);
			buffer.SetData(data, data.Length);
			return buffer;
		}

		public T[] CreateVertices<T>(int size) where T : struct
		{
			return new T[size];
		}

		public IIndexBuffer CreateIndexBuffer(uint[] indices)
		{
			return new IndexBuffer(indices);
		}

		public ITexture CreateTexture()
		{
			return new Texture();
		}

		public IFrameBuffer CreateFrameBuffer(Size s)
		{
			return new FrameBuffer(s, Color.FromArgb(0, 0, 0, 0));
		}

		public IFrameBuffer CreateFrameBuffer(Size s, Color clearColor)
		{
			return new FrameBuffer(s, clearColor);
		}

		public IShader CreateShader(IShaderBindings shaderBindings)
		{
			return new Shader(shaderBindings);
		}

		public void EnableScissor(int x, int y, int width, int height)
		{
			GL.ScissorEnable(x, y, width, height);
		}

		public void DisableScissor()
		{
			GL.ScissorDisable();
		}

		public void Present()
		{
			// The browser presents the canvas at the end of the rAF callback.
		}

		public void DrawPrimitives(PrimitiveType pt, int firstVertex, int numVertices)
		{
			// GL_POINTS / GL_LINES / GL_TRIANGLES.
			var mode = pt switch
			{
				PrimitiveType.PointList => 0x0000,
				PrimitiveType.LineList => 0x0001,
				_ => 0x0004,
			};
			GL.DrawArraysMode(mode, firstVertex, numVertices);
		}

		public void DrawElements(int numIndices, int offset)
		{
			GL.DrawElementsBytes(numIndices, offset * sizeof(uint));
		}

		public void Clear()
		{
			GL.ClearColor(0, 0, 0, 1);
			GL.ClearAll();
		}

		public void EnableDepthBuffer() { GL.DepthEnable(); }
		public void DisableDepthBuffer() { GL.DepthDisable(); }
		public void ClearDepthBuffer() { GL.ClearDepth(); }

		public void SetBlendMode(BlendMode mode)
		{
			GL.BlendMode((int)mode);
		}

		public void SetVSyncEnabled(bool enabled)
		{
			// rAF is inherently vsynced; nothing to do.
		}

		public void Dispose() { }
	}

	sealed class VertexBuffer<T> : IVertexBuffer<T> where T : struct
	{
		readonly int handle;

		public VertexBuffer(int size)
		{
			handle = GL.CreateBuffer();
			GL.BindArrayBuffer(handle);
			GL.BufferDataSize(size * Marshal.SizeOf<T>(), true);
		}

		public void Bind()
		{
			GL.BindArrayBuffer(handle);
		}

		public void SetData(T[] vertices, int length)
		{
			SetData(vertices, 0, 0, length);
		}

		public void SetData(ref T[] vertices, int length)
		{
			SetData(vertices, 0, 0, length);
		}

		public void SetData(T[] vertices, int offset, int start, int length)
		{
			GL.BindArrayBuffer(handle);
			var stride = Marshal.SizeOf<T>();
			var bytes = MemoryMarshal.AsBytes(vertices.AsSpan(offset, length)).ToArray();
			GL.BufferSubDataBytes(start * stride, bytes);
		}

		public void Dispose() { }
	}

	sealed class IndexBuffer : IIndexBuffer
	{
		readonly int handle;

		public IndexBuffer(uint[] indices)
		{
			handle = GL.CreateBuffer();
			GL.BindElementBuffer(handle);
			var signed = new int[indices.Length];
			for (var i = 0; i < indices.Length; i++)
				signed[i] = unchecked((int)indices[i]);
			GL.ElementBufferData(signed);
		}

		public void Bind()
		{
			GL.BindElementBuffer(handle);
		}

		public void Dispose() { }
	}

	sealed class Texture : ITexture
	{
		internal readonly int Handle;
		TextureScaleFilter scaleFilter = TextureScaleFilter.Nearest;

		public Texture()
		{
			Handle = GL.CreateTexture();
		}

		internal Texture(int existingHandle, Size size)
		{
			Handle = existingHandle;
			Size = size;
		}

		public Size Size { get; private set; }

		public TextureScaleFilter ScaleFilter
		{
			get => scaleFilter;
			set
			{
				scaleFilter = value;
				GL.BindTexture(Handle);
				GL.TexFilter(value == TextureScaleFilter.Linear);
			}
		}

		public void SetData(byte[] colors, int width, int height)
		{
			GL.BindTexture(Handle);
			GL.TexImage2D(width, height, colors);
			Size = new Size(width, height);
		}

		public void SetFloatData(float[] data, int width, int height)
		{
			GL.BindTexture(Handle);
			var values = new double[data.Length];
			for (var i = 0; i < data.Length; i++)
				values[i] = data[i];
			GL.TexImage2DFloat(width, height, values);
			Size = new Size(width, height);
		}

		public void SetDataFromReadBuffer(Rectangle rect)
		{
			GL.BindTexture(Handle);
			GL.CopyTexImage2D(rect.X, rect.Y, rect.Width, rect.Height);
			Size = new Size(rect.Width, rect.Height);
		}

		public byte[] GetData()
		{
			throw new NotImplementedException("Browser platform: texture readback not yet implemented (W3c)");
		}

		public void Dispose() { }
	}

	sealed class FrameBuffer : IFrameBuffer
	{
		readonly int handle;
		readonly Color clearColor;

		public FrameBuffer(Size size, Color clearColor)
		{
			this.clearColor = clearColor;
			var created = GL.CreateFramebufferTex(size.Width, size.Height);
			handle = created[0];
			Texture = new Texture(created[1], size);
		}

		public ITexture Texture { get; }

		public void Bind()
		{
			GL.BindFramebuffer(handle);
			GL.ClearColor(clearColor.R / 255d, clearColor.G / 255d, clearColor.B / 255d, clearColor.A / 255d);
			GL.ClearAll();
		}

		public void Unbind()
		{
			GL.BindFramebuffer(0);
		}

		public void EnableScissor(Rectangle rect)
		{
			GL.ScissorEnable(rect.X, rect.Y, rect.Width, rect.Height);
		}

		public void DisableScissor()
		{
			GL.ScissorDisable();
		}

		public void Dispose() { }
	}

	sealed class Shader : IShader
	{
		readonly int program;
		readonly IShaderBindings bindings;
		readonly Dictionary<string, int> uniformCache = [];
		readonly Dictionary<string, Texture> textures = [];

		public Shader(IShaderBindings bindings)
		{
			this.bindings = bindings;

			// Same substitution the desktop platform performs, but targeting
			// WebGL2's GLSL dialect (the engine's Embedded profile equivalent).
			var vertex = bindings.VertexShaderCode.Replace("{VERSION}", "300 es");
			var fragment = bindings.FragmentShaderCode.Replace("{VERSION}", "300 es");
			if (!fragment.Contains("precision"))
				fragment = fragment.Replace("#version 300 es", "#version 300 es\nprecision highp float;\nprecision highp int;");

			program = GL.CompileProgram(vertex, fragment);
			if (program == 0)
				throw new InvalidOperationException($"Failed to compile shader {bindings.VertexShaderName}/{bindings.FragmentShaderName} for WebGL2");
		}

		int Uniform(string name)
		{
			if (!uniformCache.TryGetValue(name, out var loc))
				uniformCache[name] = loc = GL.GetUniform(program, name);
			return loc;
		}

		public void Bind()
		{
			GL.UseProgram(program);
			foreach (var attribute in bindings.Attributes)
			{
				if (attribute.Type == Graphics.ShaderVertexAttributeType.Float)
					GL.AttribPointer(program, attribute.Name, attribute.Components, (int)attribute.Type, bindings.Stride, attribute.Offset);
				else
					GL.AttribIPointer(program, attribute.Name, attribute.Components, (int)attribute.Type, bindings.Stride, attribute.Offset);
			}
		}

		public void SetBool(string name, bool value)
		{
			GL.UseProgram(program);
			GL.Uniform1i(Uniform(name), value ? 1 : 0);
		}

		public void SetVec(string name, float x)
		{
			GL.UseProgram(program);
			GL.Uniform1f(Uniform(name), x);
		}

		public void SetVec(string name, float x, float y)
		{
			GL.UseProgram(program);
			GL.Uniform2f(Uniform(name), x, y);
		}

		public void SetVec(string name, float x, float y, float z)
		{
			GL.UseProgram(program);
			GL.Uniform3f(Uniform(name), x, y, z);
		}

		public void SetVec(string name, ReadOnlyMemory<float> vec, int length)
		{
			GL.UseProgram(program);
			var values = new double[length];
			var span = vec.Span;
			for (var i = 0; i < length; i++)
				values[i] = span[i];
			GL.Uniform1fv(Uniform(name), values);
		}

		public void SetTexture(string param, ITexture texture)
		{
			textures[param] = (Texture)texture;
		}

		public void SetMatrix(string param, float[] mtx)
		{
			GL.UseProgram(program);
			var values = new double[mtx.Length];
			for (var i = 0; i < mtx.Length; i++)
				values[i] = mtx[i];
			GL.UniformMatrix4fv(Uniform(param), values);
		}

		public void PrepareRender()
		{
			GL.UseProgram(program);
			var unit = 0;
			foreach (var pair in textures)
			{
				GL.ActiveTexture(unit);
				GL.BindTexture(pair.Value.Handle);
				GL.Uniform1i(Uniform(pair.Key), unit);
				unit++;
			}
		}
	}
}
