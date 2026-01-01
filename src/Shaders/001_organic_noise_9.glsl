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
    
    // Multiple octaves of noise with rotation
    float n = 0.0;
    float amplitude = 1.0;
    float frequency = 1.2;
    
    // Add rotating effect
    float angle = u_time * 0.4;
    float cos_a = cos(angle);
    float sin_a = sin(angle);
    vec2 rot_uv = vec2(
        cos_a * (uv.x - 0.5) - sin_a * (uv.y - 0.5) + 0.5,
        sin_a * (uv.x - 0.5) + cos_a * (uv.y - 0.5) + 0.5
    );
    
    for(int i = 0; i < 4; i++) {
        n += noise(rot_uv * frequency + u_time * 0.12) * amplitude;
        amplitude *= 0.5;
        frequency *= 2.0;
    }
    
    // Create psychedelic patterns
    float pattern1 = sin(n * 7.0 + u_time * 1.5);
    float pattern2 = cos(n * 5.0 + u_time * 1.2);
    
    // Combine patterns
    float organic = (pattern1 + pattern2) * 0.5;
    
    // Add spiraling flow
    vec2 flow = vec2(sin(u_time * 0.5), cos(u_time * 0.3));
    organic += noise(rot_uv + flow * 0.12) * 0.35;
    
    // Mandala/psychedelic colors
    vec3 color1 = vec3(0.9, 0.2, 0.3);
    vec3 color2 = vec3(0.2, 0.8, 0.4);
    vec3 color3 = vec3(0.2, 0.3, 0.9);
    vec3 color4 = vec3(0.8, 0.9, 0.2);
    
    vec3 color;
    if(organic < -0.25) {
        color = color4;
    } else if(organic < 0.0) {
        color = mix(color4, color3, (organic + 0.25) / 0.25);
    } else if(organic < 0.25) {
        color = mix(color3, color2, organic / 0.25);
    } else {
        color = mix(color2, color1, (organic - 0.25) / 0.75);
    }
    
    FragColor = vec4(color, 1.0);
}

