#version 330

uniform vec4 DrawColor;

in float gAlpha;
in vec3 gEdgeDist;

void main (void) {
   float d = min (min (gEdgeDist.x, gEdgeDist.y), gEdgeDist.z);
   gl_FragColor = vec4 (DrawColor.rgb, min (d, 1) * gAlpha);
}