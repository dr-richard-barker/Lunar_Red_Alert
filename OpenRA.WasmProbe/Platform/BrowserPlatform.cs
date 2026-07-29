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
using System.IO;
using OpenRA.Primitives;

namespace OpenRA.Platforms.Browser
{
	// Phase W3c: OpenRA's IPlatform for the browser. Rendering is real
	// (WebGLContext); sound is a silent engine; fonts are placeholder glyphs
	// until Canvas2D rasterization lands. Input pump arrives with W3d.
	public sealed class BrowserPlatform : IPlatform
	{
		public IPlatformWindow CreateWindow(
			Size size, WindowMode windowMode, float scaleModifier, int vertexBatchSize, int indexBatchSize, int videoDisplay, GLProfile profile)
		{
			return new BrowserWindow(size);
		}

		public ISoundEngine CreateSound(string device)
		{
			return new SilentSoundEngine();
		}

		public IFont CreateFont(byte[] data)
		{
			return new PlaceholderFont();
		}
	}

	sealed class BrowserWindow : IPlatformWindow
	{
		public IGraphicsContext Context { get; }

		public Size NativeWindowSize { get; }
		public Size EffectiveWindowSize => NativeWindowSize;
		public float NativeWindowScale => 1f;
		public float EffectiveWindowScale => 1f;
		public Size SurfaceSize => NativeWindowSize;
		public int DisplayCount => 1;
		public int CurrentDisplay => 0;
		public bool HasInputFocus => true;
		public bool IsSuspended => false;

		public GLProfile GLProfile => GLProfile.Embedded;
		public GLProfile[] SupportedGLProfiles => [GLProfile.Embedded];

#pragma warning disable CS0067
		public event Action<float, float, float, float> OnWindowScaleChanged;
#pragma warning restore CS0067

		public BrowserWindow(Size size)
		{
			NativeWindowSize = size;
			if (GL.Init(size.Width, size.Height) == 0)
				throw new InvalidOperationException("WebGL2 context creation failed");

			GL.Viewport(0, 0, size.Width, size.Height);
			Context = new WebGLContext();
		}

		public void PumpInput(IInputHandler inputHandler)
		{
			// W3d: translate browser mouse/keyboard events into engine input.
		}

		public string GetClipboardText() { return string.Empty; }
		public bool SetClipboardText(string text) { return false; }
		public bool TryOpenUrl(string url) { return false; }
		public void GrabWindowMouseFocus() { }
		public void ReleaseWindowMouseFocus() { }

		public IHardwareCursor CreateHardwareCursor(string name, Size size, byte[] data, int2 hotspot, bool pixelDouble)
		{
			return new NullHardwareCursor();
		}

		public void SetHardwareCursor(IHardwareCursor cursor) { }
		public void SetWindowTitle(string title) { }
		public void SetRelativeMouseMode(bool mode) { }
		public void SetScaleModifier(float scale) { }

		public void Dispose() { }
	}

	sealed class NullHardwareCursor : IHardwareCursor
	{
		public void Dispose() { }
	}

	// Silent audio until a WebAudio engine lands (W4): every play is a no-op
	// sound that reports itself complete.
	sealed class SilentSoundEngine : ISoundEngine
	{
		public SoundDevice[] AvailableDevices() { return [new SoundDevice(null, "Silent")]; }
		public bool Dummy => true;
		public float Volume { get; set; }

		public ISoundSource AddSoundSourceFromMemory(byte[] data, int channels, int sampleBits, int sampleRate)
		{
			return new SilentSource();
		}

		public ISound Play2D(ISoundSource sound, bool loop, bool relative, WPos pos, float volume, bool attenuateVolume)
		{
			return new SilentSound();
		}

		public ISound Play2DStream(Stream stream, int channels, int sampleBits, int sampleRate, bool loop, bool relative, WPos pos, float volume)
		{
			return new SilentSound();
		}

		public void PauseSound(ISound sound, bool paused) { }
		public void StopSound(ISound sound) { }
		public void SetAllSoundsPaused(bool paused) { }
		public void StopAllSounds() { }
		public void SetListenerPosition(WPos position) { }
		public void SetSoundVolume(float volume, ISound music, ISound video) { }
		public void SetSoundLooping(bool looping, ISound sound) { }
		public void SetSoundPosition(ISound sound, WPos position) { }
		public void Dispose() { }
	}

	sealed class SilentSource : ISoundSource
	{
		public void Dispose() { }
	}

	sealed class SilentSound : ISound
	{
		public float Volume { get; set; }
		public float SeekPosition => 0f;
		public bool Complete => true;
		public void SetPosition(WPos pos) { }
	}

	// Placeholder font: 1x1 transparent glyphs so text layout survives until
	// Canvas2D rasterization is implemented (W3d).
	sealed class PlaceholderFont : IFont
	{
		public FontGlyph CreateGlyph(char c, int size, float deviceScale)
		{
			return new FontGlyph
			{
				Offset = new int2(0, 0),
				Size = new Size(1, 1),
				Advance = size / 2f,
				Data = new byte[1],
			};
		}

		public void Dispose() { }
	}
}
