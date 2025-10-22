#version 330 core
out vec4 FragColor;
uniform float u_time;
uniform vec2  u_resolution;

float sdCircle(vec2 p, float r){ return length(p)-r; }

void main() {
    vec2 uv = (gl_FragCoord.xy / u_resolution) * 2.0 - 1.0;
    uv.x *= u_resolution.x / u_resolution.y;

    // orbiting circles
    float t = u_time;
    vec2 c1 = 0.5*vec2(cos(t*0.9), sin(t*0.9));
    vec2 c2 = 0.6*vec2(cos(-t*1.3), sin(-t*1.0+1.0));
    float d1 = sdCircle(uv - c1, 0.35);
    float d2 = sdCircle(uv - c2, 0.28);

    float m = smoothstep(0.01, 0.0, d1) + smoothstep(0.01, 0.0, d2);
    vec3 col = mix(vec3(0.02,0.02,0.02), vec3(0.9,0.4,1.0), m);
    FragColor = vec4(col, 1.0);
}
