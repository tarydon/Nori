#version 330

uniform vec4 DrawColor;
uniform vec4 BorderColor;

in vec2 gPos;
flat in ivec2 gSize;
flat in int gBorder;
out vec4 gFragColor;

void main () {
   vec2 vec = gSize - abs (gPos);
   float d = min (vec.x, vec.y);
   if (d < gBorder) gFragColor = BorderColor;
   else gFragColor = DrawColor;
}
