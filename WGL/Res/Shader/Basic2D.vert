#version 330
      
uniform mat4 Xfm;

layout (location = 0) in vec2 VertexPos;
      
void main () {
   gl_Position = Xfm * vec4 (VertexPos, 0, 1);
}
