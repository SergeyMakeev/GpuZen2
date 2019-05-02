using System;
using UnityEngine;
using System.Collections.Generic;



namespace LM
{
    enum DetailTexture
    {
        TEX_0 = 0,
        TEX_1 = 1,
        TEX_2 = 2,
        TEX_3 = 3,
        TEX_4 = 4,
        TEX_5 = 5,
        TEX_6 = 6,
        TEX_7 = 7,
        TEX_8 = 8,
        TEX_9 = 9
    }


    enum DebugMode
    {
        NONE = 0,
        RED_ALBEDO = 1,
        WHITE_ALBEDO = 2,
        LOCAL_NORMALS_ONLY = 3,
        RAW_LOCAL_ALBEDO_ONLY = 4,
        RAW_LOCAL_SMOOTHNES_ONLY = 5,
        RAW_LOCAL_METALLIC_ONLY = 6,
        LOCAL_ALBEDO_ONLY = 7,
        LOCAL_SMOOTHNES_ONLY = 8,
        LOCAL_METALLIC_ONLY = 9,

    }


    [Serializable]
    public class MaterialTemplate : ScriptableObject
    {

        struct StructuredBufferElement
        {
            public Vector4 albedoColor;
            public Vector4 emissionColor;

            public float offset;

            public float albedoAlpha;
            public float surfaceAlpha;
            public float normalsAlpha;

            public float glossiness;
            public float metallic;

            public float surfaceIndex;
            public Vector4 surfaceTilingMtx;

            public float detailDiffuseContrib;
            public float detailGlossinessContrib;
            public float detailMetallicContrib;

            public float normalsIndex;
            public Vector4 normalsTilingMtx;

            public float detailNormalsContrib;

            public float globalNormalScale;
        }


        [Serializable]
        public class LayerTemplate
        {
            public string layerName;
            public string layerKey;
            public Material material;

            [NonSerialized]
            public Material materialRuntimeOverride = null;

            public float albedoAlpha = 1.0f;
            public float surfaceAlpha = 1.0f;
            public float normalsAlpha = 1.0f;
            public float globalAlpha = 1.0f;

            [NonSerialized]
            public float globalAlphaRuntime = 1.0f;

            public float globalOffset = 0.0f;
            public float globalNormalScale = 1.0f;
            public float surfaceRotation = 0.0f;
            public float normalsRotation = 0.0f;

            [HideInInspector]
            public int[] targetSlots;
        }

        public List<LayerTemplate> layers = new List<LayerTemplate>();

        public Color color = Color.blue;

        public string sourceDescriptionXml;

        public Texture2D normalMap;
        public Texture2D indirectionMap;
        public Texture2D weightsMap;
        public Texture2D ambientMap;
        public Texture2D alphaMap;

        public bool isAlphaTested = false;
        public float ambientCorrection = 1.0f;

        public Texture2DArray textureArrayNormals;
        public Texture2DArray textureArraySurface;

        private ComputeBuffer gpuBuffer = null;


        public MaterialTemplate()
        {
            //Debug.Log("Constructor");
        }


        //TODO: Inherit from IDisposable, or make global cache
        //
        // GarbageCollector disposing of ComputeBuffer allocated in F:\Work\TestUnityProject\LayeredMaterials\Assets\LayeredMaterials\Scripts\MaterialTemplate.cs at line 265. Please use ComputeBuffer.Release() or .Dispose() to manually release the buffer.
        //
        void OnDestroy()
        {
            ReleaseGpuBuffer();
        }

        public void ForceUpdateParametersToUnityMaterial(Material mat)
        {
            if (gpuBuffer == null)
            { 
                CreateComputeBuffer();
            }

            ApplyParametersToUnityMaterial(mat, null);
        }

        public void ReleaseGpuBuffer()
        {
            if (gpuBuffer != null)
            {
                gpuBuffer.Release();
                gpuBuffer = null;
            }
        }

