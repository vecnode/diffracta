#version 330 core

out vec4 FragColor;
uniform float u_time;
uniform vec2 u_resolution;

// Random function
float random(vec2 st) {
    return fract(sin(dot(st.xy, vec2(12.9898, 78.233))) * 43758.5453123);
}

// Noise function
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

// Fractal noise
float fbm(vec2 st) {
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 0.0;
    
    for(int i = 0; i < 6; i++) {
        value += amplitude * noise(st);
        st *= 2.0;
        amplitude *= 0.5;
    }
    
    return value;
}

// Cellular noise function
float cellular(vec2 st) {
    vec2 i = floor(st);
    vec2 f = fract(st);
    
    float minDist = 1.0;
    
    for(int y = -1; y <= 1; y++) {
        for(int x = -1; x <= 1; x++) {
            vec2 neighbor = vec2(float(x), float(y));
            vec2 point = random(i + neighbor) * vec2(1.0) + neighbor;
            vec2 diff = neighbor + point - f;
            float dist = length(diff);
            minDist = min(minDist, dist);
        }
    }
    
    return minDist;
}

// Organic growth function
float organicGrowth(vec2 st, float time) {
    // Base cellular pattern
    float cells = cellular(st * 8.0);
    
    // Add fractal noise for organic variation
    float organic = fbm(st * 3.0 + time * 0.1);
    
    // Combine with time-based growth
    float growth = sin(time + length(st) * 5.0) * 0.5 + 0.5;
    
    return cells * (1.0 + organic * 0.5) * growth;
}

// Color function for organic patterns
vec3 getOrganicColor(float value) {
    // Create organic color palette
    vec3 color1 = vec3(0.1, 0.3, 0.1);  // Dark green
    vec3 color2 = vec3(0.2, 0.6, 0.2);  // Forest green
    vec3 color3 = vec3(0.4, 0.8, 0.4);  // Light green
    vec3 color4 = vec3(0.8, 0.9, 0.6);  // Pale yellow-green
    vec3 color5 = vec3(0.6, 0.4, 0.2);  // Brown
    
    float scaled = value * 4.0;
    float segment = floor(scaled);
    float local = scaled - segment;
    
    if(segment < 1.0) return mix(color1, color2, local);
    if(segment < 2.0) return mix(color2, color3, local);
    if(segment < 3.0) return mix(color3, color4, local);
    return mix(color4, color5, local);
}

void main() {
    vec2 uv = gl_FragCoord.xy / u_resolution;
    vec2 st = uv * 2.0 - 1.0;
    st.x *= u_resolution.x / u_resolution.y;
    
    // Create organic cellular pattern
    float organic = organicGrowth(st, u_time);
    
    // Add some pulsing effect
    float pulse = 0.8 + 0.2 * sin(u_time * 1.5);
    organic *= pulse;
    
    // Add subtle rotation
    float angle = u_time * 0.1;
    mat2 rot = mat2(cos(angle), -sin(angle), sin(angle), cos(angle));
    st = rot * st;
    
    // Add secondary layer for complexity
    float secondary = fbm(st * 2.0 + u_time * 0.05) * 0.3;
    organic += secondary;
    
    // Get color
    vec3 color = getOrganicColor(organic);
    
    // Add some bioluminescent glow
    float glow = 1.0 - smoothstep(0.0, 0.2, organic);
    color += glow * vec3(0.0, 0.5, 0.3) * 0.5;
    
    // Enhance contrast and saturation
    color = pow(color, vec3(0.7));
    color *= 1.2;
    
    FragColor = vec4(color, 1.0);
}
