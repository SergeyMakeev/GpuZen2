Shader "Custom/PrototypeLayeredShader" {

	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows


		#pragma multi_compile __ LOW_QUALITY

		#pragma target 4.0

		//#define LOW_QUALITY (1)



#ifdef LOW_QUALITY
#define MY_UNITY_SAMPLE_TEX2D(tex,coord) tex.Sample (sampler##tex,coord)
//#define MY_UNITY_SAMPLE_TEX2D(tex,coord) tex.SampleBias (sampler##tex, coord, 2.0)
#else
#define MY_UNITY_SAMPLE_TEX2D(tex,coord) tex.Sample (sampler##tex,coord)
#endif

#ifdef SHADER_API_D3D11

		UNITY_DECLARE_TEX2D(_MainTex);
		UNITY_DECLARE_TEX2D(_NormalTex);
		UNITY_DECLARE_TEX2D(_IndirectionTex);
		UNITY_DECLARE_TEX2D(_WeightsTex);
		UNITY_DECLARE_TEX2D(_AmbientTex);
		UNITY_DECLARE_TEX2D(_AlphaTex);

		

		UNITY_DECLARE_TEX2DARRAY(_TextureArraySurface);
		UNITY_DECLARE_TEX2DARRAY(_TextureArrayNormals);

		float ambientCorrection;
		float _isAlphaTest;


		//28 * float
		struct MaterialParams
		{
			float4 albedoColor;
			float4 emissionColor;

			float offset;

			float albedoAlpha;
			float surfaceAlpha;
			float normalsAlpha;

			float glossiness;
			float metallic;

			float surfaceIndex;
			float4 surfaceTilingMtx;

			float detailDiffuseContrib;
			float detailGlossinessContrib;
			float detailMetallicContrib;

			float normalsIndex;
			float4 normalsTilingMtx;

			float detailNormalsContrib;

			float globalNormalScale;
		};

		StructuredBuffer<MaterialParams> _MaterialParams;
#endif

		struct Input
		{
			float2 uv_NormalTex;
		};



#ifdef SHADER_API_D3D11


		float3 GetSurfaceDetails(float2 tileUV, int index)
		{
			MaterialParams details = _MaterialParams[index];

			float2 detailsUV = float2(dot(tileUV, details.surfaceTilingMtx.xy), dot(tileUV, details.surfaceTilingMtx.zw));

			float3 tex2DArrayCoords = float3(detailsUV, details.surfaceIndex);
			float4 surfaceDetails = UNITY_SAMPLE_TEX2DARRAY(_TextureArraySurface, tex2DArrayCoords);

			float albedoDetails = ((surfaceDetails.r - 0.5) * details.detailDiffuseContrib) + 1.0;
			float glossDetails = ((surfaceDetails.g - 0.5) * details.detailGlossinessContrib) + 1.0;
			float metallicDetails = ((surfaceDetails.b - 0.5) * details.detailMetallicContrib) + 1.0;

			return float3(albedoDetails, glossDetails, metallicDetails);
		}

		float3 GetNormalDetails(float2 tileUV, int index)
		{
			MaterialParams details = _MaterialParams[index];

			float2 detailsUV = float2(dot(tileUV, details.normalsTilingMtx.xy), dot(tileUV, details.normalsTilingMtx.zw));
			float3 tex2DArrayCoords = float3(detailsUV, details.normalsIndex);
			float2 normalDetails = UNITY_SAMPLE_TEX2DARRAY(_TextureArrayNormals, tex2DArrayCoords).xy * 2.0 - 1.0;
			//float2 normalDetails = UnpackNormalmapRGorAG(UNITY_SAMPLE_TEX2DARRAY(_TextureArrayNormals, tex2DArrayCoords)).xy;
			float3 localNormal = float3(normalDetails.xy, 0.0);
			localNormal *= details.detailNormalsContrib;
			return localNormal;
		}


		float3 ApplyDetailNormal(float3 normal, float3 localNormal)
		{
			//TODO: PERF: is possible to use simple equation: normal + localNormal
			float3 finalNormal = (normal * sqrt(1.000001 - saturate(dot(localNormal, localNormal)))) + localNormal;
			finalNormal = normalize(finalNormal);
			return finalNormal;
		}

		void surf (Input IN, inout SurfaceOutputStandard o)
		{

			float2 uv = IN.uv_NormalTex;

			float ambient = MY_UNITY_SAMPLE_TEX2D(_AmbientTex, uv).r;
			

			// read palette id
			float texelId = UNITY_SAMPLE_TEX2D(_IndirectionTex, uv).r;
			int id = (int)(texelId * 255);

			int i0 = id * 5 + 0;
			int i1 = id * 5 + 1;
			int i2 = id * 5 + 2;
			int i3 = id * 5 + 3;
			int i4 = id * 5 + 4;

			MaterialParams mat0 = _MaterialParams[i0];
			MaterialParams mat1 = _MaterialParams[i1];
			MaterialParams mat2 = _MaterialParams[i2];
			MaterialParams mat3 = _MaterialParams[i3];
			MaterialParams mat4 = _MaterialParams[i4];

			// read weights
			float4 weights = MY_UNITY_SAMPLE_TEX2D(_WeightsTex, uv);
			float _w0 = weights.r;
			float _w1 = weights.g;
			float _w2 = weights.b;
			float _w3 = weights.a;
			float _w4 = 1.0 - saturate(_w0 + _w1 + _w2 + _w3);



			//-------------------------------------------

	


			// mul by layer alpha
			float a_w0 = mat0.offset + (mat0.albedoAlpha * _w0);
			float a_w1 = mat1.offset + (mat1.albedoAlpha * _w1);
			float a_w2 = mat2.offset + (mat2.albedoAlpha * _w2);
			float a_w3 = mat3.offset + (mat3.albedoAlpha * _w3);
			float a_w4 = mat4.offset + (mat4.albedoAlpha * _w4);


			float nrm = a_w0 + a_w1 + a_w2 + a_w3 + a_w4;
			nrm += 0.000001f;
			a_w0 /= nrm;
			a_w1 /= nrm;
			a_w2 /= nrm;
			a_w3 /= nrm;
			a_w4 /= nrm;


			//-------------------------------------------
			// mul by layer alpha
			float s_w0 = mat0.offset + (mat0.surfaceAlpha * _w0);
			float s_w1 = mat1.offset + (mat1.surfaceAlpha * _w1);
			float s_w2 = mat2.offset + (mat2.surfaceAlpha * _w2);
			float s_w3 = mat3.offset + (mat3.surfaceAlpha * _w3);
			float s_w4 = mat4.offset + (mat4.surfaceAlpha * _w4);


			nrm = s_w0 + s_w1 + s_w2 + s_w3 + s_w4;
			nrm += 0.000001f;
			s_w0 /= nrm;
			s_w1 /= nrm;
			s_w2 /= nrm;
			s_w3 /= nrm;
			s_w4 /= nrm;

			//-------------------------------------------
			// mul by layer alpha
			float n_w0 = mat0.offset + (mat0.normalsAlpha * _w0);
			float n_w1 = mat1.offset + (mat1.normalsAlpha * _w1);
			float n_w2 = mat2.offset + (mat2.normalsAlpha * _w2);
			float n_w3 = mat3.offset + (mat3.normalsAlpha * _w3);
			float n_w4 = mat4.offset + (mat4.normalsAlpha * _w4);


			nrm = n_w0 + n_w1 + n_w2 + n_w3 + n_w4;
			nrm += 0.000001f;
			n_w0 /= nrm;
			n_w1 /= nrm;
			n_w2 /= nrm;
			n_w3 /= nrm;
			n_w4 /= nrm;

			//-------------------------------------------
			
			float3 albedo = mat0.albedoColor.rgb * a_w0 + mat1.albedoColor.rgb * a_w1 + mat2.albedoColor.rgb * a_w2 + mat3.albedoColor.rgb * a_w3 + mat4.albedoColor.rgb * a_w4;
			float3 emission = mat0.emissionColor.rgb * _w0 + mat1.emissionColor.rgb * _w1 + mat2.emissionColor.rgb * _w2 + mat3.emissionColor.rgb * _w3 + mat4.emissionColor.rgb * _w4;
			//float3 emission = saturate(a_w0);

		
			float glossiness = mat0.glossiness * s_w0 + mat1.glossiness * s_w1 + mat2.glossiness * s_w2 + mat3.glossiness * s_w3 + mat4.glossiness * s_w4;
			float metallic = mat0.metallic * s_w0 + mat1.metallic * s_w1 + mat2.metallic * s_w2 + mat3.metallic * s_w3 + mat4.metallic * s_w4;

			float globalNormalScale = mat0.globalNormalScale * n_w0 + mat1.globalNormalScale * n_w1 + mat2.globalNormalScale * n_w2 + mat3.globalNormalScale * n_w3 + mat4.globalNormalScale * n_w4;

			//-------------------------------------------

			float3 normal = UnpackNormal(MY_UNITY_SAMPLE_TEX2D(_NormalTex, uv));
			normal = lerp(float3(0, 0, 1), normal, globalNormalScale);


			//------------- apply details -----------------------


#ifndef LOW_QUALITY

/*


			// bruteforce select major weight
			int detailMaterialIndex = i0;


			if (w1 > w0 && w1 > w2 && w1 > w3)
			{
				detailMaterialIndex = i1;
			} else
			{
				if (w2 > w0 && w2 > w1 && w2 > w3)
				{
					detailMaterialIndex = i2;
				} else
				{
					if (w3 > w0 && w3 > w1 && w3 > w2)
					{
						detailMaterialIndex = i3;
					}
				}
			}

*/


			float2 tileUV = uv - float2(0.5, 0.5);

			float3 surfaceDetails0 = GetSurfaceDetails(tileUV, i0);
			float3 surfaceDetails1 = GetSurfaceDetails(tileUV, i1);
			float3 surfaceDetails2 = GetSurfaceDetails(tileUV, i2);
			float3 surfaceDetails3 = GetSurfaceDetails(tileUV, i3);
			float3 surfaceDetails4 = GetSurfaceDetails(tileUV, i4);

	
			float3 localNormal0 = GetNormalDetails(tileUV, i0);
			float3 localNormal1 = GetNormalDetails(tileUV, i1);
			float3 localNormal2 = GetNormalDetails(tileUV, i2);
			float3 localNormal3 = GetNormalDetails(tileUV, i3);
			float3 localNormal4 = GetNormalDetails(tileUV, i4);


			float albedoDetails = surfaceDetails0.x * a_w0 + surfaceDetails1.x * a_w1 + surfaceDetails2.x * a_w2 + surfaceDetails3.x * a_w3 + surfaceDetails4.x * a_w4;

			float2 surfaceDetails = surfaceDetails0.yz * s_w0 + surfaceDetails1.yz * s_w1 + surfaceDetails2.yz * s_w2 + surfaceDetails3.yz * s_w3 + surfaceDetails4.yz * s_w4;
			float glossDetails = surfaceDetails.x;
			float metallicDetails = surfaceDetails.y;

			float3 localNormal = localNormal0 * n_w0 + localNormal1 * n_w1 + localNormal2 * n_w2 + localNormal3 * n_w3 + localNormal4 * n_w4;
		

			float3 finalNormal = ApplyDetailNormal(normal, localNormal);

#else


			// bruteforce select major weight
			//TODO: less bruteforce method to select maximum weight
			int detailMaterialIndex = i0;

			if (a_w1 > a_w0 && a_w1 > a_w2 && a_w1 > a_w3 && a_w1 > a_w4)
			{
				detailMaterialIndex = i1;
			} else
			{
				if (a_w2 > a_w0 && a_w2 > a_w1 && a_w2 > a_w3 && a_w2 > a_w4)
				{
					detailMaterialIndex = i2;
				} else
				{
					if (a_w3 > a_w0 && a_w3 > a_w1 && a_w3 > a_w2 && a_w3 > a_w4)
					{
						detailMaterialIndex = i3;
					} else
					{
						if (a_w4 > a_w0 && a_w4 > a_w1 && a_w4 > a_w2 && a_w4 > a_w3)
						{
							detailMaterialIndex = i4;
						}
				
					}
				}
			}

			float2 tileUV = uv - float2(0.5, 0.5);

			float3 surfaceDetails = GetSurfaceDetails(tileUV, detailMaterialIndex);

			float3 finalNormal = normal;
			float albedoDetails = surfaceDetails.x;
			float glossDetails = surfaceDetails.y;
			float metallicDetails = surfaceDetails.z;

#endif


			//--------------------

			ambient = saturate(ambient * ambientCorrection);

			o.Normal = finalNormal;
			o.Albedo = ambient * saturate(albedoDetails * albedo);
			o.Metallic = saturate(metallicDetails * metallic);
			o.Smoothness = saturate(glossDetails * glossiness);
			o.Emission = emission;

			float alpha = MY_UNITY_SAMPLE_TEX2D(_AlphaTex, uv).a;
		

			if (_isAlphaTest > 0.1f)
			{
				clip(alpha - 0.5f);
			}
			o.Alpha = alpha;


			//o.Albedo = texelId / 32;
		}


#else

		void surf(Input IN, inout SurfaceOutputStandard o)
		{
			o.Albedo = IN.uv_NormalTex.rgg;
			o.Normal = float3(0, 0, 1);
			o.Metallic = 0;
			o.Smoothness = 0;
			o.Alpha = 1.0;
		}


#endif


		ENDCG
	}
	FallBack "Diffuse"
}
