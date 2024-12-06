#version 150
#extension GL_ARB_explicit_attrib_location : enable

uniform mat4 Xfm;

layout (location = 0) in vec2 VertexPos;
layout (location = 1) in vec2 Offset;
layout (location = 2) in float Scale;
layout (location = 3) in int Character;

out int vCharacter;
out vec2 vOffset;
out float vScale;

void main (void) {
   vCharacter = Character;
   vOffset = Offset;
   vScale = Scale;
   gl_Position = Xfm * vec4 (VertexPos, 0, 1);
}
