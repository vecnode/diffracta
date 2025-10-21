#version 330 core
out vec4 FragColor;
uniform float u_time;
uniform vec2  u_resolution;

void main() {
    vec2 uv = (gl_FragCoord.xy / u_resolution) * 2.0 - 1.0;
    uv.x *= u_resolution.x / u_resolution.y;
    
    // Center point
    vec2 center = vec2(0.0, 0.0);
    vec2 pos = uv - center;
    
    // Distance from center
    float dist = length(pos);
    
    // Angle from center
    float angle = atan(pos.y, pos.x);
    
    // Spiral arms
    float spiral1 = sin(angle * 2.0 + dist * 8.0 - u_time * 2.0);
    float spiral2 = sin(angle * 2.0 + dist * 8.0 - u_time * 2.0 + 3.14159);
    
    // Combine spirals
    float spiral = (spiral1 + spiral2) * 0.5;
    
    // Add radial falloff
    float falloff = 1.0 / (1.0 + dist * 2.0);
    spiral *= falloff;
    
    // Add some noise for star field
    float stars = sin(uv.x * 50.0) * sin(uv.y * 50.0);
    stars = smoothstep(0.95, 1.0, stars);
    
    // Create galaxy colors
    vec3 core_color = vec3(1.0, 0.8, 0.4);
    vec3 arm_color = vec3(0.6, 0.4, 1.0);
    vec3 space_color = vec3(0.05, 0.05, 0.15);
    
    // Mix colors based on spiral intensity
    vec3 color = mix(space_color, arm_color, spiral * 0.5);
    color = mix(color, core_color, falloff * 0.3);
    color += stars * vec3(1.0, 1.0, 0.8) * 0.5;
    
    FragColor = vec4(color, 1.0);
}
