#version 330 core
out vec4 FragColor;
uniform float u_time;
uniform vec2  u_resolution;

vec2 complex_multiply(vec2 a, vec2 b) {
    return vec2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x);
}

void main() {
    vec2 uv = (gl_FragCoord.xy / u_resolution) * 2.0 - 1.0;
    uv.x *= u_resolution.x / u_resolution.y;
    
    // Scale and offset for fractal viewing
    vec2 c = uv * 2.0 + vec2(sin(u_time * 0.1), cos(u_time * 0.1)) * 0.5;
    vec2 z = vec2(0.0);
    
    int iterations = 0;
    int max_iterations = 50;
    
    for(int i = 0; i < max_iterations; i++) {
        if(dot(z, z) > 4.0) break;
        z = complex_multiply(z, z) + c;
        iterations++;
    }
    
    // Color based on iteration count
    float t = float(iterations) / float(max_iterations);
    vec3 color1 = vec3(0.1, 0.1, 0.3);
    vec3 color2 = vec3(0.8, 0.2, 0.8);
    vec3 color3 = vec3(0.2, 0.8, 0.9);
    
    vec3 color;
    if(t < 0.5) {
        color = mix(color1, color2, t * 2.0);
    } else {
        color = mix(color2, color3, (t - 0.5) * 2.0);
    }
    
    FragColor = vec4(color, 1.0);
}
