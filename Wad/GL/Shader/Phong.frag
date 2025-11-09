#version 150

uniform vec4 DrawColor;
in vec3 vNormal;
out vec4 gFragColor;

const vec3 LightPosition = vec3 (0, 0, 1);
const vec4 AmbientColor = vec4 (0.1, 0.1, 0.1, 1);
const vec4 SpecularColor = vec4 (1, 1, 1, 1);
const float SpecularExponent = 64;

void main () {
   vec3 tnorm = normalize (vNormal);
   float dotp = abs (dot (LightPosition, tnorm));
   vec4 color = DrawColor;
   if (!gl_FrontFacing) color = vec4 (1, 0, 0, 1);
   vec4 ambDiffuse = color * 0.9 * dotp + AmbientColor;
   vec4 specular = SpecularColor * pow (abs (dot (tnorm, vec3 (0, 0, 1))), SpecularExponent);
   vec4 lightIntensity = ambDiffuse + specular;
   gFragColor = vec4 (lightIntensity.rgb, DrawColor.a);
}
