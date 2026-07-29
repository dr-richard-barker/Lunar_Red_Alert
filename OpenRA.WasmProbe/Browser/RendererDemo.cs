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
using OpenRA.Graphics;

namespace OpenRA.WasmProbe
{
	// Phase W3i-a milestone: the engine's REAL Renderer over BrowserPlatform.
	// Its constructor opens the window through Game.CreatePlatform's product,
	// compiles the engine's actual glsl shaders (staged into MEMFS under
	// EngineDir/glsl, {VERSION}->300 es via our WebGLContext), builds the
	// sprite renderers, and then completes a BeginUI/EndFrame cycle — the
	// same per-frame path the desktop game drives. Requires ModDataDemo
	// (Game.Settings + Game.ModData) to have run.
	internal static class RendererDemo
	{
		sealed class NullInputHandler : IInputHandler
		{
			public void ModifierKeys(Modifiers mods) { }
			public void OnKeyInput(KeyInput input) { }
			public void OnMouseInput(MouseInput input) { }
			public void OnTextInput(string text) { }
		}

		public static void Run()
		{
			var platform = Game.CreatePlatform("Browser");
			Renderer renderer;
			try
			{
				renderer = new Renderer(platform, Game.Settings.Graphics, Game.ModData.Manifest.RendererConstants.VertexBatchSize);
				Console.WriteLine($"[probe] step: engine Renderer constructed ({renderer.Resolution.Width}x{renderer.Resolution.Height}, engine shaders compiled)");
			}
			catch (Exception e)
			{
				Console.WriteLine($"[probe] STEP-FAIL Renderer ctor: {e}");
				throw;
			}

			Game.Renderer = renderer;

			try
			{
				renderer.BeginUI();
				renderer.EndFrame(new NullInputHandler());
			}
			catch (Exception e)
			{
				Console.WriteLine($"[probe] STEP-FAIL Renderer frame cycle: {e}");
				throw;
			}

			Console.WriteLine("[probe] W3i SUCCESS: engine Renderer over BrowserPlatform completed a BeginUI/EndFrame cycle with real engine shaders in-browser");
		}
	}
}
