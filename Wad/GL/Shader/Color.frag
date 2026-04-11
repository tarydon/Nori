// Draws one pixel with a color that is passed in as part of vertex data.
// This is passed into this shader from the vertex shader.
#version 330

in vec4 vColor;
out vec4 gFragColor;

void main (void) {
   gFragColor = vColor;
}
