#version 330

uniform vec4 DrawColor;
out vec4 gFragColor;

void main (void) {
    gFragColor = DrawColor;
}