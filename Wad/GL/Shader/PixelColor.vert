// Vertex shader that takes one pixel coordinate (top-left = 0,0),
// and a Color value - the pixel is converted to OpenGL clip space (-1..1,-1..1)
// and the color value is passed through to the fragment shader in the vColor
// transfer variable
#version 330

uniform vec2 VPScale;

layout (location = 0) in ivec2 Pos;
layout (location = 1) in vec4 Color;

out vec4 vColor;

void main () {
   vColor = Color;
   vec2 pix = ((vec2 (Pos) + vec2 (0.5, 0.5)) * VPScale);
   gl_Position = vec4 (pix.x - 1, 1 - pix.y, 0, 1);
}
