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
    
    // Twisted noise with rotation
    float angle = u_time * 0.3;
    float cos_a = cos(angle);
    float sin_a = sin(angle);
    vec2 rot_uv = vec2(
        cos_a * (uv.x - 0.5) - sin_a * (uv.y - 0.5) + 0.5,
        sin_a * (uv.x - 0.5) + cos_a * (uv.y - 0.5) + 0.5
    );
    
    float n = noise(rot_uv * 3.0);
    float n2 = noise(rot_uv * 6.0 + vec2(u_time * 0.2));
    
    // Create labyrinth-like pattern
    float grid = sin(uv.x * 20.0) * sin(uv.y * 20.0);
    
    // Deep ocean colors
    vec3 color1 = vec3(0.05, 0.1, 0.3);
    vec3 color2 = vec3(0.1, 0.3, 0.6);
    vec3 color3 = vec3(0.2, 0.5, 0.8);
    vec3 color4 = vec3(0.3, 0.7, 0.9);
    
    float t = (n + n2 * 0.5 + grid * 0.3);
    vec3 color = mix(mix(color1, color2, t), mix(color3, color4, t), t);
    
    FragColor = vec4(color, 1.0);
}