        public void UpdateComputeBuffer()
        {
            if (gpuBuffer == null)
            {
                //Debug.LogError("UpdateComputeBuffer - failed");
                return;
            }


            //TODO
            const int materialParamsCount = 512;

            int slotsUpdated = 0;

            StructuredBufferElement[] cpuBuffer = new StructuredBufferElement[materialParamsCount];
            for (int i = 0; i < cpuBuffer.Length; i++)
            {
                cpuBuffer[i].albedoColor = Vector4.zero;
                cpuBuffer[i].emissionColor = Vector4.zero;
                cpuBuffer[i].glossiness = 0.0f;
                cpuBuffer[i].metallic = 0.0f;
                cpuBuffer[i].surfaceIndex = 0.0f;
                cpuBuffer[i].surfaceTilingMtx = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                cpuBuffer[i].normalsIndex = 0.0f;
                cpuBuffer[i].normalsTilingMtx = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                cpuBuffer[i].globalNormalScale = 1.0f;
                cpuBuffer[i].detailDiffuseContrib = 0.0f;
                cpuBuffer[i].detailGlossinessContrib = 0.0f;
                cpuBuffer[i].detailMetallicContrib = 0.0f;
                cpuBuffer[i].detailNormalsContrib = 0.0f;
            }

            foreach (LayerTemplate layerTemplate in layers)
            {
                if (layerTemplate.material == null)
                {
                    continue;
                }

                Material mat = layerTemplate.material;
                if (layerTemplate.materialRuntimeOverride)
                {
                    mat = layerTemplate.materialRuntimeOverride;
                }

                if (mat.shader.name != "Custom/PrototypeSingleShader")
                {
                    Debug.LogError(mat.shader.name);
                    continue;
                }

                Color lAlbedo = mat.GetColor("_AlbedoColor");
                Color lEmmision = mat.GetColor("_EmissionColor");
                float lGlossines = mat.GetFloat("_Glossiness");
                float lMetallic = mat.GetFloat("_Metallic");

                float lSurfaceIndex = mat.GetFloat("_SurfaceIndex");
                float lSurfaceTilingU = mat.GetFloat("_SurfaceTilingU");
                float lSurfaceTilingV = mat.GetFloat("_SurfaceTilingV");

                float lNormalsIndex = mat.GetFloat("_NormalsIndex");
                float lNormalsTilingU = mat.GetFloat("_NormalsTilingU");
                float lNormalsTilingV = mat.GetFloat("_NormalsTilingV");

                float lDetailDiffuseContrib = mat.GetFloat("_DetailDiffuseContrib");
                float lDetailGlossinessContrib = mat.GetFloat("_DetailGlossinessContrib");
                float lDetailMetallicContrib = mat.GetFloat("_DetailMetallicContrib");
                float lDetailNormalsContrib = mat.GetFloat("_DetailNormalsContrib");

                float lSurfaceTilingRotation = mat.GetFloat("_SurfaceTilingRotation");
                float lNormalsTilingRotation = mat.GetFloat("_NormalsTilingRotation");

                //Debug.Log("upd " + lAlbedo);


                //public float surfaceRotation = 0.0f;
                //public float normalsRotation = 0.0f;


                for (int i = 0; i < layerTemplate.targetSlots.Length; i++)
                {
                    int slotIndex = layerTemplate.targetSlots[i];

                    cpuBuffer[slotIndex].albedoColor = lAlbedo;
                    cpuBuffer[slotIndex].emissionColor = lEmmision;
                    cpuBuffer[slotIndex].glossiness = lGlossines;
                    cpuBuffer[slotIndex].metallic = lMetallic;
                    cpuBuffer[slotIndex].surfaceIndex = lSurfaceIndex;

                    float radSurfaceRotation = (lSurfaceTilingRotation + layerTemplate.surfaceRotation) * (3.141592f / 180.0f);
                    float sin = Mathf.Sin(radSurfaceRotation);
                    float cos = Mathf.Cos(radSurfaceRotation);
                    cpuBuffer[slotIndex].surfaceTilingMtx = new Vector4(cos * lSurfaceTilingU, -sin * lSurfaceTilingU, sin * lSurfaceTilingV, cos * lSurfaceTilingV);

                    cpuBuffer[slotIndex].normalsIndex = lNormalsIndex;

                    float radNormalsRotation = (lNormalsTilingRotation + layerTemplate.normalsRotation) * (3.141592f / 180.0f);
                    sin = Mathf.Sin(radNormalsRotation);
                    cos = Mathf.Cos(radNormalsRotation);
                    cpuBuffer[slotIndex].normalsTilingMtx = new Vector4(cos * lNormalsTilingU, -sin * lNormalsTilingU, sin * lNormalsTilingV, cos * lNormalsTilingV);

                    cpuBuffer[slotIndex].albedoAlpha = layerTemplate.albedoAlpha * layerTemplate.globalAlpha * layerTemplate.globalAlphaRuntime;
                    cpuBuffer[slotIndex].surfaceAlpha = layerTemplate.surfaceAlpha * layerTemplate.globalAlpha * layerTemplate.globalAlphaRuntime;
                    cpuBuffer[slotIndex].normalsAlpha = layerTemplate.normalsAlpha * layerTemplate.globalAlpha * layerTemplate.globalAlphaRuntime;
                    cpuBuffer[slotIndex].offset = layerTemplate.globalOffset;

                    cpuBuffer[slotIndex].globalNormalScale = layerTemplate.globalNormalScale;

                    cpuBuffer[slotIndex].detailDiffuseContrib = lDetailDiffuseContrib;
                    cpuBuffer[slotIndex].detailGlossinessContrib = lDetailGlossinessContrib;
                    cpuBuffer[slotIndex].detailMetallicContrib = lDetailMetallicContrib;
                    cpuBuffer[slotIndex].detailNormalsContrib = lDetailNormalsContrib;

                    slotsUpdated++;
                }
            }

            gpuBuffer.SetData(cpuBuffer);

            //Debug.LogWarning("UPDATE END, slots " + slotsUpdated);
        }

