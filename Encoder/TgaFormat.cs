using System;
using System.IO;


namespace SpatialClusteringEncoder
{

	static class TgaFormat
	{
		public static bool Save(string fileName, Color32[] pixels, bool useAlpha, int width, int height)
		{
			using (FileStream stream = File.OpenWrite(fileName))
			{
				using (BinaryWriter writer = new BinaryWriter(stream))
				{
					writer.Write((byte)0);
					writer.Write((byte)0);
					writer.Write((byte)2);
					writer.Write((short)0);
					writer.Write((short)0);
					writer.Write((byte)0);
					writer.Write((short)0);
					writer.Write((short)0);
					writer.Write((short)width);
					writer.Write((short)height);

					if (useAlpha)
					{
						writer.Write((byte)32);
					}
					else
					{
						writer.Write((byte)24);
					}
					writer.Write((byte)32);

					int pixelCount = width * height;


					if (useAlpha)
					{
						byte[] data = new byte[pixelCount * 4];
						for (int i = 0; i < pixelCount; i++)
						{
							data[i * 4 + 0] = pixels[i].b;
							data[i * 4 + 1] = pixels[i].g;
							data[i * 4 + 2] = pixels[i].r;
							data[i * 4 + 3] = pixels[i].a;
						}
						writer.Write(data);
					} else
					{
						byte[] data = new byte[pixelCount * 3];
						for (int i = 0; i < pixelCount; i++)
						{
							data[i * 3 + 0] = pixels[i].b;
							data[i * 3 + 1] = pixels[i].g;
							data[i * 3 + 2] = pixels[i].r;
						}
						writer.Write(data);
					}
				}
			}

			return true;
		}

		static public Color32[] Load(string fileName, out int width, out int height)
		{
			width = 0;
			height = 0;

			if (!File.Exists(fileName))
			{
				Debug.LogError(string.Format("TGA: Image '{0}' not found", fileName));
				return null;
			}

			using (FileStream stream = File.OpenRead(fileName))
			{
				using (BinaryReader reader = new BinaryReader(stream))
				{
					// idlength
					reader.ReadByte();

					// colourmaptype
					reader.ReadByte();

					byte datatypecode = reader.ReadByte();
					if (datatypecode != 2)
					{
						Debug.LogError(string.Format("TGA: Image '{0}' has invalid type {1}, expected type 2.", fileName, datatypecode));
						return null;
					}

					//colourmaporigin
					reader.ReadInt16();

					//colourmaplength
					reader.ReadInt16();

					//colourmapdepth
					reader.ReadChar();

					//x_origin
					reader.ReadInt16();

					//y_origin
					reader.ReadInt16();

					width = reader.ReadInt16();
					height = reader.ReadInt16();
					byte bitsPerPixel = reader.ReadByte();

/*
					if (width != height)
					{
						Debug.LogError(string.Format("TGA: Image '{0}' must be squared. Size {1}x{2}", fileName, width, height));
						return null;
					}
 */ 

					if (!Utils.IsPowerOfTwo((UInt32)width))
					{
						Debug.LogError(string.Format("TGA: Image '{0}' dimension must be a power of 2. Size {1}x{2}", fileName, width, height));
						return null;
					}

					if (!Utils.IsPowerOfTwo((UInt32)height))
					{
						Debug.LogError(string.Format("TGA: Image '{0}' dimension must be a power of 2. Size {1}x{2}", fileName, width, height));
						return null;
					}


					byte imageDescriptor = reader.ReadByte();

					bool flipY = ((imageDescriptor & 32) == 0);

					if (bitsPerPixel != 24 && bitsPerPixel != 32)
					{
						Debug.LogError(string.Format("TGA: Image '{0}' has invalid bits per pixel {1}. Expected 24 or 32", fileName, bitsPerPixel));
						return null;
					}

					int pixelCount = width * height;
					Color32[] pixels = new Color32[pixelCount];

					if (bitsPerPixel == 32)
					{
							for (int i = 0; i < pixelCount; i++)
							{
								byte b = reader.ReadByte();
								byte g = reader.ReadByte();
								byte r = reader.ReadByte();
								byte a = reader.ReadByte();
								pixels[i] = new Color32(r, g, b, a);
							}
					}
					else
					{
						for (int i = 0; i < pixelCount; i++)
						{
							byte b = reader.ReadByte();
							byte g = reader.ReadByte();
							byte r = reader.ReadByte();
							pixels[i] = new Color32(r, g, b, 0xFF);
						}
					}


					if (flipY)
					{

						int heightDiv2 = height / 2;
						for (int y = 0; y < heightDiv2; y++)
						{
							int dstY = (height - 1 - y);
							for (int x = 0; x < width; x++)
							{
								int srcAddr = y * width + x;
								int dstAddr = dstY * width + x;
								Utils.Swap(ref pixels[srcAddr], ref pixels[dstAddr]);
							}
						}
					}

					return pixels;
				} // using reader
			} // using stream
		}


	}
}