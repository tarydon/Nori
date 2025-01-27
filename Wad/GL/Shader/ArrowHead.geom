#version 330

uniform vec2 VPScale;
uniform float ArrowSize;
      
layout (lines) in;
layout (triangle_strip, max_vertices = 3) out;

out float gAlpha;
out vec3 gEdgeDist;

void main (void) { 
   vec2 invScale = 1 / VPScale;
   vec2 p0 = gl_in[0].gl_Position.xy * invScale;   // Now in pixel coordinates
   vec2 p1 = gl_in[1].gl_Position.xy * invScale;

   float dx = p1.x - p0.x, dy = p1.y - p0.y, angle = 0.0;
   // Compute the angle of the arrowhead
   if (dx != 0 || dy != 0) angle = atan (dy, dx);

   // Compute the size (in pixels) and the space available
   float size = ArrowSize, space = distance (p0, p1) / 3.0;
   float ratio = space / size;
   if (ratio > 1) {
      float fCos = cos (angle) * size / 2, fSin = sin (angle) * size / 2;
      vec2 dir = vec2 (fCos, fSin);
      vec2 perpDir = vec2 (fSin / 1.3333, -fCos / 1.3333);
      vec2 pt = (p0 + p1) / 2;

      gAlpha = min ((ratio - 1), 1);
      gEdgeDist = vec3 (size, 0, 0);
      gl_Position = vec4 (VPScale * (pt + dir), 0, 1);
      EmitVertex ();

      gEdgeDist = vec3 (0, size * 0.70225, 0);
      gl_Position = vec4 (VPScale * (pt - dir - perpDir), 0, 1);
      EmitVertex ();

      gEdgeDist = vec3 (0, 0, size * 0.70225);
      gl_Position = vec4 (VPScale * (pt - dir + perpDir), 0, 1);
      EmitVertex ();

      EndPrimitive ();   
   }
}
