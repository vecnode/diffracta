#version 330 core
out vec4 FragColor;
uniform float u_time;
uniform vec2 u_resolution;
uniform sampler2D u_texture;
uniform float u_saturation;

void main() {
    vec2 uv = gl_FragCoord.xy / u_resolution;
    vec4 color = texture(u_texture, uv);
    
    // Convert to grayscale
    float gray = dot(color.rgb, vec3(0.299, 0.587, 0.114));
    
    // Mix between grayscale and original color based on saturation
    vec3 saturated = mix(vec3(gray), color.rgb, u_saturation);
    
    FragColor = vec4(saturated, color.a);
}
