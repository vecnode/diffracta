using Avalonia;
using Avalonia.OpenGL.Controls;
using Avalonia.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Diffracta.Graphics;

public sealed class ShaderSurface : OpenGlControlBase {
    private GLLoader? _gl;
    private uint _program = 0;
    private uint _postProgram = 0;
    private uint _vao = 0;
    private uint _vbo = 0;
    private uint _framebuffer = 0;
    private uint _texture = 0;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private int _uTime = -1, _uRes = -1;
    private int _uSaturation = -1;

    private string? _currentFragPath;
    private string? _loadedFragPath;
    private Action<string>? _logCallback;
    private float _saturation = 1.0f;
    private int _lastWidth = 0;
    private int _lastHeight = 0;

    public void SetLogCallback(Action<string> callback) {
        _logCallback = callback;
    }
    
    public float Saturation {
        get => _saturation;
        set => _saturation = Math.Clamp(value, 0.0f, 2.0f);
    }


    // A simple fullscreen triangle VS using traditional vertex data
    private const string VertexSrc = """
        #version 300 es
        precision mediump float;
        layout(location = 0) in vec2 aPos;
        layout(location = 1) in vec2 aUV;
        out vec2 vUV;
        void main() {
            vUV = aUV;
            gl_Position = vec4(aPos, 0.0, 1.0);
        }
        """;

    // Fallback fragment if loading fails
    private const string FallbackFrag = """
        #version 300 es
        precision mediump float;
        out vec4 FragColor;
        uniform float u_time;
        uniform vec2 u_resolution;
        void main() {
            vec2 uv = gl_FragCoord.xy / u_resolution;
            float t = 0.5 + 0.5 * sin(u_time);
            FragColor = vec4(uv.x, uv.y, t, 1.0);
        }
        """;

    // Post-processing fragment shader
    private const string PostProcessFrag = """
        #version 300 es
        precision mediump float;
        out vec4 FragColor;
        in vec2 vUV;
        uniform sampler2D u_inputTexture;
        uniform float u_saturation;
        uniform vec2 u_resolution;
        void main() {
            vec2 uv = vUV;
            vec4 color = texture(u_inputTexture, uv);
            
            // Convert to grayscale
            float gray = dot(color.rgb, vec3(0.299, 0.587, 0.114));
            
            // Apply saturation
            color.rgb = mix(vec3(gray), color.rgb, u_saturation);
            
            FragColor = color;
        }
        """;

