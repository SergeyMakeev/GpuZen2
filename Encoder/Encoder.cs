using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace SpatialClusteringEncoder
{

    //
    //
    //
    //
    //
    //
    //
    //
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    class Graph
    {

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public class EdgePoints
        {
            //bound box min/max
            public Short2 boundMin = Short2.zero;
            public Short2 boundMax = Short2.zero;

            public List<Short2> points = null;
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public class Edge
        {
            public Node a;
            public Node b;

            public EdgePoints splitA = null;
            public EdgePoints splitB = null;

            public bool isBroken = false;

            public Edge(Node _a, Node _b)
            {
                a = _a;
                b = _b;
            }

            public int GetEdgeWeight()
            {
                return Utils.Max(splitA.points.Count, splitB.points.Count);
            }

            public Node GetTarget(Node src)
            {
                if (src == a)
                {
                    return b;
                }

                if (src == b)
                {
                    return a;
                }

                Debug.Assert(false, "Sanity check failed!");
                return null;
            }

            public EdgePoints GetMyEdgePoints(Node src)
            {
                if (src == a)
                {
                    return splitA;
                }

                if (src == b)
                {
                    return splitB;
                }

                Debug.Assert(false, "Sanity check failed!");
                return null;
            }

        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public class Node
        {
            //bound box min/max
            public Short2 boundMin = Short2.zero;
            public Short2 boundMax = Short2.zero;

            // node points
            public List<Short2> data = new List<Short2>(4096);

            // 
            public List<Edge> edges = new List<Edge>(32);

            // node index (id)
            public int nodeIndex = -1;

            // connected component of subgraph
            public int connectedComponentId = -1;

            // bitmask for all layers that affect to this node
            public UInt64 srcMasksId = 0;


            public List<int> GetSourceMasksArray()
            {
                return Utils.BitsToList(srcMasksId);
            }

            public string GetSourceMasksString()
            {
                return Utils.BitsToString(srcMasksId);
            }
        }

        public Graph.Node[] nodes;
        public List<Graph.Node> roots;
    }


    sealed class LayersProcessorResultCluster
    {
        public string baseLayer { get; set; }

        public List<string> layers { get; set; }
    }

    sealed class LayersProcessorResult
    {
        public List<LayersProcessorResultCluster> clusters { get; set; }
    }

    //
    //
    //
    //
    //
    //
    //
    //
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    sealed class LayersProcessorJob
    {
        public int maxMipLevel { get; set; }
        public int maxLocalLayersCount { get; set; }

        public string descriptionFile { get; set; }
        public string indirectMapFile { get; set; }
        public string weightsMapFile { get; set; }

        public string sourceColorID { get; set; }
        public List<string> sourceLayers { get; set; }

        /*
				public int maxMipLevel = 3;
				public int maxLocalLayersCount = 4;

				public string descriptionFile;
				public string indirectMapFile;
				public string weightsMapFile;

				public string subsetsId;
				public List<string> sourceLayers = new List<string>();
		*/

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public bool Validate()
        {
            if (maxMipLevel < 1 || maxMipLevel > 5)
            {
                Debug.LogError(string.Format("Invalid mip level {0}. Mip level must be in range 1..5", maxMipLevel));
                return false;
            }

            if (maxLocalLayersCount < 2 || maxLocalLayersCount > 5)
            {
                Debug.LogError(string.Format("Invalid local layers count {0}. Local layers count must be in range 2..5", maxLocalLayersCount));
                return false;
            }

            if (string.IsNullOrEmpty(indirectMapFile))
            {
                Debug.LogError("Result indirect map file name can't be empty");
                return false;
            }

            if (string.IsNullOrEmpty(descriptionFile))
            {
                Debug.LogError("Result indirect map description file name can't be empty");
                return false;
            }

            if (string.IsNullOrEmpty(weightsMapFile))
            {
                Debug.LogError("Result weights map file name can't be empty");
                return false;
            }

            if (string.IsNullOrEmpty(sourceColorID))
            {
                Debug.LogError("Subset Id map file name can't be empty");
                return false;
            }

            return true;
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public void PrintParameters()
        {
            Debug.Log("Job description");

            Debug.Log(string.Format("  Max mip level : {0}", maxMipLevel));
            Debug.Log(string.Format("  Max local layers : {0}", maxLocalLayersCount));

            Debug.Log("  Base layer");
            Debug.Log(string.Format("    {0}", sourceColorID));

            Debug.Log(string.Format("  Source layers, count {0}", sourceLayers.Count));
            for (int i = 0; i < sourceLayers.Count; i++)
            {
                Debug.Log(string.Format("    [{0}] - '{1}'", (i + 1), sourceLayers[i]));
            }

            Debug.Log("  Destination");
            Debug.Log(string.Format("    {0}", descriptionFile));
            Debug.Log(string.Format("    {0}", indirectMapFile));
            Debug.Log(string.Format("    {0}", weightsMapFile));
            Debug.Log("");
        }
    }

    //
    //
    //
    //
    //
    //
    //
    //
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    sealed class LayersProcessor
    {
        private struct LayerDesc
        {
            public UInt64 layerMask;
            public float absWeight;

            public LayerDesc(UInt64 _layerMask, float _absWeight)
            {
                layerMask = _layerMask;
                absWeight = _absWeight;
            }
        }

        public class IntersectionBitmap
        {
            public int width;
            public int height;
            public UInt64[] buffer = null;

            public IntersectionBitmap(int _width, int _height)
            {
                width = _width;
                height = _height;
                buffer = new UInt64[width * height];
                Utils.SetArray<UInt64>(buffer, 0);
            }
        }

        private struct ApproximateCollisionPair
        {
            public Graph.Node a;
            public Graph.Node b;

            public ApproximateCollisionPair(Graph.Node _a, Graph.Node _b)
            {
                a = _a;
                b = _b;
            }

            public bool IsEqual(ApproximateCollisionPair other)
            {
                if ((other.a == a && other.b == b) || (other.a == b && other.b == a))
                {
                    return true;
                }

                return false;
            }
        }


        private class LocalPalette
        {
            public UInt64 ids = 0;
            public List<int> sourceIds = null;
            public byte[] mask = null;
            public int maskWidth = 0;
            public int maskHeight = 0;
            public Color32 baseLayerColor = Color32.transp;
        }

        static Short2[] neighborsOffsets = new Short2[8] {
            new Short2(-1, -1),
            new Short2( 0, -1),
            new Short2( 1, -1),
            new Short2( 1,  0),
            new Short2( 1,  1),
            new Short2( 0,  1),
            new Short2(-1,  1),
            new Short2(-1,  0)
        };

        struct NodeCollisionCacheItem
        {
            //value (0 = no node in this position)
            public byte val;

            //flag - (if this flag is not equal 0 this pixel already added to shared edge)
            public byte flag;
        }

        NodeCollisionCacheItem emptyCache = new NodeCollisionCacheItem() { val = 0, flag = 0 };
        NodeCollisionCacheItem[] rasterizedCollisionCache0 = null;
        NodeCollisionCacheItem[] rasterizedCollisionCache1 = null;


        LayersProcessorJob job;

        int sourceWidth = 0;
        int sourceHeight = 0;

        CpuTexture2D matBaseLayer;
        CpuTexture2D[] sourceLayers;

        CpuTexture2D downsampledBaseLayer;
        CpuTexture2D[] downsampledLayers;

        IntersectionBitmap matIntersections;

        Graph matGraph;


        List<LocalPalette> localPalettes = new List<LocalPalette>(16);
        CpuTexture2D indirectionMap = null;
        CpuTexture2D weightsMap = null;

        CpuTexture2D errors = null;

        static UInt64 baseLayerMask = Utils.GetLayerMask(63);

        const Int16 overlapSize = 1;

        List<Color32> uniqueColors = new List<Color32>(64);


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        bool LoadColorIdMap()
        {
            sourceWidth = 0;
            sourceHeight = 0;

            Debug.Log(string.Format("  Load subsets id_map '{0}'", job.sourceColorID));

            matBaseLayer = CpuTexture2D.LoadFromFile(job.sourceColorID);
            if (matBaseLayer == null)
            {
                return false;
            }

            sourceWidth = matBaseLayer.width;
            sourceHeight = matBaseLayer.height;

            if (!Utils.IsPowerOfTwo((uint)sourceWidth) || !Utils.IsPowerOfTwo((uint)sourceHeight))
            {
                Debug.LogError(string.Format("  Source texture dimension {0}x{1} must be power of two", sourceWidth, sourceHeight));
                return false;
            }

            Debug.Log(string.Format("  {0}x{1}", sourceWidth, sourceHeight));

            uniqueColors.Clear();

            for (int addr = 0; addr < matBaseLayer.pixelsCount; addr++)
            {
                Color32 pixel = matBaseLayer.texels[addr];

                if (pixel.a <= 8)
                {
                    //skip transp pixels
                    matBaseLayer.texels[addr] = Color32.transp;
                    continue;
                }

                if (pixel.r <= 8 && pixel.g <= 8 && pixel.b <= 8)
                {
                    //skip black pixels
                    matBaseLayer.texels[addr] = Color32.transp;
                    continue;
                }

                //is pixel exist in our palette?
                bool isFound = false;
                for (int j = 0; j < uniqueColors.Count; j++)
                {
                    if (Utils.IsNearRGB(uniqueColors[j], pixel))
                    {
                        matBaseLayer.texels[addr] = uniqueColors[j];
                        isFound = true;
                        break;
                    }
                }

                if (isFound == false)
                {
                    if (uniqueColors.Count >= 64)
                    {
                        Debug.LogError("Too many Id's in color id map (>64). Wrong texture?");
                        return false;
                    }
                    uniqueColors.Add(pixel);
                }

                //set alpha to 255
                matBaseLayer.texels[addr].a = 255;
            }

            //unique colors found...
            Debug.Log("");
            for (int i = 0; i < uniqueColors.Count; i++)
            {
                Debug.Log(string.Format("Base material #{0} = 0x{1:X2}{2:X2}{3:X2}", i, uniqueColors[i].r, uniqueColors[i].g, uniqueColors[i].b));
            }

            return true;
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        bool LoadSourceLayers()
        {
            Debug.Log(string.Format("  Load {0} layers", job.sourceLayers.Count));

            sourceLayers = new CpuTexture2D[job.sourceLayers.Count];
            for (int i = 0; i < job.sourceLayers.Count; i++)
            {
                string sourceLayerFileName = job.sourceLayers[i];
                Debug.Log(string.Format("    Load image '{0}'", sourceLayerFileName));
                CpuTexture2D layerTexture = CpuTexture2D.LoadFromFile(sourceLayerFileName);
                if (layerTexture == null)
                {
                    return false;
                }

                if (layerTexture.width != sourceWidth || layerTexture.height != sourceHeight)
                {
                    Debug.LogError(string.Format("    Invalid texture dimension {0}x{1}, all layers texture must be {2}x{3}", layerTexture.width, layerTexture.height, sourceWidth, sourceHeight));
                    return false;
                }

                sourceLayers[i] = layerTexture;
            }

            return true;
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        bool BuildDownsampledMasks()
        {
            Debug.Log(string.Format("  Downsample {0} layers", sourceLayers.Length));

            Debug.Log("    Downsample base layer (bottom)");
            downsampledBaseLayer = BuildDownsampledBaseMask_MaxFilter(matBaseLayer, job.maxMipLevel);
            if (downsampledBaseLayer == null)
            {
                return false;
            }


            Debug.Log("    Top to bottom");

            downsampledLayers = new CpuTexture2D[sourceLayers.Length];
            for (int i = 0; i < sourceLayers.Length; i++)
            {
                Debug.Log(string.Format("    [{0}] Downsample image '{1}'", i, job.sourceLayers[i]));
                CpuTexture2D downsampledLayer = BuildDownsampledMask_MaxFilter(sourceLayers[i], job.maxMipLevel);
                if (downsampledLayer == null)
                {
                    return false;
                }

                /*
				//clear unused texels
				for (int addr = 0; addr < downsampledBaseLayer.texels.Length; addr++)
				{
					if (downsampledBaseLayer.texels[addr].a == 0)
					{
						downsampledLayer.texels[addr] = Color32.black;
					}
				}
				*/

                downsampledLayers[i] = downsampledLayer;

            }

            return true;
        }


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        bool SelectAndIntersectLayers()
        {
            int width = downsampledBaseLayer.width;
            int height = downsampledBaseLayer.height;

            int layersCount = downsampledLayers.Length;

            LayerDesc[] pixelLayers = new LayerDesc[layersCount + 1];

            CpuTexture2D matBaseMask = downsampledBaseLayer;

            matIntersections = new IntersectionBitmap(width, height);


            bool hasErrors = false;
            CpuTexture2D errorsTex = CpuTexture2D.CreateEmpty(width, height, Color32.black);


            // recalculate alpha blend as weighted sum
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int addr = y * width + x;

                    // skip empty pixels
                    byte insideMaterialMapping = matBaseMask.texels[addr].a;
                    if (insideMaterialMapping == 0)
                    {
                        continue;
                    }

                    float mulAcc = 1.0f;
                    for (int layerIndex = 0; layerIndex < layersCount; layerIndex++)
                    {
                        pixelLayers[layerIndex].layerMask = Utils.GetLayerMask(layerIndex);

                        byte maskValue = downsampledLayers[layerIndex].texels[addr].r;

                        float maskAlpha = (float)maskValue / 255.0f;
                        float absLayerWeight = maskAlpha * mulAcc;
                        mulAcc = mulAcc * (1.0f - maskAlpha);

                        pixelLayers[layerIndex].absWeight = absLayerWeight;
                    } // layers iterator

                    // force add base layer (alpha = 1.0f)
                    pixelLayers[layersCount].layerMask = baseLayerMask;
                    pixelLayers[layersCount].absWeight = 1.0f * mulAcc;

                    // sort layers by absolute weight
                    Array.Sort<LayerDesc>(pixelLayers, (i1, i2) => i2.absWeight.CompareTo(i1.absWeight));

                    // calculate weights sum and number of layers used in this pixel
                    int pixelLayersCount = 0;
                    for (int i = 0; i < pixelLayers.Length; i++)
                    {
                        float w = pixelLayers[i].absWeight;
                        if (w > 0.0f)
                        {
                            pixelLayersCount++;
                        }
                    }

                    if (pixelLayersCount > job.maxLocalLayersCount)
                    {
                        // ERROR: too many visible layers in this pixel (> maxLocalLayersCount)
                        hasErrors = true;
                        errorsTex.texels[addr] = Color32.red;

                        // normalize existing weights
                        float weightsSum = 0.0f;
                        for (int i = 0; i < job.maxLocalLayersCount; i++)
                        {
                            weightsSum += pixelLayers[i].absWeight;
                        }

                        for (int i = 0; i < job.maxLocalLayersCount; i++)
                        {
                            pixelLayers[i].absWeight /= weightsSum;
                        }

                        //clear other layers
                        for (int i = job.maxLocalLayersCount; i < pixelLayers.Length; i++)
                        {
                            pixelLayers[i].absWeight = 0.0f;
                        }
                    }

                    // dump result (bitwise intersection)
                    for (int i = 0; i < pixelLayers.Length; i++)
                    {
                        if (pixelLayers[i].absWeight > 0.0f)
                        {
                            UInt64 layerMask = pixelLayers[i].layerMask;
                            matIntersections.buffer[addr] |= layerMask;
                        }
                    }

                } // x
            } // y



            if (hasErrors)
            {
                Debug.LogWarning("Too many layers per pixel. Save error to 'too_many_masks_error.tga'");
                TgaFormat.Save("too_many_masks_error.tga", errorsTex.texels, false, errorsTex.width, errorsTex.height);
            }

            return true;
        }


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        static Graph.Node[] FindConnectedRegions(IntersectionBitmap intersections)
        {
            int width = intersections.width;
            int height = intersections.height;

            Queue<Short2> needVisitStack = new Queue<Short2>(16384);
            byte[] visited = new byte[width * height];
            Utils.SetArray<byte>(visited, 0);

            List<Graph.Node> regions = new List<Graph.Node>(64);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int addr = y * width + x;

                    if (visited[addr] != 0)
                    {
                        continue;
                    }

                    UInt64 regionId = intersections.buffer[addr];

                    //find region entry point
                    if (regionId != 0)
                    {
                        Graph.Node node = BuildConnectedRegion(x, y, regionId, intersections, visited, needVisitStack);
                        node.nodeIndex = regions.Count;
                        node.srcMasksId = regionId;
                        regions.Add(node);
                    }
                    else
                    {
                        visited[addr] = 1;
                    }
                }
            }

            Graph.Node[] res = regions.ToArray();
            return res;
        }


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        static Graph.Node BuildConnectedRegion(int x, int y, UInt64 regionId, IntersectionBitmap intersections, byte[] visited, Queue<Short2> needVisitStack)
        {
            Graph.Node node = new Graph.Node();

            needVisitStack.Clear();
            needVisitStack.Enqueue(new Short2(x, y));

            Short2 boundMin = new Short2(x, y);
            Short2 boundMax = new Short2(x, y);

            while (needVisitStack.Count > 0)
            {
                Short2 pos = needVisitStack.Dequeue();
                int addr = pos.y * intersections.width + pos.x;

                //this point was already visited after point was added
                if (visited[addr] != 0)
                {
                    continue;
                }

                visited[addr] = 1;

                boundMin.x = Utils.Min(boundMin.x, pos.x);
                boundMin.y = Utils.Min(boundMin.y, pos.y);

                boundMax.x = Utils.Max(boundMax.x, pos.x);
                boundMax.y = Utils.Max(boundMax.y, pos.y);

                node.data.Add(pos);

                // add 8 neighbors
                for (int nb = 0; nb < neighborsOffsets.Length; nb++)
                {
                    Short2 offset = neighborsOffsets[nb];
                    int nbX = pos.x + offset.x;
                    int nbY = pos.y + offset.y;

                    if (Utils.IsClampedCoords(nbX, nbY, intersections.width, intersections.height))
                    {
                        continue;
                    }

                    int nbAddr = nbY * intersections.width + nbX;
                    if (visited[nbAddr] != 0)
                    {
                        continue;
                    }

                    //
                    UInt64 nbRegionId = intersections.buffer[nbAddr];
                    if (nbRegionId == regionId)
                    {
                        needVisitStack.Enqueue(new Short2(nbX, nbY));
                    }

                }
            } // while (needVisit.Count > 0)

            node.boundMin = boundMin;
            node.boundMax = boundMax;

            // sort data points (iterator will iterate points from left to right and from top to bottom order)
            node.data.Sort((i1, i2) =>
            {
                int res = i1.y.CompareTo(i2.y);
                if (res == 0)
                    res = i1.x.CompareTo(i2.x);
                return res;
            });


            //move points to local space
            for (int i = 0; i < node.data.Count; i++)
            {
                int localX = node.data[i].x - node.boundMin.x;
                int localY = node.data[i].y - node.boundMin.y;
                node.data[i] = new Short2(localX, localY);
            }

            return node;
        }


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        static CpuTexture2D BuildDownsampledBaseMask_MaxFilter(CpuTexture2D source, int mipLevel)
        {
            if (mipLevel <= 0)
            {
                Debug.LogError("Invalid input");
                return null;
            }

            int pixelSize = (int)(1U << mipLevel);
            if (source.width < pixelSize || source.height < pixelSize)
            {
                Debug.LogError("Invalid input");
                return null;
            }

            bool hasError = false;
            CpuTexture2D errorTexture = null;

            CpuTexture2D res = CpuTexture2D.CreateEmpty(source.width >> mipLevel, source.height >> mipLevel);

            for (int y = 0; y < res.height; y++)
            {
                for (int x = 0; x < res.width; x++)
                {
                    bool blockColorIsValid = false;
                    Color32 blockColor = Color32.transp;

                    bool blockHasErrors = false;

                    for (int sy = 0; sy < pixelSize; sy++)
                    {
                        for (int sx = 0; sx < pixelSize; sx++)
                        {
                            int ox = x * pixelSize + sx;
                            int oy = y * pixelSize + sy;

                            int addr = oy * source.width + ox;

                            Color32 pixelColor = source.texels[addr];

                            // skip empty pixels
                            if (pixelColor.a == 0)
                            {
                                continue;
                            }

                            if (blockColorIsValid == false)
                            {
                                blockColor = pixelColor;
                                blockColorIsValid = true;
                            }
                            else
                            {
                                if (Utils.IsSameRGB(blockColor, pixelColor) == false)
                                {
                                    blockHasErrors = true;
                                    //error found
                                    hasError = true;

                                    if (errorTexture == null)
                                    {
                                        errorTexture = CpuTexture2D.CreateEmpty(source.width, source.height, Color32.black);
                                    }

                                    //red = error inside block
                                    errorTexture.texels[addr] = Color32.red;
                                }
                            }
                        }
                    }

                    if (blockHasErrors)
                    {
                        for (int sy = 0; sy < pixelSize; sy++)
                        {
                            for (int sx = 0; sx < pixelSize; sx++)
                            {
                                int ox = x * pixelSize + sx;
                                int oy = y * pixelSize + sy;

                                int addr = oy * source.width + ox;

                                //green = block with different color codes inside
                                errorTexture.texels[addr].g = 255;
                            }
                        }
                    }

                    int blockAddr = y * res.width + x;
                    res.texels[blockAddr] = blockColor;
                }
            }

            //check neighbor blocks
            for (int blockY = 0; blockY < res.height; blockY++)
            {
                for (int blockX = 0; blockX < res.width; blockX++)
                {
                    int blockAddr = blockY * res.width + blockX;
                    Color32 blockColor = res.texels[blockAddr];

                    if (blockColor.a == 0)
                    {
                        continue;
                    }

                    // check 8 neighbors
                    for (int nb = 0; nb < neighborsOffsets.Length; nb++)
                    {
                        Short2 offset = neighborsOffsets[nb];
                        int nbX = blockX + offset.x;
                        int nbY = blockY + offset.y;

                        if (Utils.IsClampedCoords(nbX, nbY, res.width, res.height))
                        {
                            continue;
                        }

                        int nbAddr = nbY * res.width + nbX;
                        Color32 nbColor = res.texels[nbAddr];

                        if (nbColor.a == 0)
                        {
                            continue;
                        }

                        if (Utils.IsSameRGB(nbColor, blockColor) == false)
                        {
                            //block error found
                            hasError = true;

                            if (errorTexture == null)
                            {
                                errorTexture = CpuTexture2D.CreateEmpty(source.width, source.height, Color32.black);
                            }

                            for (int sy = 0; sy < pixelSize; sy++)
                            {
                                for (int sx = 0; sx < pixelSize; sx++)
                                {
                                    int ox = blockX * pixelSize + sx;
                                    int oy = blockY * pixelSize + sy;

                                    int addr = oy * source.width + ox;

                                    //blue = block filtration error
                                    errorTexture.texels[addr].b = 255;
                                }
                            }


                        }
                    }
                }
            }


            if (hasError)
            {
                Debug.LogError("Bad mapping found, see 'invalid_mapping.tga' to details");
                TgaFormat.Save("invalid_mapping.tga", errorTexture.texels, false, errorTexture.width, errorTexture.height);
                return null;
            }

            return res;
        }


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        static CpuTexture2D BuildDownsampledMask_MaxFilter(CpuTexture2D source, int mipLevel)
        {
            if (mipLevel <= 0)
            {
                Debug.LogError("Invalid input");
                return null;
            }

            int pixelSize = (int)(1U << mipLevel);
            if (source.width < pixelSize || source.height < pixelSize)
            {
                Debug.LogError("Invalid input");
                return null;
            }

            CpuTexture2D res = CpuTexture2D.CreateEmpty(source.width >> mipLevel, source.height >> mipLevel);

            for (int y = 0; y < res.height; y++)
            {
                for (int x = 0; x < res.width; x++)
                {
                    byte maxMask = 0;
                    for (int sy = 0; sy < pixelSize; sy++)
                    {
                        for (int sx = 0; sx < pixelSize; sx++)
                        {
                            int ox = x * pixelSize + sx;
                            int oy = y * pixelSize + sy;

                            byte maskValue = source.Load(ox, oy).r;
                            maxMask = Utils.Max(maxMask, maskValue);
                        }
                    }

                    int addr = y * res.width + x;
                    res.texels[addr].r = maxMask;
                    res.texels[addr].g = maxMask;
                    res.texels[addr].b = maxMask;
                    res.texels[addr].a = maxMask;
                }
            }
            return res;
        }


        bool BuildConnectedRegions()
        {
            Debug.Log("  BuildConnectedRegions");

            matGraph = new Graph();
            matGraph.nodes = FindConnectedRegions(matIntersections);
            if (matGraph.nodes == null)
            {
                Debug.LogError("    Can't find connected region");
            }

            Debug.Log(string.Format("    {0} regions found", matGraph.nodes.Length));

            return true;
        }


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        void DumpBaseConnnectedRegions()
        {
            Debug.Log("  DumpBaseConnnectedRegions");

            int width = downsampledBaseLayer.width;
            int height = downsampledBaseLayer.height;

            IntersectionBitmap tempIntersections = new IntersectionBitmap(width, height);

            for (int addr = 0; addr < downsampledBaseLayer.texels.Length; addr++)
            {
                if (downsampledBaseLayer.texels[addr].a != 0)
                {
                    tempIntersections.buffer[addr] = baseLayerMask;
                }
            }

            matGraph = new Graph();
            matGraph.nodes = FindConnectedRegions(tempIntersections);

            Color32[] colors = new Color32[14];

            colors[0] = new Color32(255, 0, 0, 255);
            colors[1] = new Color32(0, 255, 0, 255);
            colors[2] = new Color32(0, 0, 255, 255);
            colors[3] = new Color32(255, 0, 255, 255);
            colors[4] = new Color32(255, 255, 0, 255);
            colors[5] = new Color32(0, 255, 255, 255);
            colors[6] = new Color32(255, 255, 255, 255);

            colors[7] = new Color32(192, 0, 0, 255);
            colors[8] = new Color32(0, 192, 0, 255);
            colors[9] = new Color32(0, 0, 192, 255);
            colors[10] = new Color32(192, 0, 192, 255);
            colors[11] = new Color32(192, 192, 0, 255);
            colors[12] = new Color32(0, 192, 192, 255);
            colors[13] = new Color32(192, 192, 192, 255);

            string fileName = string.Format("connectivity_base_{0}.tga", matGraph.nodes.Length);
            Debug.LogWarning("Save connectivity debug.");

            DebugUtils.SaveColoredGraph(fileName, matGraph, width, height, colors);
        }


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        bool BuildGraphConnections()
        {
            if (!CreateGraphConnections(matGraph))
            {
                return false;
            }

            Debug.Log(string.Format("    {0} subgraphs found", matGraph.roots.Count));

            return true;
        }


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        Graph.Edge FindSharedEdge(ApproximateCollisionPair pair)
        {
            Graph.Node node0 = pair.a;
            Graph.Node node1 = pair.b;

            Graph.EdgePoints edgePts0 = new Graph.EdgePoints();
            edgePts0.points = new List<Short2>(32);
            edgePts0.boundMin = Short2.maxValue;
            edgePts0.boundMax = Short2.minValue;

            Graph.EdgePoints edgePts1 = new Graph.EdgePoints();
            edgePts1.points = new List<Short2>(32);
            edgePts1.boundMin = Short2.maxValue;
            edgePts1.boundMax = Short2.minValue;

            //calculate intersection rectangle
            Short2 rectMin = new Short2(0, 0);
            Short2 rectMax = new Short2(0, 0);
            bool res = Utils.GetIntersectionRect(node0.boundMin, node0.boundMax, node1.boundMin, node1.boundMax, ref rectMin, ref rectMax, overlapSize);
            Debug.Assert(res == true, "Sanity check failed");

            //intersection rect size
            int width = (rectMax.x - rectMin.x + 1);
            int height = (rectMax.y - rectMin.y + 1);

            // rasterize node0
            //////////////////////////////////////////////////////////////////////////

            Utils.SetArray<NodeCollisionCacheItem>(rasterizedCollisionCache0, emptyCache);

            //calculate relative offset for node0
            int ox = node0.boundMin.x - rectMin.x;
            int oy = node0.boundMin.y - rectMin.y;

            for (int i = 0; i < node0.data.Count; i++)
            {
                Short2 pos = node0.data[i];
                int x = pos.x + ox;
                int y = pos.y + oy;

                //pixel outside intersection rectangle
                if ((x < 0) || (y < 0) || (x >= width) || (y >= height))
                {
                    continue;
                }

                int addr = y * width + x;
                rasterizedCollisionCache0[addr].val = 255;
            }


            // rasterize node1 and check against node0
            //////////////////////////////////////////////////////////////////////////

            Utils.SetArray<NodeCollisionCacheItem>(rasterizedCollisionCache1, emptyCache);

            //calculate relative offset for node1
            ox = node1.boundMin.x - rectMin.x;
            oy = node1.boundMin.y - rectMin.y;

            for (int i = 0; i < node1.data.Count; i++)
            {
                Short2 pos = node1.data[i];
                int x = pos.x + ox;
                int y = pos.y + oy;

                //pixel outside intersection rectangle
                if ((x < 0) || (y < 0) || (x >= width) || (y >= height))
                {
                    continue;
                }

                int addr = y * width + x;
                rasterizedCollisionCache1[addr].val = 255;

                // check 8 neighbors
                for (int nb = 0; nb < neighborsOffsets.Length; nb++)
                {
                    Short2 offset = neighborsOffsets[nb];
                    int nbX = x + offset.x;
                    int nbY = y + offset.y;

                    if (Utils.IsClampedCoords(nbX, nbY, width, height))
                    {
                        continue;
                    }

                    //shared pixels found!
                    int nbAddr = nbY * width + nbX;
                    if (rasterizedCollisionCache0[nbAddr].val != 0)
                    {
                        if (rasterizedCollisionCache1[addr].flag == 0)
                        {
                            rasterizedCollisionCache1[addr].flag = 255;
                            edgePts1.points.Add(new Short2(pos.x, pos.y));

                            edgePts1.boundMin.x = Utils.Min(edgePts1.boundMin.x, pos.x);
                            edgePts1.boundMin.y = Utils.Min(edgePts1.boundMin.y, pos.y);

                            edgePts1.boundMax.x = Utils.Max(edgePts1.boundMax.x, pos.x);
                            edgePts1.boundMax.y = Utils.Max(edgePts1.boundMax.y, pos.y);
                        }

                        // break 
                        break;
                    }
                }
            }

            // check node0 vs node1
            //////////////////////////////////////////////////////////////////////////

            //calculate relative offset for node0
            ox = node0.boundMin.x - rectMin.x;
            oy = node0.boundMin.y - rectMin.y;

            for (int i = 0; i < node0.data.Count; i++)
            {
                Short2 pos = node0.data[i];
                int x = pos.x + ox;
                int y = pos.y + oy;

                //pixel outside intersection rectangle
                if ((x < 0) || (y < 0) || (x >= width) || (y >= height))
                {
                    continue;
                }

                int addr = y * width + x;

                // check 8 neighbors
                for (int nb = 0; nb < neighborsOffsets.Length; nb++)
                {
                    Short2 offset = neighborsOffsets[nb];
                    int nbX = x + offset.x;
                    int nbY = y + offset.y;

                    if (Utils.IsClampedCoords(nbX, nbY, width, height))
                    {
                        continue;
                    }

                    //shared pixels found!
                    int nbAddr = nbY * width + nbX;
                    if (rasterizedCollisionCache1[nbAddr].val != 0)
                    {
                        if (rasterizedCollisionCache0[addr].flag == 0)
                        {
                            rasterizedCollisionCache0[addr].flag = 255;
                            edgePts0.points.Add(new Short2(pos.x, pos.y));

                            edgePts0.boundMin.x = Utils.Min(edgePts0.boundMin.x, pos.x);
                            edgePts0.boundMin.y = Utils.Min(edgePts0.boundMin.y, pos.y);

                            edgePts0.boundMax.x = Utils.Max(edgePts0.boundMax.x, pos.x);
                            edgePts0.boundMax.y = Utils.Max(edgePts0.boundMax.y, pos.y);
                        }
                    }
                }
            }


            if (edgePts0.points.Count > 0)
            {
                Debug.Assert(edgePts1.points.Count > 0, "Sanity check failed");
            }

            if (edgePts1.points.Count > 0)
            {
                Debug.Assert(edgePts0.points.Count > 0, "Sanity check failed");
            }

            if (edgePts0.points.Count == 0)
            {
                return null;
            }

            Graph.Edge edge = new Graph.Edge(node0, node1);
            edge.splitA = edgePts0;
            edge.splitB = edgePts1;

            //add edge to both nodes
            node0.edges.Add(edge);
            node1.edges.Add(edge);
            return edge;
        }


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        static bool ValidateConnectedComponentInGraph(Graph graph)
        {
            HashSet<Graph.Node> visitedNodes = new HashSet<Graph.Node>();
            Queue<Graph.Node> nodesToVisit = new Queue<Graph.Node>(128);


            for (int subGraphIndex = 0; subGraphIndex < graph.roots.Count; subGraphIndex++)
            {
                visitedNodes.Clear();
                nodesToVisit.Clear();

                Graph.Node root = graph.roots[subGraphIndex];
                if (root.connectedComponentId != subGraphIndex)
                {
                    Debug.LogError(string.Format("Validate failed: Invalid root {0} connectedComponentId", root.nodeIndex));
                    return false;
                }
                nodesToVisit.Enqueue(root);

                while (nodesToVisit.Count > 0)
                {
                    Graph.Node node = nodesToVisit.Dequeue();
                    visitedNodes.Add(node);

                    if (node.connectedComponentId != subGraphIndex)
                    {
                        Debug.LogError(string.Format("Validate failed: Invalid node {0} connectedComponentId", node.nodeIndex));
                        return false;
                    }

                    for (int edgeIndex = 0; edgeIndex < node.edges.Count; edgeIndex++)
                    {
                        Graph.Edge edge = node.edges[edgeIndex];
                        if (edge.isBroken)
                        {
                            continue;
                        }

                        Graph.Node targetNode = edge.GetTarget(node);
                        if (visitedNodes.Contains(targetNode) == false)
                        {
                            nodesToVisit.Enqueue(targetNode);
                        }
                    } // edgeIndex
                } // nodesToVisit.Count > 0


                for (int nodeIndex = 0; nodeIndex < graph.nodes.Length; nodeIndex++)
                {
                    Graph.Node node = graph.nodes[nodeIndex];
                    if (node.connectedComponentId != subGraphIndex)
                    {
                        continue;
                    }

                    if (visitedNodes.Contains(node) == false)
                    {
                        Debug.LogError(string.Format("Validate failed: Node {0}, subgraph {1} not found in visited nodes", node.nodeIndex, subGraphIndex));
                        return false;
                    }
                }
            }


            return true;
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        static void FindConnectedComponentInGraph(Graph graph)
        {
            // find connected components in graph
            //---------------------------------------------------------
            int connectedComponentId = 0;
            Queue<Graph.Node> nodesToVisit = new Queue<Graph.Node>(128);
            List<Graph.Node> roots = new List<Graph.Node>();
            while (true)
            {
                //find root
                nodesToVisit.Clear();

                for (int nodeIndex = 0; nodeIndex < graph.nodes.Length; nodeIndex++)
                {
                    Graph.Node node = graph.nodes[nodeIndex];
                    if (node.connectedComponentId == -1)
                    {
                        node.connectedComponentId = -2;
                        nodesToVisit.Enqueue(node);
                        roots.Add(node);
                        break;
                    }
                }

                //no new root found
                if (nodesToVisit.Count == 0)
                {
                    break;
                }

                while (nodesToVisit.Count > 0)
                {
                    Graph.Node node = nodesToVisit.Dequeue();
                    Debug.Assert(node.connectedComponentId == -2, "Sanity check failed!");
                    node.connectedComponentId = connectedComponentId;

                    for (int edgeIndex = 0; edgeIndex < node.edges.Count; edgeIndex++)
                    {
                        if (node.edges[edgeIndex].isBroken)
                        {
                            continue;
                        }

                        Graph.Node targetNode = node.edges[edgeIndex].GetTarget(node);

                        //skip if we already visited
                        if (targetNode.connectedComponentId != -1)
                        {
                            continue;
                        }

                        targetNode.connectedComponentId = -2;
                        nodesToVisit.Enqueue(targetNode);
                    }
                }

                connectedComponentId++;
            }

            graph.roots = roots;
            //---------------------------------------------------------
        }


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        bool CreateGraphConnections(Graph graph)
        {
            if (graph == null || graph.nodes.Length == 0)
            {
                return false;
            }

            List<ApproximateCollisionPair> approxCollidingPairs = new List<ApproximateCollisionPair>(256);

            Debug.LogInfo("Broad phase");
            int maxWidth = 0;
            int maxHeight = 0;

            for (int i = 0; i < graph.nodes.Length; i++)
            {
                Graph.Node a = graph.nodes[i];

                int nodeWidth = (a.boundMax.x - a.boundMin.x) + 1;
                if (maxWidth < nodeWidth)
                {
                    maxWidth = nodeWidth;
                }

                int nodeHeight = (a.boundMax.y - a.boundMin.y) + 1;
                if (maxHeight < nodeHeight)
                {
                    maxHeight = nodeHeight;
                }

                for (int j = (i + 1); j < graph.nodes.Length; j++)
                {
                    Graph.Node b = graph.nodes[j];

                    //nodes contains only base layers don't have a edges (can connect to any node by design)
                    if (a.srcMasksId == baseLayerMask || b.srcMasksId == baseLayerMask)
                    {
                        continue;
                    }

                    if (Utils.IsBoxIntersected(a.boundMin, a.boundMax, b.boundMin, b.boundMax, 1))
                    {
                        approxCollidingPairs.Add(new ApproximateCollisionPair(a, b));
                    }
                }
            }

            if (maxWidth == 0 || maxHeight == 0)
            {
                return false;
            }

            Debug.LogInfo(string.Format("Input nodes = {0}", graph.nodes.Length));
            Debug.LogInfo(string.Format("Approximate colliding pairs = {0}", approxCollidingPairs.Count));

            Debug.LogInfo("Narrow phase");

            //Debug.Log("maxWidth = " + maxWidth);
            //Debug.Log("maxHeight = " + maxWidth);

            const Int16 overlapSizeX2 = (overlapSize * 2);

            rasterizedCollisionCache0 = new NodeCollisionCacheItem[(maxWidth + overlapSizeX2) * (maxHeight + overlapSizeX2)];
            rasterizedCollisionCache1 = new NodeCollisionCacheItem[(maxWidth + overlapSizeX2) * (maxHeight + overlapSizeX2)];

            int collidedPairsCount = 0;

            for (int j = 0; j < approxCollidingPairs.Count; j++)
            {
                Graph.Edge edge = FindSharedEdge(approxCollidingPairs[j]);
                if (edge != null)
                {
                    collidedPairsCount++;
                }
            }

            Debug.LogInfo(string.Format("Collided pairs = {0}", collidedPairsCount));


            FindConnectedComponentInGraph(graph);

            return true;
        }


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        static Graph.Node GetFirstElement(HashSet<Graph.Node> set)
        {
            var enumerator = set.GetEnumerator();
            bool res = enumerator.MoveNext();
            Debug.Assert(res, "Bad logic, set can't be empty");
            Graph.Node firstElement = enumerator.Current;
            return firstElement;
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        bool SolveGraphForNClusters(Graph graph)
        {
            //DebugUtils.SaveAsSingleGraph("beforesolve.dot", graph);

            int combinedError = 0;
            int solveStep = 0;
            for (solveStep = 0; solveStep < int.MaxValue; solveStep++)
            {
                int subGraphsCount = graph.roots.Count;
                int newConnectedComponentId = subGraphsCount;
                int alreadySolved = 0;

                Debug.LogInfo(string.Format("Step {0}, subraphs {1}", solveStep, subGraphsCount));

                for (int subGraphIndex = 0; subGraphIndex < subGraphsCount; subGraphIndex++)
                {
                    //Debug.LogInfo(string.Format("Subgraph index {0}", subGraphIndex));

                    SplitGraph.Solution solution = SplitGraph.Solve(graph.nodes, subGraphIndex, job.maxLocalLayersCount);
                    if (solution != null && solution.error > 0)
                    {
                        Debug.LogInfo(string.Format("Subgraph {0}, error = {1}", subGraphIndex, solution.error));

                        combinedError += solution.error;


                        /*
						Debug.LogInfo("--- setA ---");
						foreach (Graph.Node node in solution.setA)
						{
							Debug.LogInfo(string.Format("node_{0} = {1}", node.nodeIndex, node.connectedComponentId));
						}
						*/

                        //rebuild connected component in setB 
                        //--------------------------------------------------------------
                        Queue<Graph.Node> nodesToVisit = new Queue<Graph.Node>(128);
                        List<Graph.Node> newRoots = new List<Graph.Node>();

                        HashSet<Graph.Node> workingSet = new HashSet<Graph.Node>(solution.setB);
                        while (workingSet.Count > 0)
                        {
                            Graph.Node newRoot = GetFirstElement(workingSet);
                            nodesToVisit.Enqueue(newRoot);
                            newRoots.Add(newRoot);
                            while (nodesToVisit.Count > 0)
                            {
                                Graph.Node curNode = nodesToVisit.Dequeue();
                                workingSet.Remove(curNode);
                                curNode.connectedComponentId = newConnectedComponentId;

                                for (int edgeIndex = 0; edgeIndex < curNode.edges.Count; edgeIndex++)
                                {
                                    Graph.Node tgtNode = curNode.edges[edgeIndex].GetTarget(curNode);

                                    // already processed this node
                                    if (workingSet.Contains(tgtNode) == false)
                                    {
                                        continue;
                                    }
                                    nodesToVisit.Enqueue(tgtNode);
                                }
                            }

                            newConnectedComponentId++;
                        }
                        //--------------------------------------------------------------


                        /*
						Debug.LogInfo(string.Format("--- setB --- (subgraphs = {0})", newRoots.Count));
						foreach (Graph.Node node in solution.setB)
						{
							Debug.LogInfo(string.Format("node_{0} = {1}", node.nodeIndex, node.connectedComponentId));
						}

						*/
                        foreach (Graph.Edge edge in solution.edgesToSplit)
                        {
                            edge.isBroken = true;
                            //Debug.LogInfo(string.Format("{0} <-> {1}", edge.a.nodeIndex, edge.b.nodeIndex));
                        }



                        //add new roots
                        graph.roots.AddRange(newRoots);

                        //fixup old root
                        graph.roots[subGraphIndex] = GetFirstElement(solution.setA);

                        Debug.Log(string.Format("      {0} graph(s) was added. Graphs count {1}", newRoots.Count, graph.roots.Count));

                        /*
                                                if (!ValidateConnectedComponentInGraph(graph))
                                                {
                                                    DebugUtils.SaveAsSingleGraph("broken.dot", graph);
                                                    Debug.Assert(false, "Solve error!");
                                                }
                         */
                    }
                    else
                    {
                        alreadySolved++;
                    }
                }

                if (alreadySolved == subGraphsCount)
                {
                    break;
                }
            }


            Debug.Log(string.Format("Solved by {0} steps, total error = {1}", solveStep, combinedError));
            return true;
        }


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        bool CreateGraphClusters()
        {
            if (!SolveGraphForNClusters(matGraph))
            {
                return false;
            }

            Debug.Log(string.Format("    {0} clusters found", matGraph.roots.Count));
            return true;
        }



        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        bool CreateIndirectionMap()
        {
            localPalettes.Clear();

            int width = downsampledBaseLayer.width;
            int height = downsampledBaseLayer.height;

            Graph graph = matGraph;

            int subGraphsCount = graph.roots.Count;


            // find base mask color from color_id map for every graph
            Color32[] subGraphBaseColors = new Color32[subGraphsCount];
            for (int subGraphId = 0; subGraphId < subGraphsCount; subGraphId++)
            {
                for (int nodeIndex = 0; nodeIndex < graph.nodes.Length; nodeIndex++)
                {
                    Graph.Node node = graph.nodes[nodeIndex];
                    if (node.connectedComponentId != subGraphId)
                    {
                        continue;
                    }

                    Short2 pos = node.data[0];
                    int addr = (node.boundMin.y + pos.y) * width + (node.boundMin.x + pos.x);
                    subGraphBaseColors[subGraphId] = downsampledBaseLayer.texels[addr];
                    break;
                }
            }

            // find graph combined layers ID
            UInt64[] subGraphCombinedIds = new UInt64[subGraphsCount];
            for (int subGraphId = 0; subGraphId < subGraphsCount; subGraphId++)
            {
                UInt64 subGraphCombinedId = 0;

                for (int nodeIndex = 0; nodeIndex < graph.nodes.Length; nodeIndex++)
                {
                    Graph.Node node = graph.nodes[nodeIndex];
                    if (node.connectedComponentId != subGraphId)
                    {
                        continue;
                    }

                    subGraphCombinedId |= node.srcMasksId;
                }

                int idsCount = Utils.BitCount(subGraphCombinedId);
                Debug.Assert(idsCount <= job.maxLocalLayersCount, "Sanity check failed. Graph still not solved");
                subGraphCombinedIds[subGraphId] = subGraphCombinedId;
            } // subGraphId


            // Try to merge subgraphs
            List<Tuple<UInt64, Color32>> mergedIds = new List<Tuple<UInt64, Color32>>(subGraphsCount);
            for (int subGraphId = 0; subGraphId < subGraphCombinedIds.Length; subGraphId++)
            {
                UInt64 id = subGraphCombinedIds[subGraphId];
                Color32 baseColor = subGraphBaseColors[subGraphId];

                bool isMergeFailed = true;

                for (int j = 0; j < mergedIds.Count; j++)
                {
                    if (Utils.IsSameRGB(mergedIds[j].Item2, baseColor))
                    {
                        UInt64 combinedId = (mergedIds[j].Item1 | id);
                        if (Utils.BitCount(combinedId) <= job.maxLocalLayersCount)
                        {
                            mergedIds[j] = new Tuple<UInt64, Color32>(combinedId, baseColor);
                            isMergeFailed = false;
                            break;
                        }
                    }
                }

                if (isMergeFailed)
                {
                    mergedIds.Add(new Tuple<UInt64, Color32>(id, baseColor));
                }
            } // subGraphId


            // create local palettes from merged ids
            foreach (Tuple<UInt64, Color32> item in mergedIds)
            {
                LocalPalette localPalette = new LocalPalette();
                localPalette.sourceIds = Utils.BitsToList(item.Item1);
                localPalette.ids = item.Item1;
                localPalette.mask = new byte[width * height];
                localPalette.maskWidth = width;
                localPalette.maskHeight = height;
                localPalette.baseLayerColor = item.Item2;
                Utils.SetArray<byte>(localPalette.mask, 0);
                localPalettes.Add(localPalette);
            }

            //----------------------------------------------------------------------
            //rasterize palette to mask
            for (int subGraphId = 0; subGraphId < subGraphsCount; subGraphId++)
            {
                UInt64 subgraphId = subGraphCombinedIds[subGraphId];
                Color32 subgraphBaseColor = subGraphBaseColors[subGraphId];

                // find the appropriate mask
                LocalPalette palette = null;
                foreach (LocalPalette currentPalette in localPalettes)
                {
                    if (((currentPalette.ids & subgraphId) == subgraphId) && (Utils.IsSameRGB(currentPalette.baseLayerColor, subgraphBaseColor)))
                    {
                        palette = currentPalette;
                        break;
                    }
                }

                Debug.Assert(palette != null, "Sanity check failed");

                for (int nodeIndex = 0; nodeIndex < graph.nodes.Length; nodeIndex++)
                {
                    Graph.Node node = graph.nodes[nodeIndex];
                    if (node.connectedComponentId != subGraphId)
                    {
                        continue;
                    }

                    for (int i = 0; i < node.data.Count; i++)
                    {
                        Short2 pos = node.data[i];
                        int addr = (node.boundMin.y + pos.y) * width + (node.boundMin.x + pos.x);
                        Debug.Assert(palette.mask[addr] == 0, "Sanity check failed. Palettes can't overlap each other");
                        palette.mask[addr] = 255;
                    }

                }
            } // subGraphId
              //rasterize palette to mask
              //----------------------------------------------------------------------



            // create indirection map and check for mask intersections
            //----------------------------------------------------------------------------------
            Debug.Assert(localPalettes.Count >= 1 && localPalettes.Count < 254, "Sanity check failed");

            //create indirection map
            indirectionMap = CpuTexture2D.CreateEmpty(width, height, Color32.black);

            for (int i = 0; i < localPalettes.Count; i++)
            {
                LocalPalette palette = localPalettes[i];

                Debug.Assert(width == palette.maskWidth, "Sanity check failed");
                Debug.Assert(height == palette.maskHeight, "Sanity check failed");

                for (int y = 0; y < palette.maskHeight; y++)
                {
                    for (int x = 0; x < palette.maskWidth; x++)
                    {
                        int addr = y * width + x;
                        if (palette.mask[addr] != 0)
                        {
                            Debug.Assert(indirectionMap.texels[addr].r == 0, "Sanity check failed. Palettes can't overlap each other");
                            indirectionMap.texels[addr].r = (byte)(i + 1);
                        }
                    }
                }
            }

            return true;
        }


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        bool CreateWeightsMap()
        {
            int layersCount = sourceLayers.Length;

            Color32 zero = new Color32(0, 0, 0, 0);
            weightsMap = CpuTexture2D.CreateEmpty(sourceWidth, sourceHeight, zero);

            bool hasErrors = false;
            CpuTexture2D errorsTex = CpuTexture2D.CreateEmpty(sourceWidth, sourceHeight, Color32.black);

            int blockSize = 1 << job.maxMipLevel;

            // filtering error visualization
            for (int nodeIndex = 0; nodeIndex < matGraph.nodes.Length; nodeIndex++)
            {
                Graph.Node node = matGraph.nodes[nodeIndex];

                for (int edgeIndex = 0; edgeIndex < node.edges.Count; edgeIndex++)
                {
                    Graph.Edge edge = node.edges[edgeIndex];

                    if (edge.isBroken == false)
                    {
                        continue;
                    }

                    hasErrors = true;

                    Graph.EdgePoints edgePoints = edge.GetMyEdgePoints(node);

                    for (int pointIndex = 0; pointIndex < edgePoints.points.Count; pointIndex++)
                    {
                        Short2 pos = edgePoints.points[pointIndex];

                        int px = node.boundMin.x + pos.x;
                        int py = node.boundMin.y + pos.y;

                        int ox = px << job.maxMipLevel;
                        int oy = py << job.maxMipLevel;

                        for (int y = oy; y < (oy + blockSize); y++)
                        {
                            for (int x = ox; x < (ox + blockSize); x++)
                            {
                                int addr = y * sourceWidth + x;
                                errorsTex.texels[addr] = Color32.yellow;
                            }
                        }

                    } // pointIndex
                } // edgeIndex
            } // nodeIndex


            float[] currentValue = new float[5];

            // recalculate alpha blend as weighted sum
            for (int y = 0; y < sourceHeight; y++)
            {
                for (int x = 0; x < sourceWidth; x++)
                {
                    int addr = y * sourceWidth + x;

                    int imX = (x >> job.maxMipLevel);
                    int imY = (y >> job.maxMipLevel);
                    int paletteIndex = indirectionMap.Load(imX, imY).r;

                    if (paletteIndex == 0)
                    {
                        continue;
                    }

                    bool insideMaterial = false;
                    if (matBaseLayer.texels[addr].a != 0)
                    {
                        insideMaterial = true;
                    }

                    paletteIndex = paletteIndex - 1;
                    LocalPalette palette = localPalettes[paletteIndex];

                    Utils.SetArray<float>(currentValue, 0.0f);
                    int valueIndex = 0;

                    float totalWeight = 0.0f;
                    float mulAcc = 1.0f;
                    for (int layerIndex = 0; layerIndex < layersCount; layerIndex++)
                    {
                        UInt64 layerMask = Utils.GetLayerMask(layerIndex);

                        byte maskValue = sourceLayers[layerIndex].texels[addr].r;
                        float maskAlpha = (float)maskValue / 255.0f;
                        float absLayerWeight = maskAlpha * mulAcc;
                        mulAcc = mulAcc * (1.0f - maskAlpha);

                        if ((palette.ids & layerMask) == layerMask)
                        {
                            totalWeight += absLayerWeight;
                            currentValue[valueIndex] = absLayerWeight;
                            valueIndex++;
                        }
                        else
                        {
                            // significant weight has been removed inside mapping
                            if (absLayerWeight > 0.0f && insideMaterial)
                            {
                                hasErrors = true;
                                errorsTex.texels[addr] = Color32.red;
                            }
                        }
                    } // layers iterator

                    // force add base layer (alpha = 1.0f = mulAcc)
                    bool hasBaseLayer = ((palette.ids & baseLayerMask) == baseLayerMask);

                    if (hasBaseLayer)
                    {
                        totalWeight += mulAcc;
                        currentValue[currentValue.Length - 1] = mulAcc;
                        valueIndex++;
                    }

                    //normalize
                    if (totalWeight > 0.0f)
                    {
                        for (int i = 0; i < currentValue.Length; i++)
                        {
                            currentValue[i] /= totalWeight;
                        }
                    }

                    if (job.maxLocalLayersCount >= 2)
                    {
                        weightsMap.texels[addr].r = (byte)(Mathf.Clamp01(currentValue[0]) * 255.0f);
                    }

                    if (job.maxLocalLayersCount >= 3)
                    {
                        weightsMap.texels[addr].g = (byte)(Mathf.Clamp01(currentValue[1]) * 255.0f);
                    }

                    if (job.maxLocalLayersCount >= 4)
                    {
                        weightsMap.texels[addr].b = (byte)(Mathf.Clamp01(currentValue[2]) * 255.0f);
                    }

                    if (job.maxLocalLayersCount >= 5)
                    {
                        weightsMap.texels[addr].a = (byte)(Mathf.Clamp01(currentValue[3]) * 255.0f);
                    }

                } //x
            } //y


            if (hasErrors)
            {
                errors = errorsTex;
            }
            else
            {
                errors = null;
            }

            return true;
        }


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        bool SaveResults()
        {
            TgaFormat.Save(job.indirectMapFile, indirectionMap.texels, false, indirectionMap.width, indirectionMap.height);

            bool needAlpha = (job.maxLocalLayersCount == 5);
            TgaFormat.Save(job.weightsMapFile, weightsMap.texels, needAlpha, weightsMap.width, weightsMap.height);

            LayersProcessorResult res = new LayersProcessorResult();
            res.clusters = new List<LayersProcessorResultCluster>();

            for (int j = 0; j < localPalettes.Count; j++)
            {
                LayersProcessorResultCluster resCluster = new LayersProcessorResultCluster();
                //resCluster.baseLayer = 
                resCluster.layers = new List<string>();

                LocalPalette palette = localPalettes[j];
                foreach (int layerID in palette.sourceIds)
                {
                    if (layerID == 63)
                    {
                        // blend to base layer
                        // since base layer depends from color_id map, we need to find appropriate base layer
                        int baseId = -1;
                        for (int n = 0; n < uniqueColors.Count; n++)
                        {
                            if (Utils.IsSameRGB(uniqueColors[n], palette.baseLayerColor))
                            {
                                baseId = n;
                                break;
                            }
                        }

                        Debug.Assert(baseId >= 0, "Can't find color coded id");
                        resCluster.baseLayer = string.Format("base_{0:X2}{1:X2}{2:X2}", palette.baseLayerColor.r, palette.baseLayerColor.g, palette.baseLayerColor.b);
                    }
                    else
                    {
                        resCluster.layers.Add(job.sourceLayers[layerID]);
                    }
                }
                res.clusters.Add(resCluster);
            }

            string resultJson = JsonConvert.SerializeObject(res, Formatting.Indented);
            File.WriteAllText(job.descriptionFile, resultJson);
            return true;
        }


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public bool ExecuteJob(LayersProcessorJob _job)
        {
            const bool debugDump = false;

            Debug.Log("Extract local layers");

            job = _job;

            if (!LoadColorIdMap())
            {
                return false;
            }

            if (!LoadSourceLayers())
            {
                return false;
            }

            if (!BuildDownsampledMasks())
            {
                return false;
            }

            // debug
            if (debugDump)
            {
                TgaFormat.Save("dbg0__dnbase.tga", downsampledBaseLayer.texels, true, downsampledBaseLayer.width, downsampledBaseLayer.height);

                for (int i = 0; i < downsampledLayers.Length; i++)
                {
                    TgaFormat.Save(string.Format("dbg0__dn_{0}.tga", i), downsampledLayers[i].texels, false, downsampledLayers[i].width, downsampledLayers[i].height);
                }
            }

            if (debugDump)
            {
                DumpBaseConnnectedRegions();
            }



            if (!SelectAndIntersectLayers())
            {
                return false;
            }

            // debug
            if (debugDump)
            {
                // intersection bitmap
                DebugUtils.SaveIntersectionBitmap("dbg1__s_intersects", matIntersections);
            }


            if (!BuildConnectedRegions())
            {
                return false;
            }

            // debug
            if (debugDump)
            {
                // graph nodes
                for (int nodeIndex = 0; nodeIndex < matGraph.nodes.Length; nodeIndex++)
                {
                    Graph.Node node = matGraph.nodes[nodeIndex];
                    DebugUtils.SaveGraphNode(string.Format("dbg2__m_n{0}_src{1}.tga", nodeIndex, node.GetSourceMasksString()), node);
                }
            }


            if (!BuildGraphConnections())
            {
                return false;
            }

            if (!CreateGraphClusters())
            {
                return false;
            }

            // debug
            if (debugDump)
            {
                // solved graphs
                DebugUtils.SaveDotGraph("dbg3__m{0}.dot", matGraph);
            }

            if (!CreateIndirectionMap())
            {
                return false;
            }

            if (!CreateWeightsMap())
            {
                return false;
            }


            if (!SaveResults())
            {
                return false;
            }

            // debug
            if (debugDump)
            {
                // local palettes
                for (int i = 0; i < localPalettes.Count; i++)
                {
                    LocalPalette palette = localPalettes[i];
                    DebugUtils.SavePaletteMask(string.Format("dbg4__palettemask{0}_ids_{1}.tga", i, Utils.BitsToString(palette.ids)), palette.mask, palette.maskWidth, palette.maskHeight);
                }
            }

            //dump errors
            if (errors != null)
            {
                Debug.LogWarning("Save 'errors.tga'");
                TgaFormat.Save("errors.tga", errors.texels, false, errors.width, errors.height);
            }

            return true;
        }
    }

}