Shader "ChikuwaProducts/SlidenScreenShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "ForceNoShadowCasting" = "True"}
        
        Pass {
            SetTexture [_MainTex] {
                Combine texture * texture
            }
        }
    }
}