    protected override void OnOpenGlInit(GlInterface gl) {
        try
        {
            _gl = new GLLoader(gl);
            _gl.Initialize();

            // Create fullscreen triangle vertices
            float[] vertices = {
                // Position (x, y)    UV (u, v)
                -1.0f, -1.0f,        0.0f, 0.0f,  // Bottom-left
                 3.0f, -1.0f,        2.0f, 0.0f,  // Bottom-right (extended)
                -1.0f,  3.0f,        0.0f, 2.0f   // Top-left (extended)
            };

            _gl.glGenVertexArrays(1, out _vao);
            _gl.glBindVertexArray(_vao);

            _gl.glGenBuffers(1, out _vbo);
            _gl.glBindBuffer(GLLoader.GL_ARRAY_BUFFER, _vbo);
            _gl.glBufferData(GLLoader.GL_ARRAY_BUFFER, vertices.Length * sizeof(float), vertices, GLLoader.GL_STATIC_DRAW);

            // Position attribute (location = 0)
            _gl.glVertexAttribPointer(0, 2, GLLoader.GL_FLOAT, false, 4 * sizeof(float), 0);
            _gl.glEnableVertexAttribArray(0);

            // UV attribute (location = 1)
            _gl.glVertexAttribPointer(1, 2, GLLoader.GL_FLOAT, false, 4 * sizeof(float), 2 * sizeof(float));
            _gl.glEnableVertexAttribArray(1);

            // Don't build shaders here - wait until first render
            _program = 0; // Will be built on first render
            
            // Build post-processing program
            _postProgram = BuildProgram(VertexSrc, PostProcessFrag, out var postBuildLog);
            if (_postProgram == 0)
            {
                _logCallback?.Invoke($"Failed to build post-processing program: {postBuildLog}");
            }

            RequestNextFrameRendering();
        }
        catch (Exception ex)
        {
            // Log error but don't crash the app
            var errorMsg = $"OpenGL initialization failed: {ex.Message}";
            System.Diagnostics.Debug.WriteLine(errorMsg);
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            Console.WriteLine(errorMsg);
            _logCallback?.Invoke(errorMsg);
            // Set a flag to prevent rendering
            _program = 0;
        }
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb) {
        if (_gl is null) return;

        // Build shader program on first render if not already built
        if (_program == 0)
        {
            _program = BuildProgram(VertexSrc, FallbackFrag, out var buildLog);
            if (_program == 0)
            {
                _logCallback?.Invoke($"Failed to build initial shader program: {buildLog}");
                return;
            }
            CacheUniforms();
        }
        
        // Always check for pending shader to load (for switching between shaders)
        if (!string.IsNullOrEmpty(_currentFragPath) && File.Exists(_currentFragPath))
        {
            // Only load if it's different from what's currently loaded
            bool needsLoad = _loadedFragPath == null || 
                           !string.Equals(Path.GetFullPath(_loadedFragPath), Path.GetFullPath(_currentFragPath), StringComparison.OrdinalIgnoreCase);
            
            if (needsLoad)
            {
                var fragSrc = File.ReadAllText(_currentFragPath);
                var program = BuildProgram(VertexSrc, fragSrc, out string pendingBuildLog);
                
                if (program != 0)
                {
                    _program = program;
                    _loadedFragPath = _currentFragPath;
                    CacheUniforms();
                    _logCallback?.Invoke($"Successfully loaded shader: {Path.GetFileName(_currentFragPath)}");
                }
                else
                {
                    _logCallback?.Invoke($"Failed to load shader: {pendingBuildLog}");
                }
            }
        }

        try {
            var scale = VisualRoot?.RenderScaling ?? 1.0;
            int w = Math.Max(1, (int)(Bounds.Width * scale));
            int h = Math.Max(1, (int)(Bounds.Height * scale));

            // Check if size changed and recreate framebuffer if needed
            if (w != _lastWidth || h != _lastHeight) {
                _lastWidth = w;
                _lastHeight = h;
                
                // Recreate framebuffer with new size
                if (_framebuffer != 0) {
                    _gl.glDeleteFramebuffers(1, ref _framebuffer);
                    _gl.glDeleteTextures(1, ref _texture);
                    _framebuffer = 0;
                    _texture = 0;
                }
            }

            // Check if we need post-processing (saturation != 1.0)
            bool needsPostProcessing = Math.Abs(_saturation - 1.0f) > 0.001f;

            if (needsPostProcessing && _postProgram != 0)
            {
                // TWO-PASS RENDERING: Main shader -> Framebuffer -> Post-processing -> Screen
                
                // Create framebuffer and texture if needed
                if (_framebuffer == 0)
                {
                    CreateFramebuffer(w, h);
                }

                // PASS 1: Render main shader to framebuffer
                _gl.glBindFramebuffer(GLLoader.GL_FRAMEBUFFER, _framebuffer);
                _gl.glViewport(0, 0, w, h);
                _gl.glClearColor(0, 0, 0, 1);
                _gl.glClear(GLLoader.GL_COLOR_BUFFER_BIT);

                _gl.glUseProgram(_program);
                if (_uTime >= 0) _gl.glUniform1f(_uTime, (float)_clock.Elapsed.TotalSeconds);
                if (_uRes  >= 0) _gl.glUniform2f(_uRes, w, h);

                _gl.glBindVertexArray(_vao);
                _gl.glDrawArrays(GLLoader.GL_TRIANGLES, 0, 3);

                // PASS 2: Render post-processing to screen
                _gl.glBindFramebuffer(GLLoader.GL_FRAMEBUFFER, (uint)fb); // fb is the default framebuffer
                _gl.glViewport(0, 0, w, h);
                _gl.glClearColor(0, 0, 0, 1);
                _gl.glClear(GLLoader.GL_COLOR_BUFFER_BIT);

                _gl.glUseProgram(_postProgram);
                _gl.glActiveTexture(GLLoader.GL_TEXTURE0);
                _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _texture);
                _gl.glUniform1i(_gl.glGetUniformLocation(_postProgram, "u_inputTexture"), 0);
                if (_uSaturation >= 0) _gl.glUniform1f(_uSaturation, _saturation);
                if (_uRes >= 0) _gl.glUniform2f(_uRes, w, h);

                _gl.glBindVertexArray(_vao);
                _gl.glDrawArrays(GLLoader.GL_TRIANGLES, 0, 3);
            }
            else
            {
                // SINGLE-PASS RENDERING: Main shader directly to screen (original behavior)
                _gl.glBindFramebuffer(GLLoader.GL_FRAMEBUFFER, (uint)fb); // fb is the default framebuffer
                _gl.glViewport(0, 0, w, h);
                _gl.glClearColor(0, 0, 0, 1);
                _gl.glClear(GLLoader.GL_COLOR_BUFFER_BIT);

                _gl.glUseProgram(_program);
                if (_uTime >= 0) _gl.glUniform1f(_uTime, (float)_clock.Elapsed.TotalSeconds);
                if (_uRes  >= 0) _gl.glUniform2f(_uRes, w, h);

                _gl.glBindVertexArray(_vao);
                _gl.glDrawArrays(GLLoader.GL_TRIANGLES, 0, 3);
            }

            RequestNextFrameRendering(); // animate
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OpenGL render error: {ex.Message}");
            // Don't request next frame on error to prevent infinite error loop
        }
    }

    protected override void OnOpenGlDeinit(GlInterface gl) {
        if (_gl != null)
        {
            if (_vbo != 0) _gl.glDeleteBuffers(1, ref _vbo);
            if (_vao != 0) _gl.glDeleteVertexArrays(1, ref _vao);
            if (_program != 0) _gl.glDeleteProgram(_program);
            if (_postProgram != 0) _gl.glDeleteProgram(_postProgram);
            if (_texture != 0) _gl.glDeleteTextures(1, ref _texture);
            if (_framebuffer != 0) _gl.glDeleteFramebuffers(1, ref _framebuffer);
            
            // Reset size tracking
            _lastWidth = 0;
            _lastHeight = 0;
        }
    }

    public void LoadFragmentShaderFromFile(string path, out string message) {
        _currentFragPath = path;
        message = $"Running: {Path.GetFileName(path)}";
    }

    private void CacheUniforms() {
        if (_gl is null) return;
        _uTime = _gl.glGetUniformLocation(_program, "u_time");
        _uRes  = _gl.glGetUniformLocation(_program, "u_resolution");
        
        // Cache post-processing uniforms
        if (_postProgram != 0)
        {
            _uSaturation = _gl.glGetUniformLocation(_postProgram, "u_saturation");
        }
    }

    private void CreateFramebuffer(int width, int height)
    {
        if (_gl is null) return;

        // Create texture
        _gl.glGenTextures(1, out _texture);
        _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _texture);
        _gl.glTexImage2D(GLLoader.GL_TEXTURE_2D, 0, (int)GLLoader.GL_RGBA, width, height, 0, GLLoader.GL_RGBA, GLLoader.GL_UNSIGNED_BYTE, IntPtr.Zero);
        _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MIN_FILTER, (int)GLLoader.GL_LINEAR);
        _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MAG_FILTER, (int)GLLoader.GL_LINEAR);

        // Create framebuffer
        _gl.glGenFramebuffers(1, out _framebuffer);
        _gl.glBindFramebuffer(GLLoader.GL_FRAMEBUFFER, _framebuffer);
        _gl.glFramebufferTexture2D(GLLoader.GL_FRAMEBUFFER, GLLoader.GL_COLOR_ATTACHMENT0, GLLoader.GL_TEXTURE_2D, _texture, 0);

        // Check framebuffer status
        var status = _gl.glCheckFramebufferStatus(GLLoader.GL_FRAMEBUFFER);
        if (status != GLLoader.GL_FRAMEBUFFER_COMPLETE)
        {
            _logCallback?.Invoke($"Framebuffer creation failed: {status}");
        }
        else
        {
            _logCallback?.Invoke("Framebuffer created successfully");
        }
    }

    private uint BuildProgram(string vertex, string fragment, out string buildLog) {
        buildLog = string.Empty;
        if (_gl is null) 
        {
            buildLog = "GL context is null";
            return 0;
        }

        uint vs = Compile(GLLoader.GL_VERTEX_SHADER, vertex, out var vLog);
        uint fs = Compile(GLLoader.GL_FRAGMENT_SHADER, fragment, out var fLog);

        if (vs == 0 || fs == 0)
        {
            buildLog = $"Vertex Shader Error:\n{vLog}\n\nFragment Shader Error:\n{fLog}";
            return 0;
        }

        uint prog = _gl.glCreateProgram();
        _gl.glAttachShader(prog, vs);
        _gl.glAttachShader(prog, fs);
        _gl.glLinkProgram(prog);

        _gl.glGetProgramiv(prog, GLLoader.GL_LINK_STATUS, out int linked);
        if (linked == 0)
        {
            var sb = new StringBuilder(2048);
            _gl.glGetProgramInfoLog(prog, sb.Capacity, out int len, sb);
            buildLog = "Link: " + sb.ToString(0, Math.Max(0, len));
            return 0;
        }

        _gl.glDeleteShader(vs);
        _gl.glDeleteShader(fs);
        return prog;
    }

    private string ConvertToOpenGLES(string src) {
        // Ensure the shader has a #version; add one if missing
        if (!src.TrimStart().StartsWith("#version"))
            src = "#version 330 core\n" + src;

        // Convert desktop GLSL to OpenGL ES
        var lines = src.Split('\n');
        var converted = new List<string>();
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Convert version directive
            if (trimmed.StartsWith("#version"))
            {
                if (trimmed.Contains("core"))
                {
                    converted.Add("#version 300 es");
                    converted.Add("precision mediump float;");
                }
                else if (trimmed.Contains("330"))
                {
                    converted.Add("#version 300 es");
                    converted.Add("precision mediump float;");
                }
                else
                {
                    converted.Add(line);
                }
            }
            else
            {
                converted.Add(line);
            }
        }
        
        return string.Join("\n", converted);
    }

    private uint Compile(uint type, string src, out string log) {
        log = string.Empty;
        if (_gl is null) 
        {
            log = "GL context is null";
            return 0;
        }

        uint sh = _gl.glCreateShader(type);
        if (sh == 0)
        {
            log = "Failed to create shader object - glCreateShader returned 0";
            return 0;
        }

        // Convert desktop GLSL to OpenGL ES format
        src = ConvertToOpenGLES(src);

        var lengths = new[] { src.Length };
        var arr = new[] { src };
        _gl.glShaderSource(sh, 1, arr, lengths);
        _gl.glCompileShader(sh);

        _gl.glGetShaderiv(sh, GLLoader.GL_COMPILE_STATUS, out int ok);
        
        if (ok == 0)
        {
            var sb = new StringBuilder(2048);
            _gl.glGetShaderInfoLog(sh, sb.Capacity, out int len, sb);
            log = sb.ToString(0, Math.Max(0, len));
            
            Console.WriteLine($"Shader compile error log: '{log}'");
            
            // Add shader source to error for debugging
            log += $"\n\nShader Source:\n{src}";
            
            return 0;
        }
        return sh;
    }
}

