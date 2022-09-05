#version 330 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec2 aTexCoords;
layout (location = 3) in vec3 aTangent;

out vec3 WorldFragPos;
// out vec3 Normal;
out vec2 TexCoords;
out vec3 TangentLightPos;
out vec3 TangentViewPos;
out vec3 TangentFragPos;

/*
out VS_OUT {
    vec3 FragPos;
    vec3 Normal;
    vec2 TexCoords;
} vs_out;
*/

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
    /*
    FragPos = vec3(model * vec4(aPos, 1.0));
    //Normal = mat3(transpose(inverse(aInstanceMatrix))) * aNormal;
    TexCoords = aTexCoords;
    */
    /*
    vs_out.Normal = mat3(normalMatrix) * aNormal;
    vs_out.FragPos = vec3(model * vec4(aPos, 1.0));
    vs_out.TexCoords = aTexCoords;
    */

    WorldFragPos = vec3(model * vec4(aPos, 1.0));
    TexCoords = aTexCoords;

    vec3 T = normalize(mat3(normalMatrix) * aTangent);
    vec3 N = normalize(mat3(normalMatrix) * aNormal);
    //vec3 T = normalize(vec3(model * vec4(aTangent, 0.0)));
    //vec3 N = normalize(vec3(normalMatrix * vec4(aNormal, 0.0)));
    // re-orthogonalize T with respect to N
    T = normalize(T - dot(T, N) * N);
    // then retrieve perpendicular vector B with the cross product of T and N
    vec3 B = cross(N, T);
    mat3 TBN = transpose(mat3(T, B, N));

    TangentLightPos = TBN * lightPos;
    TangentViewPos  = TBN * viewPos;
    TangentFragPos  = TBN * WorldFragPos;

    gl_Position = projection * view * model * vec4(aPos, 1.0f);
    //gl_Position = vec4(0.1 * aPos, 1.0f);
}