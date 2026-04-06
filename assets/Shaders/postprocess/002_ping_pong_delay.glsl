#version 330 core
out vec4 FragColor;
uniform float u_time;
uniform vec2 u_resolution;
uniform sampler2D u_texture;
uniform sampler2D u_feedback;
uniform float u_delay_amount;

void main() {
    vec2 uv = gl_FragCoord.xy / u_resolution;
    
    // Get current frame (fresh input)
    vec4 freshPixel = texture(u_texture, uv);
    
    // Get previous frame from feedback buffer
    vec4 stalePixel = texture(u_feedback, uv);
    
    // Simple mix-based ping-pong delay (like TouchDesigner reference)
    // This creates a smoother trailing effect
    FragColor = mix(freshPixel, stalePixel, u_delay_amount);
}
