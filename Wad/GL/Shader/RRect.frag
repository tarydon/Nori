#version 330

uniform vec4 DrawColor;

in vec2 gPos;
flat in ivec2 gSize;
flat in int gRadius;

void main () {
   float d = length (max (abs (gPos), gSize) - gSize) - gRadius - 1.5;
   float a = clamp (-d, 0, 1); 
   if (a < 0.001) discard;
   gl_FragColor = vec4 (DrawColor.rgb, a);
}
