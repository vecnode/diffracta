#version 330 core
out vec4 FragColor;
uniform float u_time;
uniform vec2  u_resolution;

void main() {
    vec2 uv = (gl_FragCoord.xy / u_resolution) * 2.0 - 1.0;
    uv.x *= u_resolution.x / u_resolution.y;
    
    // Two wave sources creating interference pattern
    vec2 source1 = vec2(-0.3, 0.0);
    vec2 source2 = vec2(0.3, 0.0);
    
    float dist1 = length(uv - source1);
    float dist2 = length(uv - source2);
    
    // Wave frequencies
    float freq1 = 15.0;
    float freq2 = 15.0;
    
    // Phase differences
    float phase1 = u_time * 2.0;
    float phase2 = u_time * 2.0 + 1.0;
    
    // Wave amplitudes
    float wave1 = sin(dist1 * freq1 - phase1);
    float wave2 = sin(dist2 * freq2 - phase2);
    
    // Interference pattern
    float interference = wave1 + wave2;
    
    // Normalize and create color
    float intensity = (interference + 2.0) / 4.0;
    vec3 color = vec3(intensity * 0.8, intensity * 0.4, intensity * 1.0);
    
    FragColor = vec4(color, 1.0);
}
