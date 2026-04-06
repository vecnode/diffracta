#version 330 core
out vec4 FragColor;
uniform float u_time;
uniform vec2 u_resolution;

// Simple box fractal
float boxFractal(vec2 st, float time) {
    float value = 0.0;
    float scale = 1.0;
    
    for(int i = 0; i < 5; i++) {
        // Create boxes
        vec2 box = abs(fract(st * scale) - 0.5);
        float boxPattern = max(box.x, box.y);
        
        // Add to fractal
        value += (1.0 - smoothstep(0.0, 0.05, boxPattern)) / scale;
        
        // Scale up for next iteration
        scale *= 2.0;
        
        // Rotate slightly
        float angle = time * 0.1;
        mat2 rot = mat2(cos(angle), -sin(angle), sin(angle), cos(angle));
        st = rot * st;
    }
    
    return value;
}

void main() {
    vec2 uv = gl_FragCoord.xy / u_resolution;
    vec2 st = uv * 2.0 - 1.0;
    st.x *= u_resolution.x / u_resolution.y;
    
    // Scale for fractal
    st *= 3.0;
    
    // Create fractal
    float fractal = boxFractal(st, u_time);
    
    // Simple color mixing
    vec3 color1 = vec3(0.0, 0.0, 0.2);  // Dark blue
    vec3 color2 = vec3(0.0, 0.4, 0.8);  // Blue
    vec3 color3 = vec3(0.8, 0.0, 0.4);  // Pink
    vec3 color4 = vec3(0.8, 0.8, 0.0);  // Yellow
    
    vec3 color;
    if(fractal < 0.33) {
        color = mix(color1, color2, fractal * 3.0);
    } else if(fractal < 0.66) {
        color = mix(color2, color3, (fractal - 0.33) * 3.0);
    } else {
        color = mix(color3, color4, (fractal - 0.66) * 3.0);
    }
    
    // Add pulsing
    float pulse = 0.7 + 0.3 * sin(u_time * 1.5);
    color *= pulse;
    
    FragColor = vec4(color, 1.0);
}