using System;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	public class EnclosedBounds
	{
		public int MinX = int.MaxValue;
		public int MaxX = int.MinValue;
		public int MinY = int.MaxValue;
		public int MaxY = int.MinValue;

		public EnclosedBounds()
		{
		}

		public EnclosedBounds(int x, int y)
		{
			MinX = MaxX = x;
			MinY = MaxY = y;
		}

		public bool IsInvalid => MinX == int.MaxValue;

		public void Shift(int x, int y)
		{
			MinX += x;
			MaxX += x;
			MinY += y;
			MaxY += y;
		}

		public EnclosedBounds(int minX, int minY, int maxX, int maxY)
		{
			Set(minX, minY, maxX, maxY);
		}

		public void Set(int minX, int minY, int maxX, int maxY)
		{
			MinX = minX;
			MinY = minY;
			MaxX = maxX;
			MaxY = maxY;
		}

		public EnclosedBounds Enclose(EnclosedBounds b)
		{
			MinX = MinX < b.MinX ? MinX : b.MinX;
			MaxX = MaxX > b.MaxX ? MaxX : b.MaxX;
			MinY = MinY < b.MinY ? MinY : b.MinY;
			MaxY = MaxY > b.MaxY ? MaxY : b.MaxY;

			return this;
		}

		public EnclosedBounds CopyTo(EnclosedBounds b)
		{
			b.MinX = MinX;
			b.MinY = MinY;
			b.MaxX = MaxX;
			b.MaxY = MaxY;
			return b;
		}

		public int Width => MaxX - MinX;

		public int Height => MaxY - MinY;

		public Rect Rect => new Rect(MinX, MinY, Width, Height);
	}
}