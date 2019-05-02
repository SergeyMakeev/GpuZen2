using System;
using System.IO;
using System.Collections.Generic;


namespace SpatialClusteringEncoder
{
	static class DebugUtils
	{

		public static void SaveAsSingleGraph(string fileName, Graph matGraph)
		{
			using (TextWriter writer = File.CreateText(fileName))
			{
				int subGraphsCount = matGraph.roots.Count;

				writer.WriteLine("# use http://www.webgraphviz.com/ to rendering graph");
				writer.WriteLine(string.Format("# nodes count {0}", matGraph.nodes.Length));

				writer.WriteLine("digraph {");

				writer.WriteLine("subgraph {");


				writer.WriteLine("# nodes");
				for (int nodeIndex = 0; nodeIndex < matGraph.nodes.Length; nodeIndex++)
				{
					Graph.Node node = matGraph.nodes[nodeIndex];
					int layersCount = Utils.BitCount(node.srcMasksId);
					writer.WriteLine(string.Format("{0} [style=filled]", node.nodeIndex));
				}


				writer.WriteLine("# links");
				for (int nodeIndex = 0; nodeIndex < matGraph.nodes.Length; nodeIndex++)
				{
					Graph.Node node = matGraph.nodes[nodeIndex];

					for (int edgeIndex = 0; edgeIndex < node.edges.Count; edgeIndex++)
					{
						bool isBackLink = (node.edges[edgeIndex].b == node);
						if (isBackLink)
						{
							continue;
						}

						string style = "";
						if (node.edges[edgeIndex].isBroken)
						{
							style = ", style=\"dotted\"";
						}

						Graph.Node targetNode = node.edges[edgeIndex].b;
						int areaValue = node.edges[edgeIndex].GetEdgeWeight(); ;
						writer.WriteLine(string.Format("{0} -> {1} [label={2}{3}];", node.nodeIndex, targetNode.nodeIndex, areaValue, style));
					}
				}

				writer.WriteLine("}");
				writer.WriteLine("}");
			}


		}

		public static void SaveDotGraph(string fileName, Graph matGraph)
		{
			using (TextWriter writer = File.CreateText(fileName))
			{
				int subGraphsCount = matGraph.roots.Count;

				writer.WriteLine("# use http://www.webgraphviz.com/ to rendering graph");
				writer.WriteLine(string.Format("# nodes count {0}", matGraph.nodes.Length));
				writer.WriteLine(string.Format("# subgraphs count {0}", subGraphsCount));

				writer.WriteLine("digraph {");

				writer.WriteLine("node[fontsize = 10];");
				writer.WriteLine("edge[fontsize = 8, arrowhead=\"none\"];");

				for (int subGraphIndex = 0; subGraphIndex < subGraphsCount; subGraphIndex++)
				{
					writer.WriteLine("subgraph {");

					Graph.Node root = matGraph.roots[subGraphIndex];
					Debug.Assert(root.connectedComponentId == subGraphIndex, "Sanity check failed!");

					writer.WriteLine("# nodes");
					for (int nodeIndex = 0; nodeIndex < matGraph.nodes.Length; nodeIndex++)
					{
						Graph.Node node = matGraph.nodes[nodeIndex];
						if (node.connectedComponentId != subGraphIndex)
						{
							continue;
						}

						int layersCount = Utils.BitCount(node.srcMasksId);

						string stringAttr = "";
						if (node == root)
						{
							stringAttr = ", shape=hexagon";
						}

						System.Diagnostics.Debug.Assert(nodeIndex == node.nodeIndex, "Sanity check failed!");

						string nodeStringId = string.Format("m_n{0}_src{1}", nodeIndex, node.GetSourceMasksString());
						writer.WriteLine(string.Format("{0} [label=\"{1}\\nids={2},a={3}\\n{4}\", style=filled{5}]", node.nodeIndex, nodeStringId, layersCount, node.data.Count, node.GetSourceMasksString(), stringAttr));
					}

					writer.WriteLine("# links");

					for (int nodeIndex = 0; nodeIndex < matGraph.nodes.Length; nodeIndex++)
					{
						Graph.Node node = matGraph.nodes[nodeIndex];
						if (node.connectedComponentId != subGraphIndex)
						{
							continue;
						}

						for (int edgeIndex = 0; edgeIndex < node.edges.Count; edgeIndex++)
						{
							if (node.edges[edgeIndex].isBroken)
							{
								continue;
							}

							bool isBackLink = (node.edges[edgeIndex].b == node);
							if (isBackLink)
							{
								continue;
							}

							Graph.Node targetNode = node.edges[edgeIndex].b;
							Debug.Assert(targetNode.connectedComponentId == subGraphIndex, "Sanity check failed!");

							int areaValue = node.edges[edgeIndex].GetEdgeWeight(); ;

							writer.WriteLine(string.Format("{0} -> {1} [label={2}];", node.nodeIndex, targetNode.nodeIndex, areaValue));
						}


					}


					writer.WriteLine("}");
				}

				writer.WriteLine("}");
			}

		}

