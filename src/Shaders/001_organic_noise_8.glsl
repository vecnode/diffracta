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
    
    // Wrapping waves with industrial motion
    float wave_x = sin(uv.x * 10.0 + u_time * 0.7) * 0.5 + 0.5;
    float wave_y = cos(uv.y * 10.0 - u_time * 0.5) * 0.5 + 0.5;
    float combined = (wave_x + wave_y) * 0.5;
    
    // Add sinuous distortion for flowing pattern
    vec2 distorted = uv + vec2(
        sin(uv.y * 12.0 + u_time) * 0.08,
        cos(uv.x * 12.0 + u_time) * 0.08
    );
    
    float n = noise(distorted * 6.0);
    float n2 = noise(distorted * 12.0 + u_time * 0.4);
    
    // Urban/industrial colors
    vec3 color1 = vec3(0.3, 0.3, 0.3);
    vec3 color2 = vec3(0.5, 0.5, 0.5);
    vec3 color3 = vec3(0.7, 0.7, 0.9);
    vec3 color4 = vec3(0.9, 0.7, 0.7);
    
    float t = (combined + n * 0.7 + n2 * 0.3);
    t = pow(t, 1.3); // Add contrast
    
    vec3 color = mix(
        mix(color1, color2, t * 2.0),
        mix(color3, color4, t * 2.0),
        t
    );
    
    // Add subtle pulsing effect
    float pulse = sin(u_time * 1.2) * 0.05 + 0.95;
    color *= pulse;
    
    FragColor = vec4(color, 1.0);
}

