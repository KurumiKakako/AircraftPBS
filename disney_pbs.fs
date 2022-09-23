#version 330 core
layout (location = 0) out vec4 FragColor;
layout (location = 1) out vec4 BrightColor;

struct Material {
    sampler2D texture_albedo1;
    sampler2D texture_normal1;
    sampler2D texture_metallic1;
    sampler2D texture_roughness1;
    sampler2D texture_ao1;
    sampler2D texture_height1;
    sampler2D texture_emissive1;
    sampler2D texture_opacity1;
};

struct PointLight {
    vec3 position_world;
    vec3 position_tangent;
	
    vec3 color;
};

#define NR_POINT_LIGHTS 1

in vec3 WorldFragPos;
in vec3 WorldNormal;
in vec2 TexCoords;
in vec3 TangentLightPos;
in vec3 TangentViewPos;
in vec3 TangentFragPos;

uniform vec3 viewPos_world;
//uniform samplerCube skybox;
uniform Material material;
uniform samplerCube shadowMap;

uniform PointLight pointLights[NR_POINT_LIGHTS];

uniform float far_plane;
uniform bool shadows;
uniform bool parallax;
uniform float height_scale;
uniform bool hasEmissive;
uniform bool hasOpacity;
// IBL
uniform samplerCube irradianceMap;
uniform samplerCube prefilterMap;
uniform sampler2D   brdfLUT; 

const float PI = 3.14159265359;

// array of offset direction for sampling
vec3 gridSamplingDisk[20] = vec3[] (
   vec3(1, 1,  1), vec3( 1, -1,  1), vec3(-1, -1,  1), vec3(-1, 1,  1), 
   vec3(1, 1, -1), vec3( 1, -1, -1), vec3(-1, -1, -1), vec3(-1, 1, -1),
   vec3(1, 1,  0), vec3( 1, -1,  0), vec3(-1, -1,  0), vec3(-1, 1,  0),
   vec3(1, 0,  1), vec3(-1,  0,  1), vec3( 1,  0, -1), vec3(-1, 0, -1),
   vec3(0, 1,  1), vec3( 0, -1,  1), vec3( 0, -1, -1), vec3( 0, 1, -1)
);

float ShadowCalculation(vec3 lightPos_world) {
    // get vector between fragment position and light position
    vec3 fragToLight = WorldFragPos - lightPos_world;
    // now get current linear depth as the length between the fragment and light position
    float currentDepth = length(fragToLight);

    float shadow = 0.0;
    float bias = 0.15;
    int samples = 20;
    float viewDistance = length(viewPos_world - WorldFragPos);
    float diskRadius = (1.0 + (viewDistance / far_plane)) / 25.0;
    for(int i = 0; i < samples; ++i) {
        float closestDepth = texture(shadowMap, fragToLight + gridSamplingDisk[i] * diskRadius).r;
        closestDepth *= far_plane;   // undo mapping [0;1]
        if (currentDepth - bias > closestDepth)
            shadow += 1.0;
    }
    shadow /= float(samples);
    
    // display closestDepth as debug (to visualize depth cubemap)
    // FragColor = vec4(vec3(closestDepth / far_plane), 1.0); 

    return shadow;
}

vec2 ParallaxMapping(vec2 texCoords, vec3 viewDir_tangent) { 
    // number of depth layers
    const float minLayers = 10;
    const float maxLayers = 20;
    float numLayers = mix(maxLayers, minLayers, abs(dot(vec3(0.0, 0.0, 1.0), viewDir_tangent)));  
    // calculate the size of each layer
    float layerDepth = 1.0 / numLayers;
    // depth of current layer
    float currentLayerDepth = 0.0;
    // the amount to shift the texture coordinates per layer (from vector P)
    vec2 P = viewDir_tangent.xy / viewDir_tangent.z * height_scale; 
    vec2 deltaTexCoords = P / numLayers;
  
    // get initial values
    vec2  currentTexCoords = texCoords;
    float currentDepthMapValue = 1 - texture(material.texture_height1, currentTexCoords).r;
      
    while(currentLayerDepth < currentDepthMapValue) {
        // shift texture coordinates along direction of P
        currentTexCoords -= deltaTexCoords;
        // get depthmap value at current texture coordinates
        currentDepthMapValue = 1 - texture(material.texture_height1, currentTexCoords).r;  
        // get depth of next layer
        currentLayerDepth += layerDepth;  
    }
    
    // -- parallax occlusion mapping interpolation from here on
    // get texture coordinates before collision (reverse operations)
    vec2 prevTexCoords = currentTexCoords + deltaTexCoords;

    // get depth after and before collision for linear interpolation
    float afterDepth  = currentDepthMapValue - currentLayerDepth;
    float beforeDepth = 1 - texture(material.texture_height1, prevTexCoords).r - currentLayerDepth + layerDepth;
 
    // interpolation of texture coordinates
    float weight = afterDepth / (afterDepth - beforeDepth);
    vec2 finalTexCoords = prevTexCoords * weight + currentTexCoords * (1.0 - weight);

    return finalTexCoords;
}

