using System;
using System.Collections.Generic;

namespace SpatialClusteringEncoder
{

	////////////////////////////////////////////////////////////////////////////////////////////////////////////
	struct Color32
	{
		public byte r;
		public byte g;
		public byte b;
		public byte a;

		public Color32(byte _r, byte _g, byte _b, byte _a)
		{
			r = _r;
			g = _g;
			b = _b;
			a = _a;
		}

		public static Color32 transp = new Color32(0, 0, 0, 0);
		public static Color32 black = new Color32(0, 0, 0, 255);
		public static Color32 white = new Color32(255, 255, 255, 255);
		public static Color32 red = new Color32(255, 0, 0, 255);
		public static Color32 green = new Color32(0, 255, 0, 255);
		public static Color32 blue = new Color32(0, 0, 255, 255);
		public static Color32 yellow = new Color32(255, 255, 0, 255);
	}


	////////////////////////////////////////////////////////////////////////////////////////////////////////////
	struct Short2
	{
		public Int16 x;
		public Int16 y;

		public Short2(int _x, int _y)
		{
			x = (Int16)_x;
			y = (Int16)_y;
		}

		public static Short2 maxValue = new Short2(Int16.MaxValue, Int16.MaxValue);
		public static Short2 minValue = new Short2(Int16.MinValue, Int16.MinValue);
		public static Short2 zero = new Short2(0, 0);
	}



	////////////////////////////////////////////////////////////////////////////////////////////////////////////
	class CpuTexture2D
	{
		public Color32[] texels;
		public int width;
		public int height;

		public int pixelsCount
		{
			get
			{
				return width * height;
			}
		}


		public static CpuTexture2D CreateCopy(CpuTexture2D tex)
		{
			CpuTexture2D res = CreateEmpty(tex.width, tex.height);
			for (int i = 0; i < tex.pixelsCount; i++)
			{
				res.texels[i] = tex.texels[i];
			}
			return res;
		}


		public static CpuTexture2D CreateEmpty(int width, int height, Color32 clr)
		{
			CpuTexture2D res = CreateEmpty(width, height);
			Utils.SetArray<Color32>(res.texels, clr);
			return res;
		}

		public static CpuTexture2D CreateEmpty(int width, int height)
		{
			CpuTexture2D res = new CpuTexture2D();
			res.texels = new Color32[width * height];
			res.width = width;
			res.height = height;
			return res;
		}

		public static CpuTexture2D LoadFromFile(string name)
		{
			int width = 0;
			int height = 0;
			Color32[] texels = TgaFormat.Load(name, out width, out height);
			if (texels == null || width == 0 || height == 0)
			{
				return null;
			}

			CpuTexture2D res = new CpuTexture2D();
			res.texels = texels;
			res.width = width;
			res.height = height;
			return res;
		}

		public Color32 Load(int x, int y)
		{
			return texels[y * width + x];
		}

		public void Store(int x, int y, Color32 v)
		{
			texels[y * width + x] = v;
		}
	}



	////////////////////////////////////////////////////////////////////////////////////////////////////////////
	static class Mathf
	{
		public static float Clamp01(float f)
		{
			if (f < 0.0f)
			{
				f = 0.0f;
			}

			if (f > 1.0f)
			{
				f = 1.0f;
			}

			return f;
		}

	}

	////////////////////////////////////////////////////////////////////////////////////////////////////////////
	static class Debug
	{
		static void LogTimeStamp()
		{
			string s = DateTime.Now.ToString("HH:mm:ss  ");
			Console.Write(s);
			System.Diagnostics.Debug.Write(s);
		}

		public static void Log(string s)
		{
			LogTimeStamp();
			Console.WriteLine(s);
			System.Diagnostics.Debug.WriteLine(s);
		}

		public static void LogWarning(string s)
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			Log(s);
			Console.ResetColor();
		}

