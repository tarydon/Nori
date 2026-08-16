#version 310 es
precision highp float;

uniform vec2 VPScale;

layout (location = 0) in ivec2 Pos;

void main () {
   vec2 pix = ((vec2 (Pos) + vec2 (0.5, 0.5)) * VPScale);
   gl_Position = vec4 (pix.x - 1.0, 1.0 - pix.y, 0.0, 1.0);
}
