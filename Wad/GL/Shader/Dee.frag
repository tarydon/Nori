#version 330

uniform vec4 DrawColor;

in vec2 gPos;
flat in ivec2 gSize;
flat in int gRadius;
flat in int gSide;

void main () {
   float d = length (max (abs (gPos), gSize) - gSize) - gRadius - 1.5;
   float a = clamp (-d, 0, 1); 
   switch (gSide) {
      case 0: if (gPos.x > 0) a = 1; break;
      case 1: if (gPos.y < 0) a = 1; break;
      case 2: if (gPos.x < 0) a = 1; break;
      case 3: if (gPos.y > 0) a = 1; break;
   }
   if (a < 0.001) discard;
   gl_FragColor = vec4 (DrawColor.rgb, a);
}
