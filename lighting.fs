#version 330 core
layout (location = 0) out vec4 FragColor;
layout (location = 1) out vec4 BrightColor;

struct Material {
    sampler2D texture_diffuse1;
    sampler2D texture_specular1;
    // sampler2D texture_reflection1;
    sampler2D texture_normal1;
    sampler2D texture_height1;
    float shininess;
};

struct DirLight {
    vec3 direction;
	
    vec3 ambient;
    vec3 diffuse;
    vec3 specular;
};

struct PointLight {
    vec3 position_world;
    vec3 position_tangent;
    
    float constant;
    float linear;
    float quadratic;
	
    vec3 ambient;
    vec3 diffuse;
    vec3 specular;
};

struct SpotLight {
    vec3 position;
    vec3 direction;
    float cutOff;
    float outerCutOff;
  
    float constant;
    float linear;
    float quadratic;
  
    vec3 ambient;
    vec3 diffuse;
    vec3 specular;       
};

#define NR_POINT_LIGHTS 1

in vec3 WorldFragPos;
in vec2 TexCoords;
in vec3 TangentLightPos;
in vec3 TangentViewPos;
in vec3 TangentFragPos;

uniform vec3 viewPos_world;
//uniform samplerCube skybox;
uniform Material material;
uniform samplerCube shadowMap;

uniform DirLight dirLight;
uniform PointLight pointLights[NR_POINT_LIGHTS];
uniform SpotLight spotLight;

uniform float far_plane;
uniform bool shadows;
uniform bool parallax;
uniform float height_scale;

// function prototypes
vec3 CalcDirLight(DirLight light, vec3 normal, vec3 viewDir, vec2 texCoords);
vec3 CalcPointLight(PointLight light, vec3 normal, vec3 viewDir, vec2 texCoords);
vec3 CalcSpotLight(SpotLight light, vec3 normal, vec3 fragPos, vec2 texCoords);

// array of offset direction for sampling
vec3 gridSamplingDisk[20] = vec3[]
(
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
    float currentDepthMapValue = texture(material.texture_height1, currentTexCoords).r;
      
    while(currentLayerDepth < currentDepthMapValue) {
        // shift texture coordinates along direction of P
        currentTexCoords -= deltaTexCoords;
        // get depthmap value at current texture coordinates
        currentDepthMapValue = texture(material.texture_height1, currentTexCoords).r;  
        // get depth of next layer
        currentLayerDepth += layerDepth;  
    }
    
    // -- parallax occlusion mapping interpolation from here on
    // get texture coordinates before collision (reverse operations)
    vec2 prevTexCoords = currentTexCoords + deltaTexCoords;

    // get depth after and before collision for linear interpolation
    float afterDepth  = currentDepthMapValue - currentLayerDepth;
    float beforeDepth = texture(material.texture_height1, prevTexCoords).r - currentLayerDepth + layerDepth;
 
    // interpolation of texture coordinates
    float weight = afterDepth / (afterDepth - beforeDepth);
    vec2 finalTexCoords = prevTexCoords * weight + currentTexCoords * (1.0 - weight);

    return finalTexCoords;
}

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
    
    vec3 result = vec3(0.0, 0.0, 0.0);
    // phase 1: directional lighting
    // vec3 result = CalcDirLight(dirLight, norm, viewDir);
    // phase 2: point lights.
    for(int i = 0; i < NR_POINT_LIGHTS; i++)
        result += CalcPointLight(pointLights[i], normal_tangent, viewDir_tangent, texCoords);    
    // phase 3: spot light
    // result += CalcSpotLight(spotLight, norm, FragPos, viewDir);    
    
    FragColor = vec4(result, 1.0);
    float brightness = dot(result, vec3(0.2126, 0.7152, 0.0722));
    if(brightness > 1.0)
        BrightColor = vec4(result, 1.0);
    else
        BrightColor = vec4(0.0, 0.0, 0.0, 1.0);
}

// calculates the color when using a directional light.
vec3 CalcDirLight(DirLight light, vec3 normal, vec3 viewDir, vec2 texCoords) {
    vec3 lightDir = normalize(-light.direction);
    // diffuse shading
    float diff = max(dot(normal, lightDir), 0.0);
    // specular shading
    vec3 halfwayDir = normalize(lightDir + viewDir);  
    float spec = pow(max(dot(normal, halfwayDir), 0.0), 32.0);
    // float spec = pow(max(dot(viewDir, reflectDir), 0.0), material.shininess);
    // combine results
    vec3 ambient = light.ambient * vec3(texture(material.texture_diffuse1, texCoords));
    vec3 diffuse = light.diffuse * diff * vec3(texture(material.texture_diffuse1, texCoords));
    vec3 specular = light.specular * spec * vec3(texture(material.texture_specular1, texCoords));
    return (ambient + diffuse + specular);
}

// calculates the color when using a point light.
vec3 CalcPointLight(PointLight light, vec3 normal_tangent, vec3 viewDir_tangent, vec2 texCoords) {
    vec3 lightDir_tangent = normalize(TangentLightPos - TangentFragPos);
    // diffuse shading
    float diff = max(dot(normal_tangent, lightDir_tangent), 0.0);
    // specular shading
    vec3 halfwayDir_tangent = normalize(lightDir_tangent + viewDir_tangent);  
    float spec = pow(max(dot(normal_tangent, halfwayDir_tangent), 0.0), 256.0);
    // shadow
    float shadow = 0.0;
    if (shadows) 
        shadow = ShadowCalculation(light.position_world);       
    // attenuation
    float distance = length(light.position_world - WorldFragPos);
    float attenuation = 1.0 / (light.constant + light.linear * distance + light.quadratic * (distance * distance));    
    // attenuation = 1.0;
    // combine results
    vec3 ambient = light.ambient * vec3(texture(material.texture_diffuse1, texCoords));
    vec3 diffuse = light.diffuse * diff * vec3(texture(material.texture_diffuse1, texCoords));
    vec3 specular = light.specular * spec * vec3(texture(material.texture_specular1, texCoords));
    ambient *= attenuation;
    diffuse *= attenuation;
    specular *= attenuation;
    return ambient + (1.0 - shadow) * (diffuse + specular);
}

// calculates the color when using a spot light.
vec3 CalcSpotLight(SpotLight light, vec3 normal, vec3 fragPos, vec3 viewDir, vec2 texCoords) {
    vec3 lightDir = normalize(light.position - fragPos);
    // diffuse shading
    float diff = max(dot(normal, lightDir), 0.0);
    // specular shading
    vec3 halfwayDir = normalize(lightDir + viewDir);  
    float spec = pow(max(dot(normal, halfwayDir), 0.0), 32.0);
    // float spec = pow(max(dot(viewDir, reflectDir), 0.0), material.shininess);
    // attenuation
    float distance = length(light.position - fragPos);
    float attenuation = 1.0 / (light.constant + light.linear * distance + light.quadratic * (distance * distance));    
    // spotlight intensity
    float theta = dot(lightDir, normalize(-light.direction)); 
    float epsilon = light.cutOff - light.outerCutOff;
    float intensity = clamp((theta - light.outerCutOff) / epsilon, 0.0, 1.0);
    // combine results
    vec3 ambient = light.ambient * vec3(texture(material.texture_diffuse1, texCoords));
    vec3 diffuse = light.diffuse * diff * vec3(texture(material.texture_diffuse1, texCoords));
    vec3 specular = light.specular * spec * vec3(texture(material.texture_specular1, texCoords));
    ambient *= attenuation * intensity;
    diffuse *= attenuation * intensity;
    specular *= attenuation * intensity;
    return (ambient + diffuse + specular);
}