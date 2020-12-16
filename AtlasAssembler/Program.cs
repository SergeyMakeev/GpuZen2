using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AtlasAssembler
{
    class DDSFile
    {
        public uint magic;
        public uint size;
        public uint flags;
        public uint height;
        public uint width;
        public uint pitchOrLinearSize;
        public uint depth;
        public uint mipMapCount;
        public uint reserved1_00;
        public uint reserved1_01;
        public uint reserved1_02;
        public uint reserved1_03;
        public uint reserved1_04;
        public uint reserved1_05;
        public uint reserved1_06;
        public uint reserved1_07;
        public uint reserved1_08;
        public uint reserved1_09;
        public uint reserved1_10;

        // DDSPixelFormat
        public uint ppf_size;
        public uint ppf_flags;
        public uint ppf_fourCC;
        public uint ppf_rgbBitCount;
        public uint ppf_rBitMask;
        public uint ppf_gBitMask;
        public uint ppf_bBitMask;
        public uint ppf_alphaBitMask;
        // DDSPixelFormat

        public uint caps;
        public uint caps2;
        public uint caps3;
        public uint caps4;
        public uint reserved2;

        // DDSHeaderDXT10
        public uint dx10_dxgiFormat;
        public uint dx10_resourceDimension;
        public uint dx10_miscFlag;
        public uint dx10_arraySize;
        public uint dx10_miscFlags2;
        // DDSHeaderDXT10

        public byte[] payload;

        public bool compareHeaders(DDSFile other)
        {
            if (width != other.width)
                return false;

            if (height != other.height)
                return false;

            if (mipMapCount != other.mipMapCount)
                return false;

            if (depth != other.depth)
                return false;

            if (pitchOrLinearSize != other.pitchOrLinearSize)
                return false;

            if (ppf_fourCC != other.ppf_fourCC)
                return false;

            return true;
        }

        public void saveHeader(BinaryWriter writer)
        {
            writer.Write(magic);
            writer.Write(size);
            writer.Write(flags);
            writer.Write(height);
            writer.Write(width);
            writer.Write(pitchOrLinearSize);
            writer.Write(depth);
            writer.Write(mipMapCount);
            writer.Write(reserved1_00);
            writer.Write(reserved1_01);
            writer.Write(reserved1_02);
            writer.Write(reserved1_03);
            writer.Write(reserved1_04);
            writer.Write(reserved1_05);
            writer.Write(reserved1_06);
            writer.Write(reserved1_07);
            writer.Write(reserved1_08);
            writer.Write(reserved1_09);
            writer.Write(reserved1_10);

            writer.Write(ppf_size);
            writer.Write(ppf_flags);
            writer.Write(ppf_fourCC);
            writer.Write(ppf_rgbBitCount);
            writer.Write(ppf_rBitMask);
            writer.Write(ppf_gBitMask);
            writer.Write(ppf_bBitMask);
            writer.Write(ppf_alphaBitMask);

            writer.Write(caps);
            writer.Write(caps2);
            writer.Write(caps3);
            writer.Write(caps4);
            writer.Write(reserved2);

            if (ppf_fourCC == 0x30315844)
            {
                writer.Write(dx10_dxgiFormat);
                writer.Write(dx10_resourceDimension);
                writer.Write(dx10_miscFlag);
                writer.Write(dx10_arraySize);
                writer.Write(dx10_miscFlags2);
            }
        }

        public bool read(BinaryReader reader)
        {
            magic = reader.ReadUInt32();
            if (magic != 0x20534444)
            {
                return false;
            }

            size = reader.ReadUInt32();
            if (size != 124)
            {
                return false;
            }

            flags = reader.ReadUInt32();
            height = reader.ReadUInt32();
            width = reader.ReadUInt32();
            pitchOrLinearSize = reader.ReadUInt32();
            depth = reader.ReadUInt32();
            mipMapCount = reader.ReadUInt32();
            reserved1_00 = reader.ReadUInt32();
            reserved1_01 = reader.ReadUInt32();
            reserved1_02 = reader.ReadUInt32();
            reserved1_03 = reader.ReadUInt32();
            reserved1_04 = reader.ReadUInt32();
            reserved1_05 = reader.ReadUInt32();
            reserved1_06 = reader.ReadUInt32();
            reserved1_07 = reader.ReadUInt32();
            reserved1_08 = reader.ReadUInt32();
            reserved1_09 = reader.ReadUInt32();
            reserved1_10 = reader.ReadUInt32();


            ppf_size = reader.ReadUInt32();
            ppf_flags = reader.ReadUInt32();
            ppf_fourCC = reader.ReadUInt32();
            ppf_rgbBitCount = reader.ReadUInt32();
            ppf_rBitMask = reader.ReadUInt32();
            ppf_gBitMask = reader.ReadUInt32();
            ppf_bBitMask = reader.ReadUInt32();
            ppf_alphaBitMask = reader.ReadUInt32();

            caps = reader.ReadUInt32();
            caps2 = reader.ReadUInt32();
            caps3 = reader.ReadUInt32();
            caps4 = reader.ReadUInt32();
            reserved2 = reader.ReadUInt32();

            // DX10
            if (ppf_fourCC == 0x30315844)
            {
                dx10_dxgiFormat = reader.ReadUInt32();
                dx10_resourceDimension = reader.ReadUInt32();
                dx10_miscFlag = reader.ReadUInt32();
                dx10_arraySize = reader.ReadUInt32();
                dx10_miscFlags2 = reader.ReadUInt32();
            }
            else
            {
                dx10_dxgiFormat = 0;
                dx10_resourceDimension = 0;
                dx10_miscFlag = 0;
                dx10_arraySize = 0;
                dx10_miscFlags2 = 0;
            }

            // 64Mb
            int maxSize = 64 * 1024 * 1024;
            payload = reader.ReadBytes(maxSize);

            return true;
        }
    }

    class JobDesc
    {
        public string output { get; set; }
        public List<string> textures { get; set; }
    }

    class Program
    {
        static string stringifyFourCC(uint fourcc)
        {
            byte[] buf = new byte[5];
            buf[0] = (byte)(fourcc & 0xFF);
            buf[1] = (byte)(fourcc >> 8 & 0xFF);
            buf[2] = (byte)(fourcc >> 16 & 0xFF);
            buf[3] = (byte)(fourcc >> 24 & 0xFF);
            buf[4] = (byte)(0);
            return Encoding.ASCII.GetString(buf);
        }

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Atlas Assembler by Sergey Makeev");
                Console.WriteLine("");
                Console.WriteLine("Usage: AtlasAssembler job.json");
                return;
            }

            string jobFileName = args[0];
            JobDesc jobDesc;
            try
            {
                using (StreamReader reader = File.OpenText(jobFileName))
                {
                    jobDesc = JsonConvert.DeserializeObject<JobDesc>(reader.ReadToEnd());
                }
            }
            catch (IOException err)
            {
                Console.WriteLine("{0}", err.ToString());
                return;
            }

            if (jobDesc == null)
            {
                Console.WriteLine("Can't read json file {0}", jobFileName);
                return;
            }


            if (jobDesc.textures.Count == 0 || string.IsNullOrEmpty(jobDesc.output))
            {
                Console.WriteLine("Incorrect job file {0}", jobFileName);
                return;
            }


            List<DDSFile> ddsFiles = new List<DDSFile>();
            DDSFile ddsReference = null;

            for (int i = 0; i < jobDesc.textures.Count; i++)
            {
                string ddsFileName = jobDesc.textures[i];

                using (BinaryReader reader = new BinaryReader(File.Open(ddsFileName, FileMode.Open)))
                {
                    DDSFile ddsFile = new DDSFile();
                    bool res = ddsFile.read(reader);
                    if (res)
                    {
                        Console.WriteLine("{0} - {1}x{2} m:{3}, f:{4}", ddsFileName, ddsFile.width, ddsFile.height, ddsFile.mipMapCount, stringifyFourCC(ddsFile.ppf_fourCC));
                        if (ddsFile.ppf_fourCC != 0x31545844)
                        {
                            Console.WriteLine("Only DXT1 format supported - ignoring");
                            continue;
                        }

                        if (ddsReference != null)
                        {
                            if (!ddsReference.compareHeaders(ddsFile))
                            {
                                Console.WriteLine("Does not match - ignoring");
                                continue;
                            }
                        }

                        ddsFiles.Add(ddsFile);

                        if (ddsReference == null)
                        {
                            ddsReference = ddsFile;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Can't read {0} - ignoring", ddsFileName);
                    }
                }
            } // read all textures

            if (ddsReference == null)
            {
                Console.WriteLine("Nothing to save.");
                return;
            }

            // convert to DX10
            ddsReference.ppf_fourCC = 0x30315844;
            ddsReference.dx10_dxgiFormat = 0x00000047; // DXGI_FORMAT_BC1_UNORM 
            ddsReference.dx10_resourceDimension = 3; // D3D10_RESOURCE_DIMENSION_TEXTURE2D
            ddsReference.dx10_miscFlag = 0;
            ddsReference.dx10_miscFlags2 = 0;
            ddsReference.dx10_arraySize = (uint)(ddsFiles.Count);

            using (BinaryWriter writer = new BinaryWriter(File.Open(jobDesc.output, FileMode.Create)))
            {
                ddsReference.saveHeader(writer);

                for (int i = 0; i < ddsFiles.Count; i++)
                {
                    DDSFile ddsFile = ddsFiles[i];
                    writer.Write(ddsFile.payload);
                }
            }

            Console.WriteLine("Texture array saved to '{0}'", jobDesc.output);
        }
    }
}
