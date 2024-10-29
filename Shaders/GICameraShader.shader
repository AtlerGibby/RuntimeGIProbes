Shader "Hidden/GICameraShader"
{
    Properties
    {
        [KeywordEnum(Linear, Exponential, Exponential Squared)] _FogMode("Fog Mode", Float) = 0

        [GIMaterialTooltips(_FogColor)]
        _FogColor("Fog Color", Color) = (0.4196078, 0.4784313, 0.6274511, 1)
        [GIMaterialTooltips(_FogDensity)]
        _FogDensity ("Density (Exponential Fog)", Float) = 0.001
        [GIMaterialTooltips(_FogStart)]
        _FogStart ("Start (Linear Fog)", Float) = 0
        [GIMaterialTooltips(_FogEnd)]
        _FogEnd ("End (Linear Fog)", Float) = 300

        [GIMaterialTooltips(_GIColInf)]
        _GIColInf ("GI Hue Influence", Range(0.0, 1.0)) = 1
        [GIMaterialTooltips(_GIBrightness)]
        _GIBrightness ("GI Brightness", Range(0.0, 2.0)) = 1.7
        [GIMaterialTooltips(_GIContrast)]
        _GIContrast ("GI Contrast", Range(0.0, 2.0)) = 1.03
        [GIMaterialTooltips(_GIStrength)]
        _GIStrength ("GI Strength", Range(0.0, 1.0)) = 1

        [GIMaterialTooltips(_GIVolRad)]
        _GIVolRad ("GI Zone Falloff", float) = 0.5

        [GIMaterialTooltips(_LCStrength)]
        _LCStrength ("Light Checker Strength (Direct Lighting)", Range(0.0, 1.0)) = 1
        [GIMaterialTooltips(_SSSMStrength)]
        _SSSMStrength ("Screen Space Shadowmap Strength (Direct Lighting)", Range(0.0, 1.0)) = 1

        [GIMaterialTooltips(_DebugGILC)]
        _DebugGILC ("Debug Direct Lighting", Range(0.0, 1.0)) = 0
        [KeywordEnum(Disable, Enable)] _DebugMode("Debug Mode", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_local _FOGMODE_LINEAR _FOGMODE_EXPONENTIAL _FOGMODE_EXPONENTIAL_SQUARED
            #pragma multi_compile_local _DEBUGMODE_DISABLE _DEBUGMODE_ENABLE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            int useVR;

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                //float4 positionWorldHCS  : SV_POSITION;
                float2  uv          : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                //Varyings OUT;
                //OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                //return OUT;

                Varyings OUT;
                if(useVR)
                {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // Note: The pass is setup with a mesh already in clip
                // space, that's why, it's enough to just output vertex
                // positions
                OUT.positionHCS = float4(IN.positionOS.xyz, 1.0);

                //OUT.positionWorldHCS = TransformObjectToHClip(IN.positionOS.xyz);

                #if UNITY_UV_STARTS_AT_TOP
                OUT.positionHCS.y *= -1;
                #endif

                OUT.uv = IN.uv;
                }
                else
                {
                    OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                }
                return OUT;
            }


            //Properties defined above
            float4 _FogColor;
            float _FogDensity;
            float _FogStart;
            float _FogEnd;

            float _GIColInf;
            float _GIBrightness;
            float _GIContrast;
            float _GIStrength;
            float _GIVolRad;

            float _LCStrength;
            float _SSSMStrength;
        
            float _DebugGILC;

            float _VRXScale;
            float _VRXROffset;
            float _VRXLOffset;
            float _VRYOffset;

            //Global Shader Properties created in the GI Render Feature
            float lightMapRes;
            float4 lightMapScale;
            float4 lightMapOffsetVec4;

            TEXTURE2D_X(_GILC);
            SAMPLER (sampler_GILC);

            //TEXTURE2D_X(_ScreenSpaceShadowmapTexture);
            //SAMPLER(sampler_ScreenSpaceShadowmapTexture);

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            //TEXTURE2D_X(_CameraNormalsTexture);
            //SAMPLER(sampler_CameraNormalsTexture);

            //uniform Texture2D _GIRT;
            //SamplerState sampler_GIRT;

            uniform float4x4 _BBTransforms[128];
            uniform float4 _BBScales[128];
            uniform float _BBFalloff[128];
            uniform float _BBStrengths[128];

            uniform Texture3D _GIVolumeUp;
            SamplerState sampler_GIVolumeUp;
            uniform Texture3D _GIVolumeDown;
            SamplerState sampler_GIVolumeDown;
            uniform Texture3D _GIVolumeLeft;
            SamplerState sampler_GIVolumeLeft;
            uniform Texture3D _GIVolumeRight;
            SamplerState sampler_GIVolumeRight;
            uniform Texture3D _GIVolumeFront;
            SamplerState sampler_GIVolumeFront;
            uniform Texture3D _GIVolumeBack;
            SamplerState sampler_GIVolumeBack;

            // These are some helper functions
            void Unity_Saturation_float(float3 In, float Saturation, out float4 Out)
            {
                float luma = dot(In, float3(0.2126729, 0.7151522, 0.0721750));
                Out =  float4(float3(luma.xxx + Saturation.xxx * (In - luma.xxx)).xyz, 1);
            }

            void RGB_To_HSV_float(float3 In, out float3 Out)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 P = lerp(float4(In.bg, K.wz), float4(In.gb, K.xy), step(In.b, In.g));
                float4 Q = lerp(float4(P.xyw, In.r), float4(In.r, P.yzx), step(P.x, In.r));
                float D = Q.x - min(Q.w, Q.y);
                float  E = 1e-10;
                Out = float3(abs(Q.z + (Q.w - Q.y)/(6.0 * D + E)), D / (Q.x + E), Q.x);
            }

            void HSV_To_RGB_float(float3 In, out float3 Out)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 P = abs(frac(In.xxx + K.xyz) * 6.0 - K.www);
                Out = In.z * lerp(K.xxx, saturate(P - K.xxx), In.y);
            }

            void Unity_Contrast_float(float3 In, float Contrast, out float3 Out)
            {
                float midpoint = pow(0.5, 2.2);
                Out = (In - midpoint) * Contrast + midpoint;
            }

            float4x4 inverse(float4x4 m) 
            {
                float n11 = m[0][0], n12 = m[1][0], n13 = m[2][0], n14 = m[3][0];
                float n21 = m[0][1], n22 = m[1][1], n23 = m[2][1], n24 = m[3][1];
                float n31 = m[0][2], n32 = m[1][2], n33 = m[2][2], n34 = m[3][2];
                float n41 = m[0][3], n42 = m[1][3], n43 = m[2][3], n44 = m[3][3];

                float t11 = n23 * n34 * n42 - n24 * n33 * n42 + n24 * n32 * n43 - n22 * n34 * n43 - n23 * n32 * n44 + n22 * n33 * n44;
                float t12 = n14 * n33 * n42 - n13 * n34 * n42 - n14 * n32 * n43 + n12 * n34 * n43 + n13 * n32 * n44 - n12 * n33 * n44;
                float t13 = n13 * n24 * n42 - n14 * n23 * n42 + n14 * n22 * n43 - n12 * n24 * n43 - n13 * n22 * n44 + n12 * n23 * n44;
                float t14 = n14 * n23 * n32 - n13 * n24 * n32 - n14 * n22 * n33 + n12 * n24 * n33 + n13 * n22 * n34 - n12 * n23 * n34;

                float det = n11 * t11 + n21 * t12 + n31 * t13 + n41 * t14;
                float idet = 1.0f / det;

                float4x4 ret;

                ret[0][0] = t11 * idet;
                ret[0][1] = (n24 * n33 * n41 - n23 * n34 * n41 - n24 * n31 * n43 + n21 * n34 * n43 + n23 * n31 * n44 - n21 * n33 * n44) * idet;
                ret[0][2] = (n22 * n34 * n41 - n24 * n32 * n41 + n24 * n31 * n42 - n21 * n34 * n42 - n22 * n31 * n44 + n21 * n32 * n44) * idet;
                ret[0][3] = (n23 * n32 * n41 - n22 * n33 * n41 - n23 * n31 * n42 + n21 * n33 * n42 + n22 * n31 * n43 - n21 * n32 * n43) * idet;

                ret[1][0] = t12 * idet;
                ret[1][1] = (n13 * n34 * n41 - n14 * n33 * n41 + n14 * n31 * n43 - n11 * n34 * n43 - n13 * n31 * n44 + n11 * n33 * n44) * idet;
                ret[1][2] = (n14 * n32 * n41 - n12 * n34 * n41 - n14 * n31 * n42 + n11 * n34 * n42 + n12 * n31 * n44 - n11 * n32 * n44) * idet;
                ret[1][3] = (n12 * n33 * n41 - n13 * n32 * n41 + n13 * n31 * n42 - n11 * n33 * n42 - n12 * n31 * n43 + n11 * n32 * n43) * idet;

                ret[2][0] = t13 * idet;
                ret[2][1] = (n14 * n23 * n41 - n13 * n24 * n41 - n14 * n21 * n43 + n11 * n24 * n43 + n13 * n21 * n44 - n11 * n23 * n44) * idet;
                ret[2][2] = (n12 * n24 * n41 - n14 * n22 * n41 + n14 * n21 * n42 - n11 * n24 * n42 - n12 * n21 * n44 + n11 * n22 * n44) * idet;
                ret[2][3] = (n13 * n22 * n41 - n12 * n23 * n41 - n13 * n21 * n42 + n11 * n23 * n42 + n12 * n21 * n43 - n11 * n22 * n43) * idet;

                ret[3][0] = t14 * idet;
                ret[3][1] = (n13 * n24 * n31 - n14 * n23 * n31 + n14 * n21 * n33 - n11 * n24 * n33 - n13 * n21 * n34 + n11 * n23 * n34) * idet;
                ret[3][2] = (n14 * n22 * n31 - n12 * n24 * n31 - n14 * n21 * n32 + n11 * n24 * n32 + n12 * n21 * n34 - n11 * n22 * n34) * idet;
                ret[3][3] = (n12 * n23 * n31 - n13 * n22 * n31 + n13 * n21 * n32 - n11 * n23 * n32 - n12 * n21 * n33 + n11 * n22 * n33) * idet;

                return ret;
            }

            float sdBox(float3 wp, float4x4 t, float3 scale)
            {
                float3 p = (mul(inverse(t), float4(wp, 1))).xyz;
                float3 q = abs(p) - scale/2;
                return length(max(q,0.0)) + min(max(q.x,max(q.y,q.z)),0.0);
            }
            
            half4 frag (Varyings IN) : SV_Target
            {
                if(useVR == 1)
                {
                    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                }
                float2 UV = IN.uv;

                float4 GI_OUT = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, IN.uv);
                float4 CNT = SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_CameraNormalsTexture, IN.uv);
                //color_XR *= float4(1,0.75, 0.75, 1);

                #if UNITY_REVERSED_Z
                    real depth = SampleSceneDepth(UV);
                #else
                    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(UV));
                #endif

                // The world space position of the current pixel
                float3 worldPos = ComputeWorldSpacePosition(UV, depth, UNITY_MATRIX_I_VP);

                float4 GILC = SAMPLE_TEXTURE2D_X(_GILC, sampler_GILC, IN.uv); //float4(0,0,0,0);
                float SSST = SAMPLE_TEXTURE2D_X(_ScreenSpaceShadowmapTexture, sampler_ScreenSpaceShadowmapTexture, IN.uv).x;

                GILC = float4(GILC.xyz * _LCStrength, 1);
                SSST *= _SSSMStrength;

                //Use Screen space shadow map texture if available
                GILC = float4(min(GILC.z + SSST, 1), min(GILC.y + SSST, 1), min(GILC.z + SSST, 1),1);

                GILC = lerp(GILC, float4(1,1,1,1), 1 - _GIStrength);
                GI_OUT = lerp(GI_OUT, GILC, _DebugGILC);


                // Convert global light map origin point float4 to a local float3
                float3 lightMapOffset = lightMapOffsetVec4.xyz;

                // Desatured value of the light checker render texture
                float4 desaturatedLC = float4(0,0,0,0);

                // The color value at the current pixel position from the lightmap
                float4 lightMapX = float4(0,0,0,0);
                float4 lightMapY = float4(0,0,0,0);
                float4 lightMapZ = float4(0,0,0,0);

                // 0 = not inside a bounding box, 1 = inside a bounding box
                float BBValue = 0;

                int BBLength = _BBTransforms.Length;
                for(int i = 0; i < BBLength; i++)
                {
                    if(_BBScales[i].x != 0 && _BBScales[i].y != 0 && _BBScales[i].z != 0 && _BBStrengths[i] != 0)
                    {
                        if(
                        worldPos.x < ((lightMapRes/2) * lightMapScale.x) + lightMapOffset.x &&
                        worldPos.y < ((lightMapRes/2) * lightMapScale.y) + lightMapOffset.y &&
                        worldPos.z < ((lightMapRes/2) * lightMapScale.z) + lightMapOffset.z &&

                        worldPos.x > ((-lightMapRes/2) * lightMapScale.x) + lightMapOffset.x &&
                        worldPos.y > ((-lightMapRes/2) * lightMapScale.y) + lightMapOffset.y &&
                        worldPos.z > ((-lightMapRes/2) * lightMapScale.z) + lightMapOffset.z)
                        {
                            float boxInf = sdBox(worldPos, _BBTransforms[i], _BBScales[i].xyz);
                            float boxFalloff = _BBFalloff[i] + _GIVolRad;
                            if(boxInf <= 0)
                                BBValue += 1 * _BBStrengths[i];
                            else
                                BBValue += clamp((boxFalloff - boxInf)/boxFalloff, 0, 1) * _BBStrengths[i];
                        }
                    }
                }
                BBValue = clamp(BBValue, 0, 1);

                // Get 3D Light Map Info
                if(
                worldPos.x < ((lightMapRes/2) * lightMapScale.x) + lightMapOffset.x &&
                worldPos.y < ((lightMapRes/2) * lightMapScale.y) + lightMapOffset.y &&
                worldPos.z < ((lightMapRes/2) * lightMapScale.z) + lightMapOffset.z &&

                worldPos.x > ((-lightMapRes/2) * lightMapScale.x) + lightMapOffset.x &&
                worldPos.y > ((-lightMapRes/2) * lightMapScale.y) + lightMapOffset.y &&
                worldPos.z > ((-lightMapRes/2) * lightMapScale.z) + lightMapOffset.z)
                {

                    float3 originOffset = float3(
                        ((lightMapRes/2) * lightMapScale.x) - lightMapOffset.x,
                        ((lightMapRes/2) * lightMapScale.y) - lightMapOffset.y,
                        ((lightMapRes/2) * lightMapScale.z) - lightMapOffset.z);

                    float3 adjustedWP = worldPos + originOffset;
                    float3 invertedLMScale = 1/lightMapScale.xyz;

                    float4 tmpColU = _GIVolumeUp.Sample(sampler_GIVolumeUp, ((adjustedWP) / lightMapRes) * (invertedLMScale));
                    float4 tmpColD = _GIVolumeDown.Sample(sampler_GIVolumeDown, ((adjustedWP) / lightMapRes) * (invertedLMScale));
                    float4 tmpColL = _GIVolumeLeft.Sample(sampler_GIVolumeLeft, ((adjustedWP) / lightMapRes) * (invertedLMScale));
                    float4 tmpColR = _GIVolumeRight.Sample(sampler_GIVolumeRight, ((adjustedWP) / lightMapRes) * (invertedLMScale));
                    float4 tmpColF = _GIVolumeFront.Sample(sampler_GIVolumeFront, ((adjustedWP) / lightMapRes) * (invertedLMScale));
                    float4 tmpColB = _GIVolumeBack.Sample(sampler_GIVolumeBack, ((adjustedWP) / lightMapRes) * (invertedLMScale));
                    float lumaDifU = (0.299*tmpColU.x + 0.587*tmpColU.y + 0.114*tmpColU.z);                                                             
                    float lumaDifD = (0.299*tmpColD.x + 0.587*tmpColD.y + 0.114*tmpColD.z); 
                    float lumaDifL = (0.299*tmpColL.x + 0.587*tmpColL.y + 0.114*tmpColL.z); 
                    float lumaDifR = (0.299*tmpColR.x + 0.587*tmpColR.y + 0.114*tmpColR.z); 
                    float lumaDifF = (0.299*tmpColF.x + 0.587*tmpColF.y + 0.114*tmpColF.z); 
                    float lumaDifB = (0.299*tmpColB.x + 0.587*tmpColB.y + 0.114*tmpColB.z); 

                    if( CNT.x > 0)
                    {
                        lightMapX += float4(
                        tmpColR.xyz * lerp(1, BBValue,
                        clamp(lumaDifR * 64, 0, 1)),
                        BBValue) * CNT.x;
                    }

                    if( CNT.x < 0)
                    {
                        lightMapX += float4(
                        tmpColL.xyz * lerp(1, BBValue,
                        clamp(lumaDifL * 64, 0, 1)),
                        BBValue) * abs(CNT.x);
                    }

                    if( CNT.y > 0)
                    {
                        lightMapY += float4(
                        tmpColU.xyz * lerp(1, BBValue,
                        clamp(lumaDifU * 64, 0, 1)),
                        BBValue) * CNT.y;
                    }

                    if( CNT.y < 0)
                    {
                        lightMapY += float4(
                        tmpColD.xyz * lerp(1, BBValue,
                        clamp(lumaDifD * 64, 0, 1)),
                        BBValue) * abs(CNT.y);
                    }

                    if( CNT.z > 0)
                    {
                        lightMapZ += float4(
                        tmpColF.xyz * lerp(1, BBValue,
                        clamp(lumaDifF * 64, 0, 1)),
                        BBValue) * CNT.z;
                    }

                    if( CNT.z < 0)
                    {
                        lightMapZ += float4(
                        tmpColB.xyz * lerp(1, BBValue,
                        clamp(lumaDifB * 64, 0, 1)),
                        BBValue) * abs(CNT.z);
                    }
                
                }

                // Get Direct Lighting Info
                Unity_Saturation_float( GILC.xyz, 0, desaturatedLC);

                // Normal Output
                #ifdef _DEBUGMODE_DISABLE

                    // Coloring the output
                    float3 tmpCol = lightMapX.xyz + lightMapY.xyz + lightMapZ.xyz;
                    float3 tmpHSV;

                    float3 tmpHSV2;
                    float3 tmpHSV3;

                    RGB_To_HSV_float(tmpCol, tmpHSV);
                    RGB_To_HSV_float(GI_OUT.xyz, tmpHSV2);

                    float3 tmpCol2 = GI_OUT.xyz * lerp(float3(1,1,1), tmpCol, min(1, _GIColInf));

                    RGB_To_HSV_float(tmpCol2, tmpHSV3);

                    //float3 tmpMid = float3(tmpHSV3.x, tmpHSV2.y, tmpHSV.z * (tmpHSV2.z / (2 - _GIBrightness)));
                    float3 tmpMid = float3(tmpHSV3.x, tmpHSV2.y, tmpHSV.z * lerp(tmpHSV2.z / (2 - _GIBrightness), (tmpHSV2.z + 0.01) / (2 - _GIBrightness), tmpHSV.z + (1-tmpHSV.z) ));
                    float3 finCol;
                    HSV_To_RGB_float(tmpMid, finCol);

                    float3 finColContrast;
                    Unity_Contrast_float(finCol, _GIContrast, finColContrast);


                    // Reapplying Fog
                    float fogStength = 0;
                    #ifdef _FOGMODE_LINEAR
                        fogStength = min(1,max(0,(distance(worldPos, _WorldSpaceCameraPos) - _FogStart) / (_FogEnd -_FogStart)));
                    #endif

                    #ifdef _FOGMODE_EXPONENTIAL
                        fogStength = 1 - min(1,max(0,pow(2, - distance(worldPos, _WorldSpaceCameraPos) * _FogDensity)));
                    #endif

                    #ifdef _FOGMODE_EXPONENTIAL_SQUARED
                        fogStength = 1 - min(1,max(0,pow(2, -1 * pow(distance(worldPos, _WorldSpaceCameraPos) * _FogDensity, 2))));
                    #endif

                    return lerp(
                        lerp(float4(clamp(finColContrast, float3(0,0,0), float3(1,1,1)), 1), _FogColor, fogStength),
                        GI_OUT,
                        1 - max(0, min(lightMapX.w + lightMapY.w + lightMapZ.w, 1) - desaturatedLC)
                        );
                #endif
                
                // Debug Raw Light Map Info
                #ifdef _DEBUGMODE_ENABLE
                    if( worldPos.x < ((lightMapRes/2) * lightMapScale.x) + lightMapOffset.x &&
                        worldPos.y < ((lightMapRes/2) * lightMapScale.y) + lightMapOffset.y &&
                        worldPos.z < ((lightMapRes/2) * lightMapScale.z) + lightMapOffset.z &&

                        worldPos.x > ((-lightMapRes/2) * lightMapScale.x) + lightMapOffset.x &&
                        worldPos.y > ((-lightMapRes/2) * lightMapScale.y) + lightMapOffset.y &&
                        worldPos.z > ((-lightMapRes/2) * lightMapScale.z) + lightMapOffset.z)
                    {
                        return float4(clamp(lightMapX.xyz + lightMapY.xyz + lightMapZ.xyz, float3(0,0,0), float3(1,1,1)), 1);
                    }
                    return GI_OUT;
                #endif

            }
            ENDHLSL
        }
    }
}
