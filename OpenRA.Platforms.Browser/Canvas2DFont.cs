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

using OpenRA.Primitives;

namespace OpenRA.Platforms.Browser
{
	// Phase W3d: IFont over the browser's Canvas2D text rasterizer. Produces
	// the same shape as the desktop FreeType path: 1 byte/pixel alpha bitmaps,
	// Offset = (bearingX, -ascent). The browser picks the face (sans-serif);
	// loading OpenRA's bundled ttf faces via FontFace is a later refinement.
	sealed class Canvas2DFont : IFont
	{
		public FontGlyph CreateGlyph(char c, int size, float deviceScale)
		{
			var px = (int)(size * deviceScale);
			if (px <= 0 || char.IsControl(c))
				return EmptyGlyph(px);

			var text = c.ToString();
			var metrics = GL.MeasureGlyph(text, px);
			var width = metrics[0];
			var height = metrics[1];

			return new FontGlyph
			{
				Offset = new int2(metrics[2], metrics[3]),
				Size = new Size(width, height),
				Advance = metrics[4] / 100f,
				Data = GL.RasterizeGlyph(text, px),
			};
		}

		static FontGlyph EmptyGlyph(int px)
		{
			return new FontGlyph
			{
				Offset = new int2(0, 0),
				Size = new Size(1, 1),
				Advance = px / 2f,
				Data = new byte[1],
			};
		}

		public void Dispose() { }
	}
}