		public static void LogError(string s)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Log(s);
			Console.ResetColor();
		}

		public static void LogInfo(string s)
		{
			System.Diagnostics.Debug.WriteLine(s);
		}


		public static void Assert(bool condition, string message)
		{
			if (!condition)
			{
				Debug.LogError("Assert failed:" + message);
			}

			System.Diagnostics.Debug.Assert(condition, message);
		}
	}


	////////////////////////////////////////////////////////////////////////////////////////////////////////////
	static class Utils
	{
		public static UInt32 GetUInt32FromColor32(Color32 clr)
		{
			UInt32 res = (UInt32)(clr.a << 24 | clr.r << 16 | clr.g << 8 | clr.b);
			return res;
		}

		public static UInt64 GetLayerMask(int layerIndex)
		{
			return 1UL << layerIndex;
		}


		public static int BitCount(UInt64 v)
		{ // UInt64 (uint) counting :
			// Complexity : O(1) constant number of actions
			// Algorithm : 64-bit recursive reduction using SWAR
			const UInt64 MaskMult = 0x0101010101010101;
			const UInt64 mask1h = (~0UL) / 3 << 1;
			const UInt64 mask2l = (~0UL) / 5;
			const UInt64 mask4l = (~0UL) / 17;
			v -= (mask1h & v) >> 1;
			v = (v & mask2l) + ((v >> 2) & mask2l);
			v += v >> 4;
			v &= mask4l;
			// v += v >> 8;
			// v += v >> 16;
			// v += v >> 32;
			// return (ushort)(v & 0xff);
			// Replacement for the >> 8 and >> 16 AND return (v & 0xff)
			return (int)((v * MaskMult) >> 56);
		}

		public static string BitsToString(UInt64 v)
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder();

			int bitIndex = 0;
			int count = 0;
			while (v != 0)
			{
				if ((v & 0x1) != 0)
				{
					if (count > 0)
					{
						sb.Append(' ');
					}
					sb.Append(bitIndex);
					count++;
				}

				v >>= 1;
				bitIndex++;
			}

			return sb.ToString();
		}

		public static List<int> BitsToList(UInt64 v)
		{
			List<int> res = new List<int>();

			int bitIndex = 0;
			while (v != 0)
			{
				if ((v & 0x1) != 0)
				{
					res.Add(bitIndex);
				}

				v >>= 1;
				bitIndex++;
			}
			return res;
		}


		public static bool IsBoxIntersected(Short2 boundMin0, Short2 boundMax0, Short2 boundMin1, Short2 boundMax1, int expand = 0)
		{
			Debug.Assert(boundMin0.x <= boundMax0.x, "Sanity check failed");
			Debug.Assert(boundMin0.y <= boundMax0.y, "Sanity check failed");

			Debug.Assert(boundMin1.x <= boundMax1.x, "Sanity check failed");
			Debug.Assert(boundMin1.y <= boundMax1.y, "Sanity check failed");


			if (
				(boundMax1.x + expand) >= (boundMin0.x - expand) &&
				(boundMin1.x - expand) <= (boundMax0.x + expand) &&
				(boundMax1.y + expand) >= (boundMin0.y - expand) &&
				(boundMin1.y - expand) <= (boundMax0.y + expand)
				)
			{
				return true;
			}
			return false;
		}

		public static bool GetIntersectionRect(Short2 boundMin0, Short2 boundMax0, Short2 boundMin1, Short2 boundMax1, ref Short2 resMin, ref Short2 resMax, int expand = 0)
		{
			Debug.Assert(boundMin0.x <= boundMax0.x, "Sanity check failed");
			Debug.Assert(boundMin0.y <= boundMax0.y, "Sanity check failed");

			Debug.Assert(boundMin1.x <= boundMax1.x, "Sanity check failed");
			Debug.Assert(boundMin1.y <= boundMax1.y, "Sanity check failed");

			int minX = (boundMin0.x - expand);
			if ((boundMin1.x - expand) > minX)
			{
				minX = (boundMin1.x - expand);
			}

			int minY = (boundMin0.y - expand);
			if ((boundMin1.y - expand) > minY)
			{
				minY = (boundMin1.y - expand);
			}

			int maxX = (boundMax0.x + expand);
			if ((boundMax1.x + expand) < maxX)
			{
				maxX = (boundMax1.x + expand);
			}

			int maxY = (boundMax0.y + expand);
			if ((boundMax1.y + expand) < maxY)
			{
				maxY = (boundMax1.y + expand);
			}

			//no intersection
			if (minX > maxX || minY > maxY)
			{
				return false;
			}

			resMin.x = (Int16)minX;
			resMin.y = (Int16)minY;

			resMax.x = (Int16)maxX;
			resMax.y = (Int16)maxY;
			return true;
		}


		public static Int32 Max(Int32 lhs, Int32 rhs)
		{
			if (lhs > rhs)
				return lhs;

			return rhs;
		}


		public static byte Max(byte lhs, byte rhs)
		{
			if (lhs > rhs)
				return lhs;

			return rhs;
		}


		public static Int16 Max(Int16 lhs, Int16 rhs)
		{
			if (lhs > rhs)
				return lhs;

			return rhs;
		}

		public static Int16 Min(Int16 lhs, Int16 rhs)
		{
			if (lhs < rhs)
				return lhs;

			return rhs;
		}


		public static int Min(int lhs, int rhs)
		{
			if (lhs < rhs)
				return lhs;

			return rhs;
		}


		public static void SetArray<T>(T[] array, T v)
		{
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = v;
			}
		}

		public static void Swap<T>(ref T lhs, ref T rhs)
		{
			T temp;
			temp = lhs;
			lhs = rhs;
			rhs = temp;
		}

		public static bool IsPowerOfTwo(UInt32 v)
		{
			if (v == 0)
				return false;

			bool res = (v & (v - 1)) == 0;
			return res;
		}


		public static void GetWrappedCoords(ref int x, ref int y, int width, int height)
		{
			if (x < 0)
			{
				x += ((-x / width) + 1) * width;
			}

			if (y < 0)
			{
				y += ((-y / height) + 1) * height;
			}

			x = x % width;
			y = y % height;
		}

		public static bool IsClampedCoords(int x, int y, int width, int height)
		{
			if (x < 0)
			{
				return true;
			}

			if (y < 0)
			{
				return true;
			}

			if (x >= width)
			{
				return true;
			}

			if (y >= height)
			{
				return true;
			}

			return false;
		}

		public static void GetClampedCoords(ref int x, ref int y, int width, int height)
		{
			if (x < 0)
			{
				x = 0;
			}

			if (y < 0)
			{
				y = 0;
			}

			if (x >= width)
			{
				x = width - 1;
			}

			if (y >= height)
			{
				y = height - 1;
			}
		}

		public static bool IsSameRGB(Color32 c1, Color32 c2)
		{
			if (c1.r != c2.r)
				return false;

			if (c1.g != c2.g)
				return false;

			if (c1.b != c2.b)
				return false;

			return true;
		}

		public static bool IsNearRGB(Color32 c1, Color32 c2, int threshold = 6)
		{
			int dR = Math.Abs(c1.r - c2.r);
			int dG = Math.Abs(c1.g - c2.g);
			int dB = Math.Abs(c1.b - c2.b);

			if (dR > threshold || dG > threshold || dB > threshold)
			{
				return false;
			}

			return true;
		}

		public static T GetValueWrapped<T>(T[] data, int width, int height, int x, int y)
		{
			GetWrappedCoords(ref x, ref y, width, height);
			return data[y * width + x];
		}

		public static T GetValueClamped<T>(T[] data, int width, int height, int x, int y)
		{
			GetClampedCoords(ref x, ref y, width, height);
			return data[y * width + x];
		}

		public static T GetValueBorder<T>(T[] data, int width, int height, int x, int y, T border)
		{
			if (x < 0)
			{
				return border;
			}

			if (y < 0)
			{
				return border;
			}

			if (x >= width)
			{
				return border;
			}

			if (y >= height)
			{
				return border;
			}

			return data[y * width + x];
		}
	}


}