using System;
using System.IO;


namespace SpatialClusteringEncoder
{

	static class DdsFormat
	{
		//
		// https://msdn.microsoft.com/en-us/library/windows/desktop/bb943982(v=vs.85).aspx
		//

		const int DDSD_CAPS = 0x00000001;
		const int DDSD_HEIGHT = 0x00000002;
		const int DDSD_WIDTH = 0x00000004;
		const int DDSD_PIXELFORMAT = 0x00001000;
		const int DDSD_MIPMAPCOUNT = 0x00020000;
		const int DDSD_LINEARSIZE = 0x00080000;

		const int DDPF_ALPHAPIXELS = 0x00000001;
		const int DDPF_RGB = 0x00000040;

		const int DDSCAPS_COMPLEX = 0x00000008;
		const int DDSCAPS_TEXTURE = 0x00001000;
		const int DDSCAPS_MIPMAP = 0x00400000;



		public class MipLevel
		{
			public UInt32 width = 0;
			public UInt32 height = 0;
			public Color32[] pixels = null;
		}


		public static bool Save(string fileName, MipLevel[] levels)
		{
			if (levels == null || levels.Length <= 0)
			{
				return false;
			}

			UInt32 w = levels[0].width;
			UInt32 h = levels[0].height;
			for (int i = 1; i < levels.Length; i++)
			{
				w = w >> 1;
				h = h >> 1;

				if (levels[i].width != w || levels[i].height != h)
				{
					return false;
				}
			}

			using (FileStream stream = File.OpenWrite(fileName))
			{
				using (BinaryWriter writer = new BinaryWriter(stream))
				{
					UInt32 signature = 0x20534444; // 'DDS '
					writer.Write(signature);

					UInt32 headerSize = 124;
					writer.Write(headerSize);

					UInt32 flags = DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_PIXELFORMAT | DDSD_MIPMAPCOUNT | DDSD_LINEARSIZE;
					writer.Write(flags);

					UInt32 width = levels[0].width;
					writer.Write(width);

					UInt32 height = levels[0].height;
					writer.Write(height);

					UInt32 linearSize = width * height * 4;
					writer.Write(linearSize);

					UInt32 depth = 0;
					writer.Write(depth);

					UInt32 mipMapCount = (UInt32)levels.Length;
					writer.Write(mipMapCount);

					UInt32 alphaBitDepth = 0;
					writer.Write(alphaBitDepth);

					for (int i = 0; i < 10; i++)
					{
						UInt32 reserved = 0;
						writer.Write(reserved);
					}

					//pixel format
					//---------------------------------------------------------
					UInt32 pfSize = 32;
					writer.Write(pfSize);

					UInt32 pfFlags = DDPF_RGB | DDPF_ALPHAPIXELS; //RGBA
					writer.Write(pfFlags);

					UInt32 pfFourCC = 0x0;
					writer.Write(pfFourCC);

					UInt32 pfRgbBitCount = 32;
					writer.Write(pfRgbBitCount);

					UInt32 pfRmask = 0x00FF0000;
					writer.Write(pfRmask);

					UInt32 pfGmask = 0x0000FF00;
					writer.Write(pfGmask);

					UInt32 pfBmask = 0x000000FF;
					writer.Write(pfBmask);

					UInt32 pfAmask = 0xFF000000;
					writer.Write(pfAmask);


					// caps
					//---------------------------------------------------------

					UInt32 caps = DDSCAPS_COMPLEX | DDSCAPS_MIPMAP | DDSCAPS_TEXTURE;
					writer.Write(caps);

					UInt32 caps2 = 0;
					writer.Write(caps2);

					UInt32 caps3 = 0;
					writer.Write(caps3);

					UInt32 caps4 = 0;
					writer.Write(caps4);

					UInt32 reserved2 = 0;
					writer.Write(reserved2);


					w = levels[0].width;
					h = levels[0].height;
					for (int level = 0; level < levels.Length; level++)
					{
						UInt32 pixelsCount = w * h;

						byte[] data = new byte[pixelsCount * 4];
						for (int i = 0; i < pixelsCount; i++)
						{
							data[i * 4 + 0] = levels[level].pixels[i].b;
							data[i * 4 + 1] = levels[level].pixels[i].g;
							data[i * 4 + 2] = levels[level].pixels[i].r;
							data[i * 4 + 3] = levels[level].pixels[i].a;
						}
						writer.Write(data);

						w = w >> 1;
						h = h >> 1;
					}



				}
			}

			return true;
		}

	}
}