#version 330 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec2 aTexCoords;
layout (location = 3) in vec3 aTangent;

out vec3 WorldFragPos;
out vec3 WorldNormal;
out vec2 TexCoords;
out vec3 TangentLightPos;
out vec3 TangentViewPos;
out vec3 TangentFragPos;

layout (std140) uniform Matrices {
    mat4 projection;
    mat4 view;
};
uniform mat4 model;
uniform mat4 normalMatrix;

uniform vec3 lightPos;
uniform vec3 viewPos;

void main()
{
    WorldFragPos = vec3(model * vec4(aPos, 1.0));
    WorldNormal = normalize(mat3(normalMatrix) * aNormal);
    TexCoords = aTexCoords;

    vec3 T = normalize(mat3(normalMatrix) * aTangent);
    vec3 N = WorldNormal;
    T = normalize(T - dot(T, N) * N);
    vec3 B = cross(N, T);
    mat3 TBN = transpose(mat3(T, B, N));

    TangentLightPos = TBN * lightPos;
    TangentViewPos  = TBN * viewPos;
    TangentFragPos  = TBN * WorldFragPos;

    gl_Position = projection * view * model * vec4(aPos, 1.0f);
}