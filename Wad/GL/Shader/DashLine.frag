#version 330

uniform vec4 DrawColor;
uniform float LineWidth;
uniform float LineType;
uniform sampler2D LTypeTexture;

in float gDist;
in float gTexCoord;
out vec4 gFragColor;

void main (void) {
   float d = abs (gDist) / LineWidth;
   float a1 = exp2 (-2 * d * d);
   float a2 = texture2D (LTypeTexture, vec2 (gTexCoord, LineType)).r;
   gFragColor = vec4 (DrawColor.rgb, a1 * a2);
}
