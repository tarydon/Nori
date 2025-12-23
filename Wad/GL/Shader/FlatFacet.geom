#version 330

uniform vec4 DrawColor;

layout (triangles) in;
layout (triangle_strip, max_vertices = 3) out;

flat out vec4 vLightIntensity;
in vec3 vNormal[];

const vec3 LightPosition = vec3 (0, 0, 1);
const vec4 AmbientColor = vec4 (0.1, 0.1, 0.1, 1);
const vec4 SpecularColor = vec4 (1, 1, 1, 1);
const float SpecularExponent = 100;

void main () {
   // Average the normal values for flat shading
   vec3 tnorm = normalize (vNormal[0] + vNormal[1] + vNormal[2]);
   float dotp = abs (dot (LightPosition, tnorm)); 
   vec4 ambDiffuse = DrawColor * 0.9 * dotp + AmbientColor;
   vec4 specular = SpecularColor * pow (abs (dot (tnorm, vec3 (0, 0, 1))), SpecularExponent);
   vLightIntensity = ambDiffuse + specular;

   for (int i = 0; i < 3; i++){
      gl_Position = gl_in[i].gl_Position;
      EmitVertex();
   }

   EndPrimitive();
}