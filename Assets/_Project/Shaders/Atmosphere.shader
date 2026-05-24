Shader "Hidden/Atmosphere"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;
            float3 _CameraWorldPos;

            float3 planetCenter;
            float atmosphereRadius;
            float planetRadius;
            float3 dirToSun;
            int numInScatteringPoints;
			int numOpticalDepthPoints;
			float intensity;
			float3 scatteringCoefficients;
			float densityFalloff;

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewVector : TEXCOORD1;
            };
            struct appdata {
                float4 vertex : POSITION;
                float4 uv : TEXCOORD0;
			};

            v2f vert (appdata v) {
                v2f output;
                output.pos = UnityObjectToClipPos(v.vertex);
                output.uv = v.uv;
                float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv.xy * 2 - 1, 0, -1));
                output.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
                return output;
			}

            float2 raySphere(float3 sphereCenter, float sphereRadius, float3 rayOrigin, float3 rayDir){
                float3 offset = rayOrigin - sphereCenter;
                float a = 1;
                float b = 2 * dot(offset, rayDir);
                float c = dot(offset, offset) - sphereRadius * sphereRadius;
                float d = b*b-4*a*c;

                if(d>0){
                    float s = sqrt(d);
                    float dstToSphereNear = max(0, (-b - s) / (2*a));
                    float dstToSphereFar = (-b+s) / (2*a);
                    if(dstToSphereFar >= 0){
                        return float2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
                    }
                }
                return float2(1e7, 0);
            }

            float densityAtPoint(float3 densitySamplePoint) {
				float heightAboveSurface = length(densitySamplePoint - planetCenter) - planetRadius;
				float height01 = heightAboveSurface / (atmosphereRadius - planetRadius);
				float localDensity = exp(-height01 * densityFalloff) * (1 - height01);
				return localDensity;
			}
			
			float opticalDepth(float3 rayOrigin, float3 rayDir, float rayLength) {
				float3 densitySamplePoint = rayOrigin;
				float stepSize = rayLength / (numOpticalDepthPoints - 1);
				float opticalDepth = 0;

				for (int i = 0; i < numOpticalDepthPoints; i ++) {
					float localDensity = densityAtPoint(densitySamplePoint);
					opticalDepth += localDensity * stepSize;
					densitySamplePoint += rayDir * stepSize;
				}
				return opticalDepth;
			}

            float3 calculateLight(float3 rayOrigin, float3 rayDir, float rayLength, float3 originalColor){
                float3 inScatterPoint = rayOrigin;
                float stepSize = rayLength / (numInScatteringPoints-1);
                float3 inScatterLight = 0;
                float viewRayOpticalDepth = 0;
                for(int i = 0; i < numInScatteringPoints; i++){
                    float sunRayLength = raySphere(planetCenter, atmosphereRadius, inScatterPoint, dirToSun).y;
                    float sunRayOpticalDepth = opticalDepth(inScatterPoint, dirToSun, sunRayLength);
                    viewRayOpticalDepth = opticalDepth(inScatterPoint, -rayDir, stepSize * i);
                    float3 transmittance = exp(-(sunRayOpticalDepth + viewRayOpticalDepth)*scatteringCoefficients);
                    float localDensity = densityAtPoint(inScatterPoint);
                    inScatterLight += localDensity*transmittance*scatteringCoefficients*stepSize;
                    inScatterPoint+=rayDir*stepSize;
                }
                float originalColorTransmittance = exp(-viewRayOpticalDepth);
                return originalColor*originalColorTransmittance+inScatterLight;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 originalColor = tex2D(_MainTex, i.uv);
                float sceneDepthNonLinear = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float sceneDepth = LinearEyeDepth(sceneDepthNonLinear) * length(i.viewVector);

                float3 rayOrigin = _CameraWorldPos;
                float3 rayDir = normalize(i.viewVector);

                float2 hitInfo = raySphere(planetCenter, atmosphereRadius, rayOrigin, rayDir);
                float dstToAtmosphere = hitInfo.x;
                float dstThroughAtmosphere = min(hitInfo.y, sceneDepth - dstToAtmosphere);
                float3 finalColor = originalColor;
                float epsilon = 0.001;
                if(dstThroughAtmosphere > 0){
                    float3 pointInAtmosphere = rayOrigin+rayDir*dstToAtmosphere;
                    float3 light = calculateLight(pointInAtmosphere, rayDir, dstThroughAtmosphere-epsilon*2, originalColor);
                    return float4(light, 0.0);
                }

                return originalColor;
            }
            ENDCG
        }
    }
}