        public void CreateComputeBuffer()
        {
            if (gpuBuffer != null)
            {
                //Debug.LogWarning("RELEASE");
                gpuBuffer.Release();
                gpuBuffer = null;
            }

            //Debug.LogWarning("CREATE");
            const int materialParamsCount = 256;
            gpuBuffer = new ComputeBuffer(materialParamsCount, sizeof(float) * 29, ComputeBufferType.Default);

            UpdateComputeBuffer();
        }


        public void ApplyParametersToUnityMaterial(Material mat, MaterialPropertyBlock propBlock)
        {
            //Debug.LogWarning("ApplyParametersToUnityMaterial");

            if (gpuBuffer == null && layers.Count > 0)
            {
                //Debug.LogWarning("ApplyParametersToUnityMaterial - create compute buffer");
                CreateComputeBuffer();
            }

            int normalTexId = Shader.PropertyToID("_NormalTex");
            int indirectionTexId = Shader.PropertyToID("_IndirectionTex");
            int weightsTexId = Shader.PropertyToID("_WeightsTex");

            int surfaceTexId = Shader.PropertyToID("_TextureArraySurface");
            int normalsTexId = Shader.PropertyToID("_TextureArrayNormals");

            int ambientTexId = Shader.PropertyToID("_AmbientTex");

            int alphaTexId = Shader.PropertyToID("_AlphaTex");


            //int materialParamsId = Shader.PropertyToID("_MaterialParams");

            mat.SetTexture(normalTexId, normalMap);
            mat.SetTexture(indirectionTexId, indirectionMap);
            mat.SetTexture(weightsTexId, weightsMap);
            mat.SetTexture(ambientTexId, ambientMap);
            mat.SetTexture(alphaTexId, alphaMap);

            mat.SetTexture(surfaceTexId, textureArraySurface);
            mat.SetTexture(normalsTexId, textureArrayNormals);

            mat.SetFloat("ambientCorrection", ambientCorrection);

            mat.SetFloat("_isAlphaTest", isAlphaTested ? 1.0f : 0.0f);

            Debug.Assert(gpuBuffer != null, "Bad logic!!!");

            if (propBlock != null)
            {
                mat.SetBuffer("_MaterialParams", gpuBuffer);
                propBlock.SetBuffer("_MaterialParams", gpuBuffer);
            }
            else
            {
                mat.SetBuffer("_MaterialParams", gpuBuffer);
            }

            //Debug.LogWarning("SET " + mat.shader.name);

        }
    }

}