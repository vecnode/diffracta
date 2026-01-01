#version 330 core
out vec4 FragColor;
uniform float u_time;
uniform vec2  u_resolution;

float random(vec2 st) {
    return fract(sin(dot(st.xy, vec2(12.9898, 78.233))) * 43758.5453123);
}

float noise(vec2 st) {
    vec2 i = floor(st);
    vec2 f = fract(st);
    
    float a = random(i);
    float b = random(i + vec2(1.0, 0.0));
    float c = random(i + vec2(0.0, 1.0));
    float d = random(i + vec2(1.0, 1.0));
    
    vec2 u = f * f * (3.0 - 2.0 * f);
    
    return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

void main() {
    vec2 uv = gl_FragCoord.xy / u_resolution;
    
    // More aggressive noise with higher frequencies
    float n = 0.0;
    float amplitude = 1.0;
    float frequency = 3.0;
    
    for(int i = 0; i < 6; i++) {
        n += noise(uv * frequency + u_time * 0.5) * amplitude;
        amplitude *= 0.4;
        frequency *= 2.5;
    }
    
    // Add spiraling pattern
    float angle = atan(uv.y - 0.5, uv.x - 0.5) + n * 2.0;
    float radius = length(uv - 0.5) + u_time * 0.2;
    float spiral = sin(angle * 4.0 + radius * 6.0);
    
    // Electric blue-purple color scheme
    vec3 color1 = vec3(0.1, 0.2, 0.8);
    vec3 color2 = vec3(0.3, 0.1, 0.9);
    vec3 color3 = vec3(0.6, 0.2, 1.0);
    vec3 color4 = vec3(0.9, 0.4, 0.5);
    
    float t = (spiral + n) * 0.5;
    vec3 color = mix(mix(color1, color2, t), mix(color3, color4, t), t);
    
    FragColor = vec4(color, 1.0);
}
