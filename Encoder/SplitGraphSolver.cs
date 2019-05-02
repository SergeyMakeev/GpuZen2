using System;
using System.Collections.Generic;
using System.IO;

namespace SpatialClusteringEncoder
{

	//
	//
	// Greedy algorithm to solve Graph Partitioning Problem
	//   inspired by Kernighan–Lin algorithm
	//
	//
	static class SplitGraph
	{
		public class Solution
		{
			public int error = 0;
			public List<Graph.Edge> edgesToSplit = new List<Graph.Edge>();

			public Graph.Node root = null;

			public HashSet<Graph.Node> setA = null;
			public HashSet<Graph.Node> setB = null;

			public bool isFinalSolution = false;
		}


		static Solution FindSolutionStep(ref HashSet<Graph.Node> setA, ref HashSet<Graph.Node> setB, int maxUniqueIdsCount, ref UInt64 cachedPaletteForSetA, Dictionary<UInt64, int> edgeWeightsCache)
		{
			edgeWeightsCache.Clear();

			//find edge from setA to setB with the maximum weight
			foreach (Graph.Node node in setA)
			{
				for (int edgeIndex = 0; edgeIndex < node.edges.Count; edgeIndex++)
				{
					Graph.Edge edge = node.edges[edgeIndex];
					if (edge.isBroken)
					{
						continue;
					}

					Graph.Node target = edge.GetTarget(node);

					//check if we can move thought current edge (check cluster constraints)
					UInt64 combinedIds = (target.srcMasksId | cachedPaletteForSetA);
					if (Utils.BitCount(combinedIds) > maxUniqueIdsCount)
					{
						continue;
					}

					if (setA.Contains(target) == false)
					{
						Debug.Assert(setB.Contains(target) == true, "Sanity check failed!");

						UInt64 targetKey = target.srcMasksId;

						int edgeWeight = edge.GetEdgeWeight();

						int val = 0;
						if (edgeWeightsCache.TryGetValue(targetKey, out val) == false)
						{
							edgeWeightsCache.Add(targetKey, edgeWeight);
						} else
						{
							edgeWeightsCache[targetKey] = val + edgeWeight;
						}
					}
				}
			}


			//no more edges available
			if (edgeWeightsCache.Count == 0)
			{
				//two nodes and can't find solution, break all existing links as solution
				if (setB.Count == 1 && setA.Count == 1)
				{
					Solution finalSolution = new Solution();
					finalSolution.setA = new HashSet<Graph.Node>(setA);
					finalSolution.setB = new HashSet<Graph.Node>(setB);
					finalSolution.error = 0;
					finalSolution.isFinalSolution = true;

					foreach (Graph.Node node in setA)
					{
						for (int edgeIndex = 0; edgeIndex < node.edges.Count; edgeIndex++)
						{
							Graph.Edge edge = node.edges[edgeIndex];
							if (edge.isBroken)
							{
								continue;
							}

							int edgeWeight = edge.GetEdgeWeight();
							finalSolution.edgesToSplit.Add(edge);
							finalSolution.error += edgeWeight;
						}
					}

					return finalSolution;
				}

				return null;
			}

			//find max weight
			UInt64 maxWeightId = 0;
			int maxWeight = 0;
			foreach (var item in edgeWeightsCache)
			{
				if (item.Value > maxWeight)
				{
					maxWeight = item.Value;
					maxWeightId = item.Key;
				}
			}

			Debug.Assert(maxWeightId != 0, "Sanity check failed!");

			// move all desired nodes from setB to setA
			Solution solution = new Solution();

			HashSet<Graph.Node> verticesToMove = new HashSet<Graph.Node>();

			//Debug.LogInfo(string.Format("    Weight total: {0}, dest {1}", maxWeight, maxWeightId));

			foreach (Graph.Node node in setA)
			{
				for (int edgeIndex = 0; edgeIndex < node.edges.Count; edgeIndex++)
				{
					Graph.Edge edge = node.edges[edgeIndex];
					if (edge.isBroken)
					{
						continue;
					}

					Graph.Node target = edge.GetTarget(node);
					if (target.srcMasksId != maxWeightId)
					{
						continue;
					}

					//Debug.LogInfo(string.Format("      {0} to {1}, weight {2}", edge.a.GetStringId(), edge.b.GetStringId(), edge.GetEdgeWeight()));

					cachedPaletteForSetA |= target.srcMasksId;
					verticesToMove.Add(target);
				}
			}


			foreach (Graph.Node node in verticesToMove)
			{
				setB.Remove(node);
				setA.Add(node);
			}
			verticesToMove.Clear();

			// calculate solution error
			solution.setA = new HashSet<Graph.Node>(setA);
			solution.setB = new HashSet<Graph.Node>(setB);
			solution.error = 0;
			solution.isFinalSolution = false;
			foreach (Graph.Node node in setA)
			{
				for (int edgeIndex = 0; edgeIndex < node.edges.Count; edgeIndex++)
				{
					Graph.Edge edge = node.edges[edgeIndex];
					if (edge.isBroken)
					{
						continue;
					}

					Graph.Node target = edge.GetTarget(node);

					if (setA.Contains(target) == false)
					{
						Debug.Assert(setB.Contains(target) == true, "Sanity check failed!");

						UInt64 targetKey = target.srcMasksId;

						int edgeWeight = edge.GetEdgeWeight();
						solution.error += edgeWeight;
						solution.edgesToSplit.Add(edge);

						//Debug.LogInfo(string.Format("      {0} to {1}, weight {2}", edge.a.GetStringId(), edge.b.GetStringId(), edge.GetEdgeWeight()));
					}
				}
			}

			//Debug.LogInfo(string.Format("    solution error: {0}", solution.solutionError));

			return solution;
		}

