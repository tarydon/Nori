// Experimental UI Rect shader

#version 330 core

// Constants
uniform vec2 VPScale;      // = 2.0 / ViewportSize

// Inputs
layout (location = 0) in ivec2 Center;
layout (location = 1) in uvec2 HSize;
layout (location = 2) in uint FillColor;

layout (location = 3) in uint Radius;

// Outputs
out vec2 vLocalPos;
flat out vec2 vHSize;
flat out float vRadius;

flat out vec4 vFillColor;

// Generate quad corners procedurally
vec2 GetCorner (int vertexID) {
   switch (vertexID) {
      case 0: return vec2 (-1, -1);
      case 1: return vec2 (1, -1);
      case 2: return vec2 (1, 1);
      default: return vec2 (-1, 1);
   }
}

void main () {
   // Expand for shadows and anti-aliasing
   float expand = 4.0;
   vec2 expandedHSize = vec2 (HSize) + expand;

   // Get the corner (based on instanced vertex ID)
   vec2 corner = GetCorner (gl_VertexID);
   vLocalPos = corner * expandedHSize;    // Output to fragment shader

   // Screen space position, and in NDC
   vec2 screenPos = vec2 (Center) + vLocalPos;
   vec2 ndc = screenPos * VPScale - 1.0;
   ndc.y = -ndc.y;      // Origin is top left
   gl_Position = vec4 (ndc, 0, 1);

   // Pass through parameters
   vHSize = vec2 (HSize);
   vRadius = float (Radius);
   vFillColor = unpackUnorm4x8 (FillColor);
}
