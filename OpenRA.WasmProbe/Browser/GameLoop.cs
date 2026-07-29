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
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.WasmProbe
{
	// Phase W4c: the LIVE game loop — requestAnimationFrame drives
	// Game.PerformBrowserFrame (engine edit #4) after MenuDemo boots.
	// Phase W4d: a unit in the live world obeys a movement command — the
	// activity -> pathfinding -> locomotion stack end to end. The command is
	// queued directly on the actor (the shellmap's local client is a
	// spectator, so lobby-level order ownership doesn't apply; the scripted
	// AI exercises the order pipeline itself every tick).
	public static partial class GameLoop
	{
		const int LiveFrames = 90;
		const int CommandFrames = 150;

		internal static bool Ready;

		// W5 play mode: no gates, no stop — the engine owns the frame for good.
		internal static bool PlayForever;
		static int frames;
		static int firstWorldTick = int.MinValue;
		static int lastWorldTick = int.MinValue;

		static Actor commanded;
		static WPos commandStart;
		static bool commandIssued;

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

			// Play mode: no gates — keep the engine on the browser's frame.
			if (PlayForever)
				return true;

			if (firstWorldTick == int.MinValue)
				firstWorldTick = tick;
			lastWorldTick = tick;
			frames++;

			// Phase W4c: prove the loop is alive.
			if (frames == LiveFrames)
			{
				var simNote = firstWorldTick >= 0 && lastWorldTick > firstWorldTick
					? $"shellmap sim advanced {lastWorldTick - firstWorldTick} world ticks ({firstWorldTick}->{lastWorldTick})"
					: firstWorldTick >= 0
						? $"world present but sim did not advance (tick {lastWorldTick})"
						: "no world loaded — UI loop live";

				Console.WriteLine($"[probe] W4c SUCCESS: {LiveFrames} live frames via Game.PerformBrowserFrame — {simNote}");

				// Phase W4d setup: command a mobile unit in the live world.
				var world = Game.ActiveWorld;
				if (world == null)
				{
					Console.WriteLine("[probe] W4d skipped: no active world");
					return false;
				}

				commanded = world.Actors.FirstOrDefault(a =>
					a.IsInWorld && !a.IsDead && a.Info.HasTraitInfo<MobileInfo>());
				if (commanded == null)
				{
					Console.WriteLine("[probe] W4d skipped: no mobile actor in the world");
					return false;
				}

				commandStart = commanded.CenterPosition;
				var destination = world.Map.CellContaining(commandStart) + new CVec(5, 0);
				try
				{
					commanded.QueueActivity(false, new Move(commanded, destination));
					commandIssued = true;
					Console.WriteLine($"[probe] step: commanded '{commanded.Info.Name}' ({commanded.ActorID}) to move 5 cells east from {commandStart}");
				}
				catch (Exception e)
				{
					Console.WriteLine($"[probe] STEP-FAIL issuing Move: {e}");
					throw;
				}

				return true;
			}

			// Phase W4d verdict: did the unit obey?
			if (commandIssued && frames >= LiveFrames + CommandFrames)
			{
				var displacement = (commanded.CenterPosition - commandStart).Length;
				if (commanded.IsDead)
					Console.WriteLine("[probe] W4d note: commanded unit died mid-move (battle casualties are canon) — treating as inconclusive");
				else if (displacement > 256)
					Console.WriteLine($"[probe] W4d SUCCESS: unit '{commanded.Info.Name}' obeyed the Move command — displaced {displacement} world units through pathfinding/locomotion in-browser");
				else
					throw new InvalidOperationException($"Unit did not move (displacement {displacement} after {CommandFrames} frames)");

				return false;
			}

			return true;
		}
	}
}
