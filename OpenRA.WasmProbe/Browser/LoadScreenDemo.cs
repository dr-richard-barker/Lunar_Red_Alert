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
	// Phase W3i-b milestone: the game's REAL load screen rendered in-browser.
	// This exercises the full sprite pipeline the menu needs — binary assets
	// (uibits PNG + ttf fonts) staged into MEMFS, Renderer.InitializeFonts
	// through Canvas2DFont, Sound over the silent engine, then the manifest's
	// own ILoadScreen (LogoStripeLoadScreen) Init + Display: sheet building,
	// texture upload, sprite batching, EndFrame — the desktop draw path.
	// Runs LAST so the CI screenshot captures the actual load screen.
	internal static class LoadScreenDemo
	{
		public static void Run()
		{
			var modData = Game.ModData;

			try
			{
				Game.Sound = new Sound(RendererDemo.CreatedPlatform, Game.Settings.Sound);
				Console.WriteLine("[probe] step: Game.Sound up (silent engine)");

				Game.Renderer.InitializeFonts(modData);
				Console.WriteLine("[probe] step: Renderer fonts initialized via Canvas2DFont");
			}
			catch (Exception e)
			{
				Console.WriteLine($"[probe] STEP-FAIL Sound/fonts: {e}");
				throw;
			}

			try
			{
				var loadScreen = modData.ObjectCreator.CreateObject<ILoadScreen>(modData.Manifest.LoadScreen.Value);
				loadScreen.Init(modData.Manifest, modData.DefaultFileSystem);
				loadScreen.Display();
				Console.WriteLine($"[probe] W3i-b SUCCESS: real load screen ({modData.Manifest.LoadScreen.Value}) rendered in-browser via the engine sprite pipeline");
			}
			catch (Exception e)
			{
				Console.WriteLine($"[probe] STEP-FAIL load screen: {e}");
				throw;
			}
		}
	}
}
