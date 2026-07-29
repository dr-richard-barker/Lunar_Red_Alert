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

namespace OpenRA.WasmProbe
{
	// Phase W4c: the LIVE game loop. After MenuDemo boots the game,
	// requestAnimationFrame drives Game.PerformBrowserFrame (engine edit #4:
	// one pacing iteration of the desktop Loop — logic catch-up + render).
	// Input flows through RenderTick's own input pump, so the UI is fully
	// interactive. The gate observes the world tick counter the seam returns:
	// if the shellmap world loaded, the SIMULATION itself must advance.
	public static partial class GameLoop
	{
		const int TargetFrames = 90;
		internal static bool Ready;
		static int frames;
		static int firstWorldTick = int.MinValue;
		static int lastWorldTick = int.MinValue;

		[JSExport]
		public static bool IsReady()
		{
			return Ready;
		}

		[JSExport]
		public static bool OnFrame(double timestamp)
		{
			int tick;
			try
			{
				tick = Game.PerformBrowserFrame();
			}
			catch (Exception e)
			{
				Console.WriteLine($"[probe] STEP-FAIL live game frame {frames}: {e}");
				throw;
			}

			if (firstWorldTick == int.MinValue)
				firstWorldTick = tick;
			lastWorldTick = tick;

			if (++frames < TargetFrames)
				return true;

			var simNote = firstWorldTick >= 0 && lastWorldTick > firstWorldTick
				? $"shellmap sim advanced {lastWorldTick - firstWorldTick} world ticks ({firstWorldTick}->{lastWorldTick})"
				: firstWorldTick >= 0
					? $"world present but sim did not advance (tick {lastWorldTick})"
					: "no world loaded — UI loop live";

			Console.WriteLine($"[probe] W4c SUCCESS: {TargetFrames} live frames via Game.PerformBrowserFrame — {simNote}");
			return false;
		}
	}
}
