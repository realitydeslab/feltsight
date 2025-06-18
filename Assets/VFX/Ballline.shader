Shader "VFX/Ballline_URP"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _EmissionColor ("Emission Color", Color) = (1,1,1,1)
        _EmissionIntensity ("Emission Intensity", Range(0, 10)) = 1.0
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _TargetDistance ("Target Distance", Float) = 10.0
        _LineWidth ("Line Width", Range(0.1, 5.0)) = 1.0
        _FadeSharpness ("Fade Sharpness", Range(0.1, 5.0)) = 1.0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Transparent"
        }
        
        LOD 200
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _EmissionColor;
                float _EmissionIntensity;
                float _Smoothness;
                float _Metallic;
                float _TargetDistance;
                float _LineWidth;
                float _FadeSharpness;
            CBUFFER_END
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float3 viewDirWS : TEXCOORD4;
            };
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = input.uv;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                
                return output;
            }
            
            float4 frag(Varyings input) : SV_Target
            {
                // 计算当前像素到摄像机的距离
                float currentDepth = distance(_WorldSpaceCameraPos, input.positionWS);
                
                // 计算距离目标距离的偏差
                float distanceFromTarget = abs(currentDepth - _TargetDistance);
                
                // 使用smoothstep创建平滑的alpha渐变
                float alpha = 1.0 - smoothstep(0, _LineWidth * 0.5, distanceFromTarget);
                
                // 应用渐变锐度控制
                alpha = pow(alpha, _FadeSharpness);
                
                // 如果alpha太小，直接丢弃片元
                if (alpha < 0.01)
                    discard;
                
                // 基础光照计算
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = normalize(input.viewDirWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = _Color.rgb;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.alpha = _Color.a * alpha;
                surfaceData.normalTS = float3(0, 0, 1);
                
                // 添加自发光，强度随alpha变化
                surfaceData.emission = _EmissionColor.rgb * _EmissionIntensity * alpha;
                
                float4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.a = surfaceData.alpha;
                
                return color;
            }
            ENDHLSL
        }
        
        // Shadow Caster Pass (简化版，因为是透明物体)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float _TargetDistance;
                float _LineWidth;
                float _FadeSharpness;
            CBUFFER_END
            
            struct Attributes
            {
                float4 positionOS : POSITION;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                return output;
            }
            
            float4 frag(Varyings input) : SV_Target
            {
                float currentDepth = distance(_WorldSpaceCameraPos, input.positionWS);
                float distanceFromTarget = abs(currentDepth - _TargetDistance);
                float alpha = 1.0 - smoothstep(0, _LineWidth * 0.5, distanceFromTarget);
                alpha = pow(alpha, _FadeSharpness);
                
                if (alpha < 0.01)
                    discard;
                    
                return 0;
            }
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
