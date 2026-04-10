#version 330

uniform vec4 DrawColor;
uniform vec4 BorderColor;

in vec2 gPos;
flat in ivec2 gSize;
flat in int gRadius;
flat in int gBorder;

void main () {
   float d = length (max (abs (gPos), gSize) - gSize) - gRadius - 1.5;
   if (d < -gBorder) gl_FragColor = mix (DrawColor, BorderColor, clamp (d + gBorder + 1, 0, 1));
   else gl_FragColor = vec4 (BorderColor.rgb, clamp (-d, 0, 1));
}