float Pow5(float v) {
	return v * v * v * v * v;
}

float sqr(float x) { return x*x; }

vec3 Diffuse_Burley_Disney(vec3 diffuseColor, float roughness, float NdotV, float NdotL, float VdotH) {
	float FD90 = 0.5 + 2 * VdotH * VdotH * roughness;
	float FdV = 1 + (FD90 - 1) * Pow5( 1 - NdotV );
	float FdL = 1 + (FD90 - 1) * Pow5( 1 - NdotL );
	return diffuseColor * ( (1 / PI) * FdV * FdL );
}

// ----------------------------------------------------------------------------
// Generalized-Trowbridge-Reitz distribution
float D_GTR1(float roughness, vec3 N, vec3 H) {
    float a = roughness*roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float cos2th = NdotH * NdotH;
    float den = (1.0 + (a2 - 1.0) * cos2th);

    return (a2 - 1.0) / (PI * log(a2) * den);
}

float D_GTR2(float roughness, vec3 N, vec3 H) {
    float a = roughness*roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float cos2th = NdotH * NdotH;
    float den = (1.0 + (a2 - 1.0) * cos2th);

    return a2 / (PI * den * den);
}

float GTR2_aniso(float NdotH, float HdotX, float HdotY, float ax, float ay) {
    return 1 / (PI * ax*ay * sqr( sqr(HdotX/ax) + sqr(HdotY/ay) + NdotH*NdotH ));
}

// ----------------------------------------------------------------------------
float smithG_GGX(float NdotV, float roughness) {
    float r = (roughness + 1.0);
    float alphaG = (r*r) / 4.0;

    float a = alphaG*alphaG;
    float b = NdotV*NdotV;
    return 1 / (NdotV + sqrt(a + b - a*b));
}

float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness) {
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2 = smithG_GGX(NdotV, roughness);
    float ggx1 = smithG_GGX(NdotL, roughness);

    return ggx1 * ggx2;
}

// ----------------------------------------------------------------------------
vec3 fresnelSchlick(float cosTheta, vec3 F0) {
    return F0 + (1.0 - F0) * Pow5(clamp(1.0 - cosTheta, 0.0, 1.0));
}

vec3 fresnelSchlickRoughness(float cosTheta, vec3 F0, float roughness) {
    return F0 + (max(vec3(1.0 - roughness), F0) - F0) * Pow5(1.0 - cosTheta);
} 

