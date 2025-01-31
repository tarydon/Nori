#version 330

uniform mat4 Xfm;
uniform mat4 NormalXfm;
uniform vec4 DrawColor;

layout (location = 0) in vec3 Position;
layout (location = 1) in vec3 Normal;
out vec4 vLightIntensity;

const vec3 LightPosition = vec3 (0, 0, 1);
const vec4 AmbientColor = vec4 (0.1, 0.1, 0.1, 1);
const vec4 SpecularColor = vec4 (1, 1, 1, 1);
const float SpecularExponent = 100;

void main (void) {
   vec3 tnorm = normalize ((NormalXfm * vec4 (Normal, 1)).xyz);

   float dotp = abs (dot (LightPosition, tnorm)); 
   vec4 ambDiffuse = DrawColor * 0.9 * dotp + AmbientColor;
   vec4 specular = SpecularColor * pow (abs (dot (tnorm, vec3 (0, 0, 1))), SpecularExponent);
   
   vLightIntensity = ambDiffuse + specular;
   gl_Position = Xfm * vec4 (Position, 1);
}
