#version 330 core

// Inputs
in vec2 vLocalPos;
flat in vec2 vHSize;
flat in float vRadius;
flat in float vBorderThickness;

flat in vec4 vFillColor;
flat in vec4 vBorderColor;

const float vContactShadowBlur = 2;
const float vContactShadowOpacity = 0.25;
const vec2 vContactShadowOffset = vec2 (0, 0);

const float vAmbientShadowBlur = 12;
const vec2 vAmbientShadowOffset = vec2 (0, 7);
const float vAmbientShadowOpacity = 0.45;

const float vInsetShadowBlur = 3;
const float vInsetShadowOpacity = 0.5;
const vec2 vInsetShadowOffset = vec2 (0, 1);

// Outputs
out vec4 gFragColor;

// A signed-distance function for rounded rectangles.
// At the edge, this returns 0. At points outside the edge, this returns +ve
// values, at points inside the edge it returns -ve values (distance to closest)
// point on the boundary
float SDRoundRect (vec2 pos, vec2 size, float rad) {
   vec2 q = abs (pos) - size + rad;
   return length (max (q, 0)) + min (max (q.x, q.y), 0) - rad;
}

vec2 Erf (vec2 x) {
   vec2 s = sign (x);
   x = abs (x);
   vec2 a = 1.0 + (0.278393 + (0.230389 + 0.078108 * x * x) * x) * x;
   a *= a;
   return s - s / (a * a);
}

float RectShadow (vec2 p, vec2 halfSize, float blur) {
   vec2 scale = vec2 (1.0 / (blur * 1.41421356237));
   vec2 low = (-halfSize - p) * scale;
   vec2 high = (halfSize - p) * scale;
   vec2 integral = 0.5 * (Erf (high) - Erf (low));
   return integral.x * integral.y;
}

void main () {
   float d = SDRoundRect (vLocalPos, vHSize, vRadius);
   float aa = max (fwidth (d) * 0.5, 0.25);

   float outerCoverage = smoothstep (aa, -aa, d);
   float fillCoverage = smoothstep (aa, -aa, d + vBorderThickness);
   float borderCoverage = outerCoverage - fillCoverage;
   vec4 borderColor = vBorderColor * borderCoverage;
   vec4 fillColor = vFillColor * fillCoverage;

   float shadowD = SDRoundRect (vLocalPos - vContactShadowOffset, vHSize, vRadius);
   float contactShadow = 1 - smoothstep (0, vContactShadowBlur, max (shadowD, 0));
   contactShadow *= vContactShadowOpacity;

   float ambientShadow = RectShadow (vLocalPos - vAmbientShadowOffset, vHSize, vAmbientShadowBlur);
   ambientShadow *= vAmbientShadowOpacity;
   ambientShadow *= (1.0 - outerCoverage);

   float insetD = SDRoundRect (vLocalPos - vInsetShadowOffset, vHSize, vRadius) + vBorderThickness;
   float insetShadow = 1 - smoothstep (0, -vInsetShadowBlur, insetD);
   insetShadow *= vInsetShadowOpacity;
   insetShadow *= fillCoverage;
   vec4 insetShadowColor = vec4 (0, 0, 0, insetShadow);

   float shadowAlpha = ambientShadow + contactShadow * (1 - ambientShadow);
   vec4 shadowColor = vec4 (0, 0, 0, shadowAlpha);

   vec4 shapeColor = borderColor + fillColor * (1 - borderColor.a);
   shapeColor = shapeColor * (1 - insetShadowColor.a);
   gFragColor = shapeColor + shadowColor * (1 - shapeColor.a);
}
