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

using System.Runtime.InteropServices.JavaScript;

namespace OpenRA.Platforms.Browser
{
	// WebGL2 interop for the browser platform layer (Phase W3c). Same design
	// as the probe's WebGL class: int handles into a JS-side table, primitive
	// marshalling only. JS half lives in wwwroot/main.js ('webgl.js' module).
	internal static partial class GL
	{
		[JSImport("init", "webgl.js")]
		internal static partial int Init(int width, int height);

		[JSImport("getCanvasSize", "webgl.js")]
		internal static partial int[] GetCanvasSize();

		[JSImport("viewport", "webgl.js")]
		internal static partial void Viewport(int x, int y, int width, int height);

		[JSImport("clearColor", "webgl.js")]
		internal static partial void ClearColor(double r, double g, double b, double a);

		[JSImport("clearAll", "webgl.js")]
		internal static partial void ClearAll();

		[JSImport("clearDepth", "webgl.js")]
		internal static partial void ClearDepth();

		[JSImport("depthEnable", "webgl.js")]
		internal static partial void DepthEnable();

		[JSImport("depthDisable", "webgl.js")]
		internal static partial void DepthDisable();

		[JSImport("scissorEnable", "webgl.js")]
		internal static partial void ScissorEnable(int x, int y, int width, int height);

		[JSImport("scissorDisable", "webgl.js")]
		internal static partial void ScissorDisable();

		[JSImport("blendMode", "webgl.js")]
		internal static partial void BlendMode(int mode);

		[JSImport("createBuffer", "webgl.js")]
		internal static partial int CreateBuffer();

		[JSImport("bindArrayBuffer", "webgl.js")]
		internal static partial void BindArrayBuffer(int buffer);

		[JSImport("bufferDataSize", "webgl.js")]
		internal static partial void BufferDataSize(int sizeInBytes, bool dynamic);

		[JSImport("bufferDataBytes", "webgl.js")]
		internal static partial void BufferDataBytes(byte[] data, bool dynamic);

		[JSImport("bufferSubDataBytes", "webgl.js")]
		internal static partial void BufferSubDataBytes(int byteOffset, byte[] data);

		[JSImport("bindElementBuffer", "webgl.js")]
		internal static partial void BindElementBuffer(int buffer);

		[JSImport("elementBufferData", "webgl.js")]
		internal static partial void ElementBufferData(int[] indices);

		[JSImport("compileProgram", "webgl.js")]
		internal static partial int CompileProgram(string vertexSource, string fragmentSource);

		[JSImport("useProgram", "webgl.js")]
		internal static partial void UseProgram(int program);

		[JSImport("attribPointer", "webgl.js")]
		internal static partial void AttribPointer(int program, string name, int components, int glType, int stride, int offset);

		[JSImport("attribIPointer", "webgl.js")]
		internal static partial void AttribIPointer(int program, string name, int components, int glType, int stride, int offset);

		[JSImport("getUniform", "webgl.js")]
		internal static partial int GetUniform(int program, string name);

		[JSImport("uniform1i", "webgl.js")]
		internal static partial void Uniform1i(int location, int value);

		[JSImport("uniform1f", "webgl.js")]
		internal static partial void Uniform1f(int location, double value);

		[JSImport("uniform2f", "webgl.js")]
		internal static partial void Uniform2f(int location, double x, double y);

		[JSImport("uniform3f", "webgl.js")]
		internal static partial void Uniform3f(int location, double x, double y, double z);

		[JSImport("uniform1fv", "webgl.js")]
		internal static partial void Uniform1fv(int location, double[] values);

		[JSImport("uniformMatrix4fv", "webgl.js")]
		internal static partial void UniformMatrix4fv(int location, double[] values);

		[JSImport("activeTexture", "webgl.js")]
		internal static partial void ActiveTexture(int unit);

		[JSImport("createTexture", "webgl.js")]
		internal static partial int CreateTexture();

		[JSImport("bindTexture", "webgl.js")]
		internal static partial void BindTexture(int texture);

		[JSImport("texImage2D", "webgl.js")]
		internal static partial void TexImage2D(int width, int height, byte[] rgbaPixels);

		[JSImport("texFilter", "webgl.js")]
		internal static partial void TexFilter(bool linear);

		[JSImport("createFramebufferTex", "webgl.js")]
		internal static partial int[] CreateFramebufferTex(int width, int height);

		[JSImport("bindFramebuffer", "webgl.js")]
		internal static partial void BindFramebuffer(int framebuffer);

		[JSImport("drawArraysMode", "webgl.js")]
		internal static partial void DrawArraysMode(int mode, int first, int count);

		[JSImport("drawElementsBytes", "webgl.js")]
		internal static partial void DrawElementsBytes(int count, int byteOffset);

		[JSImport("readPixel", "webgl.js")]
		internal static partial int[] ReadPixel(int x, int y);

		[JSImport("getError", "webgl.js")]
		internal static partial int GetError();
	}
}
