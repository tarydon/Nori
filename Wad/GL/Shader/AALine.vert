#version 310 es
precision highp float;

layout (location = 0) in vec2 P0;
layout (location = 1) in vec2 P1;

uniform mat4 Xfm;
uniform float LineWidth;

void main()
{
    float x = (gl_VertexID & 1) != 0 ? 1.0 : -1.0;
    float y = (gl_VertexID & 2) != 0 ? 1.0 : -1.0;

    vec2 d = normalize (P1 - P0);
    vec2 n = vec2 (-d.y, d.x);

    vec2 p = mix (P0, P1, (y + 1.0) * 0.5);
    p += n * x * LineWidth * 0.5;

    gl_Position = Xfm * vec4 (p, 0.0, 1.0);
}
