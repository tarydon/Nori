#version 330

uniform mat4 Xfm;
uniform mat4 NormalXfm;

layout (location = 0) in vec3 Position;
layout (location = 1) in vec3 Normal;
layout (location = 2) in vec2 TexCoord;

out vec2 vTexCoord;
out float vLighting;

const vec3 LightPosition = vec3 (0, 0, 1);
const float SpecularExponent = 100;

void main (void) {
   vec3 tnorm = normalize ((NormalXfm * vec4 (Normal, 1)).xyz);
   float dotp = abs (dot (LightPosition, tnorm));
   float ambDiffuse = dotp + 0.1;
   float specular = 0.1 * pow (abs (dot (tnorm, vec3 (0, 0, 1))), SpecularExponent);
   vLighting = ambDiffuse + specular;

   gl_Position = Xfm * vec4 (Position, 1);
   vTexCoord = TexCoord;
}
