#version 330 core

in vec2 vLocalPos;

flat in vec2 vHalfSize;
flat in vec4 vFillColor;

out vec4 gFragColor;

void main () {
   gFragColor = vec4 (1, 0, 0, 1); 
}