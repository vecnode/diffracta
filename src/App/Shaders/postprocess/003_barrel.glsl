#version 330 core
out vec4 FragColor;
uniform float u_time;
uniform vec2 u_resolution;
uniform sampler2D u_texture;
uniform float u_barrel_strength;

// Function to apply barrel distortion
vec2 barrelDistortion(vec2 coord, float amt) {
    vec2 centeredCoord = coord - 0.5;
    float distanceSquared = dot(centeredCoord, centeredCoord);
    return coord + centeredCoord * pow(distanceSquared, 1.0) * amt;
}

void main() {
    vec2 uv = gl_FragCoord.xy / u_resolution;
    
    // Apply barrel distortion
    vec2 distortedUv = barrelDistortion(uv, u_barrel_strength);
    
    // Sample the texture with distorted coordinates
    FragColor = texture(u_texture, distortedUv);
}

