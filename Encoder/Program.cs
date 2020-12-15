using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SpatialClusteringEncoder
{
	class Program
	{
		static int Main(string[] args)
		{
			if (args.Length < 1)
			{
				Console.WriteLine("Spatial Clustering Encoder by Sergey Makeev");
				Console.WriteLine("");
				Console.WriteLine("Usage: TextureEncoder job.json");
				return -1;
			}

			string descFileName = args[0];
			LayersProcessorJob jobDesc;
			try
			{
				using (StreamReader reader = File.OpenText(descFileName))
				{
					jobDesc = JsonConvert.DeserializeObject<LayersProcessorJob>(reader.ReadToEnd());
				}
			}
			catch(IOException err)
			{
				Console.WriteLine("{0}", err.ToString());
				return -2;
			}

			if (jobDesc == null)
            {
				Console.WriteLine("Can't read json file {0}", descFileName);
				return -3;
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
