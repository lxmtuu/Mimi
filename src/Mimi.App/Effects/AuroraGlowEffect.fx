// Limited WPF pixel-shader prototype for the Home hero accent layer.
// Compile with tools/Compile-PixelShader.ps1 (profile ps_3_0, entry main).
// The shader preserves the rendered layer's alpha, so the existing theme
// gradient remains responsible for the shape and falloff of the glow.

sampler2D inputSampler : register(s0);
float4 accentColor : register(c0);
float4 secondaryColor : register(c1);
float strength : register(c2);
float phase : register(c3);

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float4 source = tex2D(inputSampler, uv);

    float phaseRadians = phase * 6.28318530718;
    float diagonal = uv.x * 1.35 - uv.y * 0.65;
    float wave = sin(
        diagonal * 9.0 +
        phaseRadians +
        sin(uv.y * 7.0 - phaseRadians) * 0.65);
    float band = smoothstep(0.15, 1.0, wave * 0.5 + 0.5);
    float sheen = pow(band, 3.0);

    float horizontalFade = sin(uv.x * 3.14159265359);
    float verticalFade = sin(uv.y * 3.14159265359);
    float mask = verticalFade * lerp(0.35, 1.0, horizontalFade);

    float3 tint = lerp(accentColor.rgb, secondaryColor.rgb, band);
    float mixAmount = saturate(strength) * sheen * mask;

    // WPF effect surfaces are premultiplied. Tint toward tint * alpha
    // instead of adding unpremultiplied RGB, which keeps soft edges clean.
    float3 color = lerp(source.rgb, tint * source.a, mixAmount);
    return float4(color, source.a);
}
