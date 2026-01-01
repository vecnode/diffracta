#version 330 core

out vec4 FragColor;
uniform float u_time;
uniform vec2 u_resolution;

// Complex number operations
vec2 complexMult(vec2 a, vec2 b) {
    return vec2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x);
}

vec2 complexAdd(vec2 a, vec2 b) {
    return a + b;
}

// Mandelbrot fractal calculation
float mandelbrot(vec2 c, int maxIter) {
    vec2 z = vec2(0.0);
    
    for(int i = 0; i < maxIter; i++) {
        if(dot(z, z) > 4.0) {
            return float(i) / float(maxIter);
        }
        z = complexAdd(complexMult(z, z), c);
    }
    
    return 1.0;
}

// Smooth coloring function
vec3 getColor(float t) {
    if(t >= 1.0) return vec3(0.0);
    
    // Create a smooth color palette
    vec3 color1 = vec3(0.0, 0.0, 0.1);  // Deep blue
    vec3 color2 = vec3(0.0, 0.3, 0.8);  // Blue
    vec3 color3 = vec3(0.0, 0.8, 0.3);  // Green
    vec3 color4 = vec3(0.8, 0.8, 0.0);  // Yellow
    vec3 color5 = vec3(0.8, 0.0, 0.0);  // Red
    vec3 color6 = vec3(0.8, 0.0, 0.8);  // Magenta
    
    float scaled = t * 5.0;
    float segment = floor(scaled);
    float local = scaled - segment;
    
    if(segment < 1.0) return mix(color1, color2, local);
    if(segment < 2.0) return mix(color2, color3, local);
    if(segment < 3.0) return mix(color3, color4, local);
    if(segment < 4.0) return mix(color4, color5, local);
    return mix(color5, color6, local);
}

void main() {
    vec2 uv = (gl_FragCoord.xy - 0.5 * u_resolution) / min(u_resolution.x, u_resolution.y);
    
    // Fixed center and zoom - keep Mandelbrot centered
    float zoom = 1.0 + 0.3 * sin(u_time * 0.1);
    vec2 center = vec2(-0.5, 0.0); // Fixed center, no movement
    
    // Apply zoom and center
    vec2 c = center + uv * 0.4 / zoom;
    
    // Calculate Mandelbrot
    float escape = mandelbrot(c, 100);
    
    // Get color
    vec3 color = getColor(escape);
    
    // Add some glow effect
    float glow = 1.0 - smoothstep(0.0, 0.1, escape);
    color += glow * vec3(0.5, 0.8, 1.0);
    
    // Add subtle animation to the colors
    color *= 0.8 + 0.2 * sin(u_time + length(uv) * 10.0);
    
    FragColor = vec4(color, 1.0);
}
