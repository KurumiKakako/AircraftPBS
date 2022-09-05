#version 330 core
out vec4 FragColor;

in vec3 TexCoords;

uniform samplerCube environmentMap;

void main() {    
    FragColor = textureLod(environmentMap, TexCoords, 0);
    //FragColor = vec4(1.0);
}