// ----------------------------------------------------------------------------
void main() {
    vec3 viewDir_tangent = normalize(TangentViewPos - TangentFragPos);

    vec2 texCoords = TexCoords;
    if (parallax)
        texCoords = ParallaxMapping(TexCoords,  viewDir_tangent);
    // discards a fragment when sampling outside default texture region (fixes border artifacts)
    if (texCoords.x > 1.0 || texCoords.y > 1.0 || texCoords.x < 0.0 || texCoords.y < 0.0)
        discard;

    // Obtain normal from normal map in range [0,1]
    vec3 normal_tangent = texture(material.texture_normal1, texCoords).rgb;
    // Transform normal vector to range [-1,1]
    normal_tangent = normalize(normal_tangent * 2.0 - 1.0);  // this normal is in tangent space

    vec3 albedo = texture(material.texture_albedo1, texCoords).rgb;
    float metallic = texture(material.texture_metallic1, texCoords).r;
    float roughness = texture(material.texture_roughness1, texCoords).r;
    float ao = texture(material.texture_ao1, texCoords).r;

    // calculate reflectance at normal incidence; if dia-electric (like plastic) use F0 
    // of 0.04 and if it's a metal, use the albedo color as F0 (metallic workflow)    
    vec3 F0 = vec3(0.04); 
    F0 = mix(F0, albedo, metallic);

    // reflectance equation
    vec3 Lo = vec3(0.0);
    for(int i = 0; i < NR_POINT_LIGHTS; i++) {
        vec3 lightDir_tangent = normalize(TangentLightPos - TangentFragPos);
        vec3 halfwayDir_tangent = normalize(lightDir_tangent + viewDir_tangent);  
        // shadow
        float shadow = 0.0;
        if (shadows) 
            shadow = ShadowCalculation(pointLights[i].position_world);       
        // attenuation
        float distance = length(pointLights[i].position_world - WorldFragPos);
        // float attenuation = 1.0 / (light.constant + light.linear * distance + light.quadratic * (distance * distance));    
        //float attenuation = 1.0 / (distance * distance);
        float attenuation = 1.0;
        vec3 radiance = pointLights[i].color * attenuation;
    
        // Cook-Torrance BRDF
        float D = D_GTR2(roughness, normal_tangent, halfwayDir_tangent); 
        float G = GeometrySmith(normal_tangent, viewDir_tangent, lightDir_tangent, roughness);      
        vec3 F = fresnelSchlick(max(dot(halfwayDir_tangent, viewDir_tangent), 0.0), F0);

        vec3 numerator    = D * G * F; 
        float denominator = 4.0 * max(dot(normal_tangent, viewDir_tangent), 0.0) * max(dot(normal_tangent, lightDir_tangent), 0.0) + 0.0001; // + 0.0001 to prevent divide by zero
        vec3 specular = numerator / denominator;

        float NdotV = max(dot(normal_tangent, viewDir_tangent), 0.0);
        float NdotL = max(dot(normal_tangent, lightDir_tangent), 0.0);
        float VdotH = max(dot(viewDir_tangent, halfwayDir_tangent), 0.0);
        
        vec3 diffuse = Diffuse_Burley_Disney(albedo, roughness, NdotV, NdotL, VdotH) * (1-metallic);

        // add to outgoing radiance Lo
        Lo += (1.0 - shadow) * (diffuse + specular) * radiance * NdotL;  // note that we already multiplied the BRDF by the Fresnel (kS) so we won't multiply by kS again 
    }

    // -----------------------------haven't updated to fit Disney brdf-------------------------------
    // ambient lighting (we now use IBL as the ambient term)
    vec3 F = fresnelSchlickRoughness(max(dot(normal_tangent, viewDir_tangent), 0.0), F0, roughness);
    vec3 kS = F;
    vec3 kD = 1.0 - kS;
    kD *= 1.0 - metallic;
    
    vec3 irradiance = texture(irradianceMap, WorldNormal).rgb;
    vec3 diffuse = irradiance * albedo;

    // sample both the pre-filter map and the BRDF lut and combine them together as per the Split-Sum approximation to get the IBL specular part.
    const float MAX_REFLECTION_LOD = 4.0;
    vec3 viewDir_world = normalize(viewPos_world - WorldFragPos);
    vec3 reflection_world = reflect(-viewDir_world, WorldNormal);
    vec3 prefilteredColor = textureLod(prefilterMap, reflection_world, roughness * MAX_REFLECTION_LOD).rgb;    
    vec2 brdf = texture(brdfLUT, vec2(max(dot(normal_tangent, viewDir_tangent), 0.0), roughness)).rg;
    vec3 specular = prefilteredColor * (F * brdf.x + brdf.y);

    vec3 ambient = (kD * diffuse + specular) * ao;
    // -----------------------------------------------------------------------------------------------
    
    vec3 color = ambient + Lo;
    if (hasEmissive) {
        color += texture(material.texture_emissive1, texCoords).rgb;
    }
    
    FragColor = vec4(color, 1.0);
    if (hasOpacity && texture(material.texture_opacity1, texCoords).r < 1.0) {
        //FragColor = vec4(color, texture(material.texture_opacity1, texCoords).r);
    }
    
    float brightness = dot(color, vec3(0.2126, 0.7152, 0.0722));
    if(brightness > 4.99)
        BrightColor = vec4(color, 1.0);
    else
        BrightColor = vec4(0.0, 0.0, 0.0, 1.0);
}
