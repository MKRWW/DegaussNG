sampler s0 : register(s0);

float c0 : register(c0); // amplitude
float c1 : register(c1); // rgb shift
float c2 : register(c2); // brightness
float c3 : register(c3); // phase

float4 main(float2 uv : TEXCOORD) : SV_Target
{
    float offset = c0 * (sin(uv.y * 34.0 + c3) + 0.45 * sin(uv.y * 91.0 - c3 * 1.7));

    float y = saturate(uv.y);

    float3 color;
    color.r = tex2D(s0, float2(saturate(uv.x + offset + c1), y)).r;
    color.g = tex2D(s0, float2(saturate(uv.x + offset), y)).g;
    color.b = tex2D(s0, float2(saturate(uv.x + offset - c1), y)).b;

    return float4(color * (1.0 + c2), 1.0);
}
