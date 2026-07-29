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
using System.Linq;
using OpenRA.Platforms.Browser;
using OpenRA.Primitives;

namespace OpenRA.WasmProbe
{
	// Phase W3d milestone: (1) real glyphs rasterized via Canvas2D behind the
	// engine's IFont contract; (2) real DOM input events pumped through
	// IPlatformWindow.PumpInput into an IInputHandler — the same call path the
	// engine's InputHandler uses each tick on desktop.
	internal static class InputFontDemo
	{
		sealed class RecordingInputHandler : IInputHandler
		{
			public readonly List<MouseInput> Mouse = [];
			public readonly List<KeyInput> Keys = [];

			public void ModifierKeys(Modifiers mods) { }
			public void OnKeyInput(KeyInput input) { Keys.Add(input); }
			public void OnMouseInput(MouseInput input) { Mouse.Add(input); }
			public void OnTextInput(string text) { }
		}

		public static void Run()
		{
			IPlatform platform = new BrowserPlatform();
			using var window = platform.CreateWindow(
				new Size(256, 256), WindowMode.Windowed, 1f, 8192, 8192, 0, GLProfile.Embedded);

			// --- Fonts: rasterize a real glyph through IFont ---
			using var font = platform.CreateFont([]);
			var glyph = font.CreateGlyph('A', 24, 1f);
			if (glyph.Size.Width < 2 || glyph.Size.Height < 2)
				throw new InvalidOperationException($"Glyph 'A' too small: {glyph.Size.Width}x{glyph.Size.Height}");
			if (glyph.Data == null || !glyph.Data.Any(b => b > 128))
				throw new InvalidOperationException("Glyph 'A' bitmap has no opaque pixels");
			if (glyph.Advance <= 0)
				throw new InvalidOperationException("Glyph 'A' has no advance");

			Console.WriteLine($"[probe] glyph 'A' rasterized: {glyph.Size.Width}x{glyph.Size.Height}, advance {glyph.Advance:F1}, offset ({glyph.Offset.X},{glyph.Offset.Y})");

			// --- Input: real DOM events -> queue -> PumpInput -> IInputHandler ---
			var handler = new RecordingInputHandler();
			WebGL.SynthesizeTestInput();
			window.PumpInput(handler);

			var down = handler.Mouse.FirstOrDefault(m => m.Event == MouseInputEvent.Down);
			if (down.Event != MouseInputEvent.Down || down.Button != MouseButton.Left)
				throw new InvalidOperationException("No left-button mouse-down was pumped");
			if (down.Location.X != 30 || down.Location.Y != 40)
				throw new InvalidOperationException($"Mouse-down at ({down.Location.X},{down.Location.Y}), expected (30,40)");
			if (!handler.Mouse.Any(m => m.Event == MouseInputEvent.Up))
				throw new InvalidOperationException("No mouse-up was pumped");

			var key = handler.Keys.FirstOrDefault(k => k.Event == KeyInputEvent.Down);
			if (key.Key != Keycode.A || key.UnicodeChar != 'a')
				throw new InvalidOperationException($"Key-down was {key.Key}/'{key.UnicodeChar}', expected A/'a'");

			Console.WriteLine($"[probe] input pumped: mouse Down/Up at (30,40) + key '{key.UnicodeChar}' via IInputHandler");
			Console.WriteLine("[probe] W3d SUCCESS: Canvas2D glyphs + DOM input through engine IFont/IInputHandler contracts");
		}
	}
}
