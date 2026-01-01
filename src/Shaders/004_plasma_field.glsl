#version 330 core
out vec4 FragColor;
uniform float u_time;
uniform vec2  u_resolution;

float noise(vec2 p) {
    return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

void main() {
    vec2 uv = gl_FragCoord.xy / u_resolution;
    
    // Multiple noise layers for plasma effect
    float n1 = noise(uv * 3.0 + u_time * 0.5);
    float n2 = noise(uv * 6.0 - u_time * 0.3);
    float n3 = noise(uv * 12.0 + u_time * 0.7);
    
    // Combine noise layers
    float plasma = sin(n1 * 6.28) + sin(n2 * 6.28) + sin(n3 * 6.28);
    plasma = plasma / 3.0;
    
    // Add some movement
    plasma += sin(uv.x * 10.0 + u_time) * 0.1;
    plasma += sin(uv.y * 8.0 + u_time * 1.2) * 0.1;
    
    // Create color gradient
    vec3 color1 = vec3(0.1, 0.0, 0.3);
    vec3 color2 = vec3(0.8, 0.2, 0.8);
    vec3 color3 = vec3(0.2, 0.6, 1.0);
    vec3 color4 = vec3(1.0, 0.8, 0.2);
    
    vec3 color;
    if(plasma < -0.5) {
        color = mix(color1, color2, (plasma + 1.0) * 2.0);
    } else if(plasma < 0.0) {
        color = mix(color2, color3, (plasma + 0.5) * 2.0);
    } else if(plasma < 0.5) {
        color = mix(color3, color4, plasma * 2.0);
    } else {
        color = color4;
    }
    
    FragColor = vec4(color, 1.0);
}
