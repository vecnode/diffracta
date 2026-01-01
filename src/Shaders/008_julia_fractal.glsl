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

// Julia set fractal calculation
float julia(vec2 z, vec2 c, int maxIter) {
    for(int i = 0; i < maxIter; i++) {
        if(dot(z, z) > 4.0) {
            return float(i) / float(maxIter);
        }
        z = complexAdd(complexMult(z, z), c);
    }
    
    return 1.0;
}

// Dynamic Julia parameter based on time
vec2 getJuliaC(float time) {
    float radius = 0.7885; // Classic Julia set radius
    float angle = time * 0.1;
    return vec2(radius * cos(angle), radius * sin(angle));
}

// Color palette for Julia sets
vec3 getJuliaColor(float t) {
    if(t >= 1.0) return vec3(0.0);
    
    // Create a vibrant color palette
    vec3 color1 = vec3(0.1, 0.0, 0.2);  // Deep purple
    vec3 color2 = vec3(0.8, 0.0, 0.4);  // Hot pink
    vec3 color3 = vec3(0.0, 0.8, 0.8);  // Cyan
    vec3 color4 = vec3(0.8, 0.8, 0.0);  // Yellow
    vec3 color5 = vec3(0.0, 0.4, 0.0);  // Green
    
    float scaled = t * 4.0;
    float segment = floor(scaled);
    float local = scaled - segment;
    
    if(segment < 1.0) return mix(color1, color2, local);
    if(segment < 2.0) return mix(color2, color3, local);
    if(segment < 3.0) return mix(color3, color4, local);
    return mix(color4, color5, local);
}

void main() {
    vec2 uv = (gl_FragCoord.xy - 0.5 * u_resolution) / min(u_resolution.x, u_resolution.y);
    
    // Scale and center the coordinate system
    vec2 z = uv * 2.0;
    
    // Get dynamic Julia parameter
    vec2 c = getJuliaC(u_time);
    
    // Calculate Julia set
    float escape = julia(z, c, 80);
    
    // Get color
    vec3 color = getJuliaColor(escape);
    
    // Add some dynamic effects
    float pulse = 0.5 + 0.5 * sin(u_time * 2.0);
    color *= pulse;
    
    // Add subtle rotation effect
    float rotation = u_time * 0.05;
    float dist = length(uv);
    color += 0.1 * sin(dist * 20.0 - rotation * 10.0) * vec3(1.0, 0.5, 0.0);
    
    // Enhance contrast
    color = pow(color, vec3(0.8));
    
    FragColor = vec4(color, 1.0);
}
