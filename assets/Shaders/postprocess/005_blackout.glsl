#version 330 core
out vec4 FragColor;
uniform float u_time;
uniform vec2 u_resolution;
uniform sampler2D u_texture;
uniform float u_blackout;

void main() {
    vec2 uv = gl_FragCoord.xy / u_resolution;
    vec4 color = texture(u_texture, uv);
    
    // Multiply RGB by (1 - u_blackout): 0 = no change, 1 = full black
    // Keep alpha at 1.0 to ensure opaque black, not transparent
    FragColor = vec4(color.rgb * (1.0 - u_blackout), 1.0);
}

