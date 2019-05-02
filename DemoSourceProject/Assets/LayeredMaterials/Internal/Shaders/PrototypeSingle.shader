Shader "Custom/PrototypeSingleShader" {
	Properties {


		_MainTex("Global normal map (preview)", 2D) = "bump" {}

		[NoScaleOffset] _textureArrayNormals("Details normals (texture array)", 2DArray) = "white" {}
		[NoScaleOffset] _textureArraySurface("Details surface (texture array)", 2DArray) = "white" {}

		[Space][HDR] _AlbedoColor("Albedo color", Color) = (1,1,1,1)
		[HDR] _EmissionColor("Emission color", Color) = (0,0,0,0)
		_Glossiness("Smoothness", Range(0,1)) = 0.0
		_Metallic("Metallic", Range(0,1)) = 0.0
		[Space][Enum(LM.DetailTexture)] _SurfaceIndex("Surface index", Float) = 0.0
		_SurfaceTilingU("Surface tiling U", Range(0,20)) = 8.0
		_SurfaceTilingV("Surface tiling V", Range(0,20)) = 8.0
		_SurfaceTilingRotation("Surface tiling Rotation", Range(0,180)) = 0.0

		_DetailDiffuseContrib("Diffuse contribution", Range(0,6)) = 0.0
		_DetailGlossinessContrib("Smoothness contribution", Range(0,6)) = 0.0
		_DetailMetallicContrib("Metallic contribution", Range(0,6)) = 0.0

		[Space][Enum(LM.DetailTexture)] _NormalsIndex("Normal index", Float) = 0.0
		_NormalsTilingU("Normals tiling U", Range(0,20)) = 8.0
		_NormalsTilingV("Normals tiling V", Range(0,20)) = 8.0
		_NormalsTilingRotation("Normals tiling Rotation", Range(0,180)) = 0.0
		_DetailNormalsContrib("Normals contribution", Range(0,2)) = 0.0



		[Space]_GlobalMaterialScale("Global material scale", Range(0,1)) = 1.0

		
		[Space][Enum(LM.DebugMode)] _DebugMode("Debug mode", Float) = 0.0

	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows

		#pragma target 4.0

		UNITY_DECLARE_TEX2D(_MainTex);

		UNITY_DECLARE_TEX2DARRAY(_textureArraySurface);
		UNITY_DECLARE_TEX2DARRAY(_textureArrayNormals);

		fixed4 _AlbedoColor;
		fixed3 _EmissionColor;
		half _Glossiness;
		half _Metallic;

		half _SurfaceIndex;
		half _SurfaceTilingU;
		half _SurfaceTilingV;
		half _SurfaceTilingRotation;

		half _NormalsIndex;
		half _NormalsTilingU;
		half _NormalsTilingV;
		half _NormalsTilingRotation;


		half _DetailDiffuseContrib;
		half _DetailGlossinessContrib;
		half _DetailMetallicContrib;
		half _DetailNormalsContrib;

		half _DebugMode;

		half _GlobalMaterialScale;

		struct Input
		{
			float2 uv_MainTex;
		};


		void surf (Input IN, inout SurfaceOutputStandard o)
		{
			//float3 whiteTexel = UNITY_SAMPLE_TEX2D(_MainTex, IN.uv_MainTex);

			//float3 normal = float3(0, 0, 1);
			float3 normal = UnpackNormal(UNITY_SAMPLE_TEX2D(_MainTex, IN.uv_MainTex));
			normal = lerp(float3(0, 0, 1), normal, _GlobalMaterialScale);
			
			if (_DebugMode == 3.0f)
			{
				normal = float3(0.0f, 0.0f, 1.0f);
			}

			float2 tileUV = IN.uv_MainTex - float2(0.5, 0.5);
			float radians = _SurfaceTilingRotation * (3.141592f / 180.0f);
			float _sin = sin(radians); 
			float _cos = cos(radians);
			float4 surfaceTilingMtx = float4(_cos*_SurfaceTilingU, -_sin*_SurfaceTilingU, _sin*_SurfaceTilingV, _cos*_SurfaceTilingV);


			float2 detailsUV = float2(dot(tileUV, surfaceTilingMtx.xy), dot(tileUV, surfaceTilingMtx.zw));

			float3 tex2DArrayCoords = float3(detailsUV, _SurfaceIndex);
			float4 surfaceDetails = UNITY_SAMPLE_TEX2DARRAY(_textureArraySurface, tex2DArrayCoords);

			float albedoDetails = ((surfaceDetails.r - 0.5) * _DetailDiffuseContrib) + 1.0;
			float glossDetails = ((surfaceDetails.g - 0.5) * _DetailGlossinessContrib) + 1.0;
			float metallicDetails = ((surfaceDetails.b - 0.5) * _DetailMetallicContrib) + 1.0;

			radians = _NormalsTilingRotation * (3.141592f / 180.0f);
			_sin = sin(radians); 
			_cos = cos(radians);
			float4 normalsTilingMtx = float4(_cos*_NormalsTilingU, -_sin*_NormalsTilingU, _sin*_NormalsTilingV, _cos*_NormalsTilingV);
			detailsUV = float2(dot(tileUV, normalsTilingMtx.xy), dot(tileUV, normalsTilingMtx.zw));
			tex2DArrayCoords = float3(detailsUV, _NormalsIndex);
			float2 normalDetails = UNITY_SAMPLE_TEX2DARRAY(_textureArrayNormals, tex2DArrayCoords).xy * 2.0 - 1.0;
			//float2 normalDetails = UnpackNormalmapRGorAG(UNITY_SAMPLE_TEX2DARRAY(_textureArrayNormals, tex2DArrayCoords)).xy;
			float3 localNormal = float3(normalDetails.xy, 0.0);
			localNormal *= _DetailNormalsContrib;

			//PERF: можно было бы просто сделать normal + localNormal, но тогда нормали сильно выгнутые слабее действуют (так ближе к реальному преобразованию)
			float3 finalNormal = (normal * sqrt(1.000001 - saturate(dot(localNormal, localNormal)))) + localNormal;
			finalNormal = normalize(finalNormal);

			o.Normal = finalNormal;


			o.Albedo = saturate(albedoDetails * _AlbedoColor).rgb;
			o.Alpha = 1.0;

			o.Metallic = saturate(metallicDetails * _Metallic);
			o.Smoothness = saturate(glossDetails * _Glossiness);
			o.Emission = _EmissionColor;

			if (_DebugMode == 1.0f)
			{
				o.Albedo = float3(1, 0, 0);
			}

			if (_DebugMode == 2.0f)
			{
				o.Albedo = float3(1, 1, 1);
			}


			if (_DebugMode == 4.0f)
			{
				o.Albedo = surfaceDetails.r;
			}

			if (_DebugMode == 5.0f)
			{
				o.Smoothness = surfaceDetails.g;
			}

			if (_DebugMode == 6.0f)
			{
				o.Metallic = surfaceDetails.b;
			}

			if (_DebugMode == 7.0f)
			{
				o.Albedo = saturate(0.5f * albedoDetails);
			}

			if (_DebugMode == 8.0f)
			{
				o.Smoothness = saturate(0.5f * glossDetails);
			}

			if (_DebugMode == 9.0f)
			{
				o.Metallic = saturate(0.5 * metallicDetails);
			}





		}
		ENDCG
	}
	//FallBack "Diffuse"
}