		static Solution FindSolution(Graph.Node graphEntry, List<Graph.Node> graphVertices, int maxIdsCount, Dictionary<UInt64, int> edgeWeightsCache, HashSet<Graph.Node> setA, HashSet<Graph.Node> setB, List<Solution> solutions)
		{
			setA.Clear();
			setB.Clear();
			solutions.Clear();

			UInt64 cachedPaletteForSetA = graphEntry.srcMasksId;

			//split to initial sets
			setA.Add(graphEntry);
			for (int i = 0; i < graphVertices.Count; i++)
			{
				if (graphVertices[i] == graphEntry)
				{
					continue;
				}
				setB.Add(graphVertices[i]);
			}

			Solution bestSolution = null;

			while (true)
			{
				Solution stepSolution = FindSolutionStep(ref setA, ref setB, maxIdsCount, ref cachedPaletteForSetA, edgeWeightsCache);

				//no more solutions
				if (stepSolution == null)
				{
					break;
				}

				stepSolution.root = graphEntry;
				if (bestSolution == null || stepSolution.error < bestSolution.error)
				{
					bestSolution = stepSolution;
				}

				if (stepSolution.isFinalSolution)
				{
					break;
				}
			}

			return bestSolution;
		}

		static public Solution Solve(Graph.Node[] nodes, int subgraphId, int maxIdsCount)
		{
			int[] histogram = new int[64];
			Utils.SetArray<int>(histogram, 0);

			// get subgraph vertices
			List<Graph.Node> graphVertices = new List<Graph.Node>(nodes.Length);
			UInt64 combinedIds = 0;
			for (int i = 0; i < nodes.Length; i++)
			{
				if (nodes[i].connectedComponentId != subgraphId)
				{
					continue;
				}

				UInt64 bitToCheck = 1;
				for (int bitIndex = 0; bitIndex < histogram.Length; bitIndex++)
				{
					if ((nodes[i].srcMasksId & bitToCheck) != 0)
					{
						histogram[bitIndex] += nodes[i].data.Count;
					}

					bitToCheck = bitToCheck << 1;
				}
				combinedIds |= nodes[i].srcMasksId;
				graphVertices.Add(nodes[i]);
			}

			// input graph was already solved
			int idsCount = Utils.BitCount(combinedIds);
			if (idsCount <= maxIdsCount)
			{
				//Debug.LogInfo(string.Format("Subgraph {0} already solved. Unique layers count {1}.", subgraphId, idsCount));
				return null;
			}

			Debug.Log(string.Format("    Solve Subgraph {0}. Unique layers count {1}. Vertices {2}", subgraphId, idsCount, graphVertices.Count));

			const int maxIterationsCount = 20;

			graphVertices.Sort((i1, i2) =>
			{
				int res = i2.edges.Count.CompareTo(i1.edges.Count);
				if (res == 0)
				{
					res = i2.data.Count.CompareTo(i1.data.Count);
				}
				return res;
			});


			Dictionary<UInt64, int> edgeWeightsCache = new Dictionary<UInt64, int>();

			HashSet<Graph.Node> setACache = new HashSet<Graph.Node>();
			HashSet<Graph.Node> setBCache = new HashSet<Graph.Node>();
			List<Solution> solutionsCache = new List<Solution>(graphVertices.Count);

			// limit maximum number of iteration
			int maxFindIterations = Utils.Max( Utils.Min(graphVertices.Count, maxIterationsCount), 1);

			// try to solve using vertices with many edges
			Solution bestSolution = null;
			for (int i = 0; i < maxFindIterations;i++)
			{
				Solution currentSolution = FindSolution(graphVertices[i], graphVertices, maxIdsCount, edgeWeightsCache, setACache, setBCache, solutionsCache);
				if (currentSolution != null)
				{
					if (bestSolution == null || currentSolution.error < bestSolution.error)
					{
						bestSolution = currentSolution;
					}
				}
			}

			//try to solve from other end. use vertices with as less edges as possible
			int remainingVertices = graphVertices.Count - maxFindIterations;
			int maxFindReverseIterations = Utils.Max( Utils.Min(remainingVertices, maxIterationsCount), 1);

			for (int j = 0; j < maxFindReverseIterations; j++)
			{
				int i = (graphVertices.Count - 1 - j);
				Solution currentSolution = FindSolution(graphVertices[i], graphVertices, maxIdsCount, edgeWeightsCache, setACache, setBCache, solutionsCache);
				if (currentSolution != null)
				{
					if (bestSolution == null || currentSolution.error < bestSolution.error)
					{
						bestSolution = currentSolution;
					}
				}
			}



			if (bestSolution != null)
			{
				Debug.Log(string.Format("error {0}", bestSolution.error));

				if (idsCount == (maxIdsCount + 1))
				{
					int removeError = bestSolution.error;
					int removeId = -1;

					UInt64 bitToCheck = 1;
					for (int bitIndex = 0; bitIndex < histogram.Length; bitIndex++)
					{
						if ((combinedIds & bitToCheck) != 0)
						{
							int currentRemoveError = histogram[bitIndex];
							if (currentRemoveError < removeError)
							{
								removeError = currentRemoveError;
								removeId = bitIndex;
							}
						}
						bitToCheck = bitToCheck << 1;
					}

					//is better to remove unwanted id, than split graph
					if (removeId >= 0)
					{
						//TODO:
						Debug.LogWarning(string.Format("Better solution exist. Need to delete layer {0}", removeId));
					}
				}

				return bestSolution;
			}


			Debug.LogError(string.Format("Subgraph {0}. Can't find solution!", subgraphId));

/*
			// try to solve using vertices with many edges
			for (int i = 0; i < maxFindIterations; i++)
			{
				Solution currentSolution = FindSolution(graphVertices[i], graphVertices, maxIdsCount, edgeWeightsCache, setACache, setBCache, solutionsCache);
				if (currentSolution != null)
				{
					if (bestSolution == null || currentSolution.error < bestSolution.error)
					{
						bestSolution = currentSolution;
					}
				}
			}
*/
			
			return null;
		}
	}
}