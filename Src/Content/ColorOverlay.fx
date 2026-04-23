#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

//texture being drawn
Texture2D SpriteTexture;

// Texture and Sampler
sampler2D SpriteTextureSampler = sampler_state
{
    Texture = <SpriteTexture>;
};

//what the pixelshader recieves
struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TextureCoordinates : TEXCOORD0;
};

// Parameters
float ProgressThreshold;
float4 OverlayColor;

float4 MainPS(VertexShaderOutput input) : COLOR
{
    // Use the standard tex2D - this works on DesktopGL if we use ps_3_0
    float4 tex = tex2D(SpriteTextureSampler,input.TextureCoordinates);

    if (input.TextureCoordinates.x < ProgressThreshold)
    {
        // Apply overlay but keep original alpha
        tex.rgb *= OverlayColor.rgb;
    }

    return tex * input.Color;
}

technique SpriteDrawing
{
    pass P0
    {
        // ps_3_0 is the "Golden Ratio" for MonoGame cross-platform shaders
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};