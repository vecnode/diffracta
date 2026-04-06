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
    
    // Layered noise with slow movement
    float n1 = noise(uv * 0.5 + u_time * 0.02);
    float n2 = noise(uv * 2.0 + u_time * 0.05);
    float n3 = noise(uv * 8.0 + u_time * 0.1);
    
    float pattern = n1 * n2 + n3 * 0.3;
    
    // Add radial waves
    float dist = length(uv - 0.5);
    float waves = sin(dist * 12.0 - u_time * 2.0);
    
    // Organic fire-like colors
    vec3 color1 = vec3(0.1, 0.05, 0.0);
    vec3 color2 = vec3(0.8, 0.2, 0.05);
    vec3 color3 = vec3(1.0, 0.5, 0.0);
    vec3 color4 = vec3(1.0, 0.9, 0.3);
    
    float t = pattern + waves * 0.2;
    vec3 color;
    
    if(t < 0.25) {
        color = mix(color1, color2, t / 0.25);
    } else if(t < 0.5) {
        color = mix(color2, color3, (t - 0.25) / 0.25);
    } else if(t < 0.75) {
        color = mix(color3, color4, (t - 0.5) / 0.25);
    } else {
        color = mix(color4, vec3(1.0), (t - 0.75) / 0.25);
    }
    
    FragColor = vec4(color, 1.0);
}