		public static void SavePaletteMask(string fileName, byte[] mask, int width, int height)
		{
			Color32[] tmp = new Color32[width * height];
			Utils.SetArray<Color32>(tmp, Color32.black);

			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					int addr = y * width + x;
					if (mask[addr] != 0)
					{
						tmp[addr] = Color32.white;
					}
				}
			}

			TgaFormat.Save(fileName, tmp, false, width, height);
		}

		public static void SaveColoredGraph(string fileName, Graph graph, int width, int height, Color32[] colors)
		{
			Color32[] tmp = new Color32[width * height];
			Utils.SetArray<Color32>(tmp, Color32.transp);

			for (int nodeIndex = 0; nodeIndex < graph.nodes.Length; nodeIndex++)
			{
				Graph.Node node = graph.nodes[nodeIndex];

				int colorIndex = nodeIndex % colors.Length;

				//fill cluster
				for (int i = 0; i < node.data.Count; i++)
				{
					Short2 pos = node.data[i];
					int addr = (node.boundMin.y + pos.y) * width + (node.boundMin.x + pos.x);
					tmp[addr] = colors[colorIndex];
				}

				
				for (int i = 0; i < node.edges.Count; i++)
				{
					Graph.EdgePoints edgePoints = node.edges[i].GetMyEdgePoints(node);

					if (edgePoints == null)
					{
						continue;
					}

					for (int j = 0; j < edgePoints.points.Count; j++)
					{
						Short2 pos = edgePoints.points[j];
						int addr = (node.boundMin.y + pos.y) * width + (node.boundMin.x + pos.x);
						tmp[addr].a = 128;
					}
				}

			} // nodeIndex

			TgaFormat.Save(fileName, tmp, true, width, height);
		}


		public static void SaveGraphNode(string fileName, Graph.Node node)
		{
			int width = node.boundMax.x + 1;
			int height = node.boundMax.y + 1;

			Color32[] tmp = new Color32[width * height];
			Utils.SetArray<Color32>(tmp, Color32.black);

			//fill bound box
			for (int ty = node.boundMin.y; ty <= node.boundMax.y; ty++)
			{
				for (int tx = node.boundMin.x; tx <= node.boundMax.x; tx++)
				{
					int addr = ty * width + tx;
					tmp[addr] = Color32.green;
				}
			}

			//fill cluster
			for (int i = 0; i < node.data.Count; i++)
			{
				Short2 pos = node.data[i];
				int addr = (node.boundMin.y + pos.y) * width + (node.boundMin.x + pos.x);
				tmp[addr] = Color32.white;
			}

			for (int i = 0; i < node.edges.Count; i++)
			{
				Graph.EdgePoints edgePoints = node.edges[i].GetMyEdgePoints(node);

				if (edgePoints == null)
				{
					continue;
				}

				for (int j = 0; j < edgePoints.points.Count; j++)
				{
					Short2 pos = edgePoints.points[j];
					int addr = (node.boundMin.y + pos.y) * width + (node.boundMin.x + pos.x);
					tmp[addr] = Color32.red;
				}
			}

			TgaFormat.Save(fileName, tmp, false, width, height);
		}



		public static void SaveIntersectionBitmap(string namePattern, LayersProcessor.IntersectionBitmap bitmap)
		{
			int width = bitmap.width;
			int height = bitmap.height;

			HashSet<UInt64> uniqueIds = new HashSet<UInt64>();

			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					int addr = y * width + x;
					if (bitmap.buffer[addr] != 0)
					{
						uniqueIds.Add(bitmap.buffer[addr]);
					}
				}
			}

			Color32[] pixels = new Color32[width * height];
			foreach (UInt64 id in uniqueIds)
			{
				Utils.SetArray<Color32>(pixels, Color32.black);

				for (int y = 0; y < height; y++)
				{
					for (int x = 0; x < width; x++)
					{
						int addr = y * width + x;
						if (bitmap.buffer[addr] == id)
						{
							pixels[addr] = Color32.white;
						}
					}
				}

				string name = string.Format("{0}_{1}.tga", namePattern, Utils.BitsToString(id));
				TgaFormat.Save(name, pixels, false, width, height);
			}
		}


	}



}