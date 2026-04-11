#version 330

uniform vec4 DrawColor;

in vec2 gPos;
flat in ivec2 gSize;
flat in int gRadius;
out vec4 gFragColor;

void main () {
   float d = length (max (abs (gPos), gSize) - gSize) - gRadius - 1.5;
   float a = clamp (-d, 0, 1);
   if (a < 0.001) discard;
   gFragColor = vec4 (DrawColor.rgb, a);
}
