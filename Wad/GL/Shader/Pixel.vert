#version 330

uniform vec2 VPScale;

layout (location = 0) in vec2 Pos;

void main () {
   gl_Position = vec4 ((Pos + vec2 (0.5, 0.5)) * VPScale, 0, 1) - vec4 (1, 1, 0, 0);
}
