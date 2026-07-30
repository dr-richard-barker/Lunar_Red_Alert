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

using System.Collections.Generic;

namespace OpenRA.Network
{
	// A same-process, socket-free byte pipe standing in for a TCP loopback
	// connection between a local Server and its one local client. Used when
	// running in browser-wasm, where there is no raw socket API at all --
	// even a singleplayer skirmish normally connects to its own local server
	// over real loopback TCP so the same client/server code path serves both
	// single- and multiplayer.
	//
	// This only replaces the transport: both sides keep using their existing
	// wire-level packet framing and OrderIO serialization exactly as-is, just
	// reading/writing these queues instead of a socket. Single-threaded by
	// design (both ends are pumped from the same browser frame loop), so
	// plain Queue<T> is sufficient -- no concurrency support needed.
	public sealed class LocalTransport
	{
		public readonly Queue<byte[]> ToServer = new();
		public readonly Queue<byte[]> ToClient = new();
	}
}
