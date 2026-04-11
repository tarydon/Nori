// Vertex shader for drawing rounded-rectangles, in pixels.
// The (0,0) maps to the bottom left of the screen, and +X is to the right, 
// and +Y is upwards.
// Uniforms:
//    VPScale : The precomputed value (2.0 / ViewportX, 2.0 / ViewportY)
//    DrawColor : Fill color for the rounded rectangle
// Attributes: 
//    Box : Pixel coordinates of the box (lower left corner in xy, top left corner in wz)

#version 330

uniform vec2 VPScale;

layout (location = 0) in ivec4 Box;

void main () {
   ivec2 vSize = ivec2 (Box.z - Box.x, Box.w - Box.y);                  // Size, in pixels
   vec2 pxCenter = vec2 ((Box.x + Box.z) / 2, (Box.y + Box.w) / 2);		// Center position, in pixels
		
	vec2 adjust = mod (vSize, 2) / 2.0;
   vec2 pix = (pxCenter + adjust) * VPScale;
	gl_Position = vec4 (pix.x - 1, 1 - pix.y, vSize * VPScale);
}
