#version 330 core
out vec4 FragColor;
uniform float u_time;
uniform vec2  u_resolution;

void main() {
    vec2 uv = gl_FragCoord.xy / u_resolution;
    float r = 0.5 + 0.5*sin(u_time*1.2 + uv.x*6.2831);
    float g = 0.5 + 0.5*sin(u_time*0.9 + uv.y*6.2831);
    float b = 0.5 + 0.5*sin(u_time*1.7 + (uv.x+uv.y)*3.1416);
    FragColor = vec4(r, g, b, 1.0);
}
