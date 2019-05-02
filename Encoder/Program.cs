using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SpatialClusteringEncoder
{
	class Program
	{
		static int Main(string[] args)
		{
			if (args.Length < 6)
			{
				Console.WriteLine("Spatial Clustering Encoder by Sergey Makeev");
                Console.WriteLine("");
                Console.WriteLine("Usage: TextureEncoder <maxMipLevel> <layersCount> <result_descriptor.xml> <result_indirection.tga> <result_weights.tga> <input_color_id.tga> [<input_layers>...] ");
                Console.WriteLine("");
                Console.WriteLine("<Parameters>");
                Console.WriteLine(" maxMipLevel - maximum mip level (default=4)");
                Console.WriteLine(" layersCount - maximum per pixel layers count (default=4)");
                Console.WriteLine("");
                Console.WriteLine("Usage example: TextureEncoder 3 4 result_indirect_desc.xml result_indirect.tga result_weights.tga color_id_map.tga [layerTop.tga ... layerBottom.tga]");
				return -1;
			}

			LayersProcessorJob jobDesc = new LayersProcessorJob();

			if (int.TryParse(args[0], out jobDesc.maxMipLevel) == false)
			{
				jobDesc.maxMipLevel = 4;
			}
		
			if (int.TryParse(args[1], out jobDesc.maxLocalLayersCount) == false)
			{
				jobDesc.maxLocalLayersCount = 4;
			}

			jobDesc.resultIndirectDescription = args[2];
			jobDesc.resultIndirectMap = args[3];
			jobDesc.resultWeightsMap = args[4];
			jobDesc.subsetsId = args[5];

			for (int i = 6; i < args.Length; i++)
			{
				jobDesc.sourceLayers.Add(args[i]);
			}

			if (!jobDesc.Validate())
			{
				Debug.LogError("Invalid parameters.");
				return -2;
			}

			jobDesc.PrintParameters();

			LayersProcessor processor = new LayersProcessor();
			processor.ExecuteJob(jobDesc);

			return 0;
		}
	}
}
