#ifndef SHADER_H
#define SHADER_H

#include <glad/glad.h>

#include <string>
#include <fstream>
#include <sstream>
#include <iostream>
using namespace std;

class Shader {
public:
    unsigned int ID;
    // constructor generates the shader on the fly
    // ------------------------------------------------------------------------
    Shader(const char* vertexPath, const char* geometryPath, const char* fragmentPath) {
        ID = glCreateProgram();
        // vertex shader
        unsigned int vertexShader = createShader(vertexPath, "VERTEX");
        glAttachShader(ID, vertexShader);
        // fragment shader
        unsigned int fragmentShader = createShader(fragmentPath, "FRAGMENT");
        glAttachShader(ID, fragmentShader);
        // geometry shader
        unsigned int geometryShader = 0;
        if (geometryPath[0] != '\0') {
            geometryShader = createShader(geometryPath, "GEOMETRY");
            glAttachShader(ID, geometryShader);
        }
        // link shaders
        glLinkProgram(ID);
        checkCompileErrors(ID, "PROGRAM");
        // delete the shaders as they're linked into our program now and no longer necessary
        glDeleteShader(vertexShader);
        glDeleteShader(fragmentShader);
        if (geometryPath[0] != '\0') {
            glDeleteShader(geometryShader);
        }
    }
    // activate the shader before any calls to glUniform
    // (finding the uniform location does not require you to use the shader program first, but updating a uniform does require you to first use the program (by calling glUseProgram), because it sets the uniform on the currently active shader program.)
    // ------------------------------------------------------------------------
    void use() {
        glUseProgram(ID);
    }
    // utility uniform functions
    // ------------------------------------------------------------------------
    void setBoolUniform(const string& name, bool value) const {
        glUniform1i(glGetUniformLocation(ID, name.c_str()), (int)value);
    }
    // ------------------------------------------------------------------------
    void setIntUniform(const string& name, int value) const {
        glUniform1i(glGetUniformLocation(ID, name.c_str()), value);
    }
    // ------------------------------------------------------------------------
    void setFloatUniform(const string& name, float value) const {
        glUniform1f(glGetUniformLocation(ID, name.c_str()), value);
    }
    // ------------------------------------------------------------------------
    void setMat4Uniform(const string& name, glm::mat4 value) const {
        glUniformMatrix4fv(glGetUniformLocation(ID, name.c_str()), 1, GL_FALSE, glm::value_ptr(value));
        // glUniformMatrix4fv(glGetUniformLocation(shaderProgram, name.c_str()), 1, GL_FALSE, &value[0][0]);
    }
    // ------------------------------------------------------------------------
    void setVec3Uniform(const string& name, float value1, float value2, float value3) const {
        glUniform3f(glGetUniformLocation(ID, name.c_str()), value1, value2, value3);
    }
    // ------------------------------------------------------------------------
    void setVec3Uniform(const string& name, glm::vec3 value) const {
        glUniform3f(glGetUniformLocation(ID, name.c_str()), value.x, value.y, value.z);
    }
    // ------------------------------------------------------------------------
    void setVec3Uniform(const string& name, float value) const {
        glUniform3f(glGetUniformLocation(ID, name.c_str()), value, value, value);
    }

    glm::vec3 getVec3Uniform(const string& name) const {
        GLfloat vector[3];
        glGetUniformfv(ID, glGetUniformLocation(ID, name.c_str()), vector);
        return glm::vec3(vector[0], vector[1], vector[2]);
    }

private:
    // utility function for checking shader compilation/linking errors.
    // ------------------------------------------------------------------------
    void checkCompileErrors(unsigned int shader, string type) {
        int success;
        char infoLog[1024];
        if (type != "PROGRAM")
        {
            glGetShaderiv(shader, GL_COMPILE_STATUS, &success);
            if (!success)
            {
                glGetShaderInfoLog(shader, 1024, NULL, infoLog);
                cout << "ERROR::SHADER_COMPILATION_ERROR of type: " << type << "\n" << infoLog << "\n -- --------------------------------------------------- -- " << endl;
            }
        }
        else
        {
            glGetProgramiv(shader, GL_LINK_STATUS, &success);
            if (!success)
            {
                glGetProgramInfoLog(shader, 1024, NULL, infoLog);
                cout << "ERROR::PROGRAM_LINKING_ERROR of type: " << type << "\n" << infoLog << "\n -- --------------------------------------------------- -- " << endl;
            }
        }
    }

    int createShader(const char* path, string type) {
        // 1. retrieve the vertex/fragment source code from filePath
        string shaderString;
        ifstream shaderFile;
        // ensure ifstream objects can throw exceptions:
        shaderFile.exceptions(ifstream::failbit | ifstream::badbit);
        try {
            // open files
            shaderFile.open(path);
            stringstream shaderStream;
            // read file's buffer contents into streams
            shaderStream << shaderFile.rdbuf();
            // close file handlers
            shaderFile.close();
            // convert stream into string
            shaderString = shaderStream.str();
        }
        catch (ifstream::failure& e) {
            cout << "ERROR::SHADER::FILE_NOT_SUCCESFULLY_READ: " << e.what() << std::endl;
        }
        const char* shaderCode = shaderString.c_str();

        // 2. build and compile our shader program
        // vertex shader
        unsigned int shader = 0;
        if (type == "VERTEX") {
            shader = glCreateShader(GL_VERTEX_SHADER);
        }
        else if (type == "GEOMETRY") {
            shader = glCreateShader(GL_GEOMETRY_SHADER);
        }
        else if (type == "FRAGMENT") {
            shader = glCreateShader(GL_FRAGMENT_SHADER);
        }
        glShaderSource(shader, 1, &shaderCode, NULL);
        glCompileShader(shader);
        checkCompileErrors(shader, type);

        return shader;
    }
};
#endif