#version 330 core
out vec4 FragColor;
uniform float u_time;
uniform vec2  u_resolution;

void main() {
    vec2 uv = (gl_FragCoord.xy / u_resolution) * 2.0 - 1.0;
    uv.x *= u_resolution.x / u_resolution.y;
    
    // Diffraction grating parameters
    float grating_spacing = 0.1;
    float wavelength = 0.5 + 0.3 * sin(u_time * 0.5);
    
    // Calculate diffraction pattern
    float phase = sin(uv.x / grating_spacing * 6.28318);
    float intensity = phase * phase;
    
    // Add multiple orders
    float order1 = sin(uv.x / grating_spacing * 6.28318);
    float order2 = sin(uv.x / grating_spacing * 6.28318 * 2.0);
    float order3 = sin(uv.x / grating_spacing * 6.28318 * 3.0);
    
    // Combine orders with decreasing intensity
    float pattern = order1 * order1 + order2 * order2 * 0.5 + order3 * order3 * 0.25;
    
    // Add some vertical modulation
    pattern *= sin(uv.y * 10.0 + u_time) * 0.3 + 0.7;
    
    // Create spectral colors based on wavelength
    vec3 color;
    if(wavelength < 0.4) {
        color = vec3(0.8, 0.2, 0.8); // Purple
    } else if(wavelength < 0.5) {
        color = vec3(0.2, 0.2, 1.0); // Blue
    } else if(wavelength < 0.6) {
        color = vec3(0.2, 0.8, 1.0); // Cyan
    } else if(wavelength < 0.7) {
        color = vec3(0.2, 1.0, 0.2); // Green
    } else if(wavelength < 0.8) {
        color = vec3(1.0, 1.0, 0.2); // Yellow
    } else {
        color = vec3(1.0, 0.4, 0.2); // Red
    }
    
    // Apply pattern
    color *= pattern * 0.8 + 0.2;
    
    FragColor = vec4(color, 1.0);
}
