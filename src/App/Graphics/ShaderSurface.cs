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
    private uint[] _postProcessPrograms = new uint[5]; // 5 slots for post-processing
    private bool[] _postProcessActive = new bool[5];   // Which slots are active
    private float[] _postProcessValues = new float[5]; // Values for each slot
    private uint _passthroughProgram = 0; // For final render
    private uint _vao = 0;
    private uint _vbo = 0;
    private uint _framebuffer = 0;
    private uint _texture = 0;
    private uint[] _postProcessFramebuffers = new uint[5]; // Dedicated buffer per post-process slot
    private uint[] _postProcessTextures = new uint[5];     // Output texture per slot
    private uint _pingPongFeedbackTexture = 0;              // For ping-pong delay feedback across frames
    private uint _pingPongFeedbackFramebuffer = 0;
    private bool _pingPongToggle = false;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private int _uTime = -1, _uRes = -1;

    private string? _currentFragPath;
    private string? _loadedFragPath;
    private Action<string>? _logCallback;
    private int _lastWidth = 0;
    private int _lastHeight = 0;

    public void SetLogCallback(Action<string> callback) {
        _logCallback = callback;
    }
    
    public float Saturation {
        get => _postProcessValues[0];
        set => _postProcessValues[0] = Math.Clamp(value, 0.0f, 1.0f);
    }

    public float PingPongDelay {
        get => _postProcessValues[1];
        set => _postProcessValues[1] = Math.Clamp(value, 0.0f, 1.0f);
    }

    public bool SaturationActive {
        get => _postProcessActive[0];
        set => _postProcessActive[0] = value;
    }

    public bool PingPongActive {
        get => _postProcessActive[1];
        set => _postProcessActive[1] = value;
    }

    // Generic methods for all 5 slots
    public bool GetSlotActive(int slot) {
        return slot >= 0 && slot < 5 ? _postProcessActive[slot] : false;
    }

    public void SetSlotActive(int slot, bool active) {
        if (slot >= 0 && slot < 5) {
            _postProcessActive[slot] = active;
        }
    }

    public float GetSlotValue(int slot) {
        return slot >= 0 && slot < 5 ? _postProcessValues[slot] : 0.0f;
    }

    public void SetSlotValue(int slot, float value) {
        if (slot >= 0 && slot < 5) {
            _postProcessValues[slot] = Math.Clamp(value, 0.0f, 1.0f);
        }
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
    
    // Simple passthrough fragment shader for final render
    private const string PassthroughFrag = """
        #version 300 es
        precision mediump float;
        out vec4 FragColor;
        uniform sampler2D u_texture;
        uniform vec2 u_resolution;
        void main() {
            vec2 uv = gl_FragCoord.xy / u_resolution;
            FragColor = texture(u_texture, uv);
        }
        """;

    protected override void OnOpenGlInit(GlInterface gl) {
        try
        {
            _gl = new GLLoader(gl);
            _gl.Initialize();
            
            // Initialize buffer arrays
            for (int i = 0; i < 5; i++)
            {
                _postProcessFramebuffers[i] = 0;
                _postProcessTextures[i] = 0;
            }

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
            
            // Load post-processing shaders
            LoadPostProcessShaders();
            
            // Build passthrough program for final render
            _passthroughProgram = BuildProgram(VertexSrc, PassthroughFrag, out var passthroughLog);
            if (_passthroughProgram == 0)
            {
                _logCallback?.Invoke($"Failed to build passthrough program: {passthroughLog}");
            }
            
            _logCallback?.Invoke($"Post-process shaders loaded. Programs: [{string.Join(", ", _postProcessPrograms)}]");

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

            // Check if we need post-processing (any shader is active)
            bool needsPostProcessing = false;
            for (int i = 0; i < 5; i++)
            {
                if (_postProcessActive[i] && _postProcessPrograms[i] != 0)
                {
                    needsPostProcessing = true;
                    break;
                }
            }

            if (needsPostProcessing)
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

                // PASS 2: Apply post-processing shaders in sequence
                _gl.glBindFramebuffer(GLLoader.GL_FRAMEBUFFER, (uint)fb); // fb is the default framebuffer
                _gl.glViewport(0, 0, w, h);
                _gl.glClearColor(0, 0, 0, 1);
                _gl.glClear(GLLoader.GL_COLOR_BUFFER_BIT);

                // Apply each active shader in sequence (post-processing pipeline)
                // Build chain: each effect reads from previous effect's output, writes to its own dedicated buffer
                uint currentTexture = _texture; // Start with main shader output
                
                for (int i = 0; i < 5; i++)
                {
                    if (_postProcessActive[i] && _postProcessPrograms[i] != 0)
                    {
                        // Use dedicated buffer for this slot
                        uint targetBuffer = _postProcessFramebuffers[i];
                        uint targetTexture = _postProcessTextures[i];
                        
                        // Render to this slot's dedicated buffer
                        _gl.glBindFramebuffer(GLLoader.GL_FRAMEBUFFER, targetBuffer);
                        _gl.glViewport(0, 0, w, h);
                        
                        // Clear buffer for all effects except ping-pong delay (which needs feedback)
                        if (i == 1)
                        {
                            // Don't clear - ping-pong delay needs previous frame's data preserved
                        }
                        else
                        {
                            _gl.glClearColor(0, 0, 0, 1);
                            _gl.glClear(GLLoader.GL_COLOR_BUFFER_BIT);
                        }
                        
                        _gl.glUseProgram(_postProcessPrograms[i]);
                        
                        // Bind the input texture (from previous effect in chain)
                        _gl.glActiveTexture(GLLoader.GL_TEXTURE0);
                        _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, currentTexture);
                        _gl.glUniform1i(_gl.glGetUniformLocation(_postProcessPrograms[i], "u_texture"), 0);
                        
                        // Ping-pong delay (slot 1) needs additional feedback texture
                        if (i == 1)
                        {
                            // Bind previous frame's output as feedback
                            _gl.glActiveTexture(GLLoader.GL_TEXTURE0 + 1);
                            _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _pingPongFeedbackTexture);
                            _gl.glUniform1i(_gl.glGetUniformLocation(_postProcessPrograms[i], "u_feedback"), 1);
                            
                            // Set delay amount parameter
                            _gl.glUniform1f(_gl.glGetUniformLocation(_postProcessPrograms[i], "u_delay_amount"), _postProcessValues[i]);
                        }
                        
                        // Set effect-specific uniforms
                        if (i == 0) // Saturation
                        {
                            _gl.glUniform1f(_gl.glGetUniformLocation(_postProcessPrograms[i], "u_saturation"), _postProcessValues[i]);
                        }
                        else if (i == 2) // Barrel Distortion
                        {
                            _gl.glUniform1f(_gl.glGetUniformLocation(_postProcessPrograms[i], "u_barrel_strength"), _postProcessValues[i]);
                        }
                        
                        // Set resolution uniform
                        _gl.glUniform2f(_gl.glGetUniformLocation(_postProcessPrograms[i], "u_resolution"), w, h);
                        
                        // Draw the effect
                        _gl.glBindVertexArray(_vao);
                        _gl.glDrawArrays(GLLoader.GL_TRIANGLES, 0, 3);
                        
                        // Update current texture for next effect in the chain
                        currentTexture = targetTexture;
                    }
                }
                
                // Update ping-pong feedback buffer for next frame (if ping-pong is active)
                if (_postProcessActive[1] && _postProcessPrograms[1] != 0)
                {
                    // Copy current ping-pong output to feedback buffer for next frame
                    _gl.glBindFramebuffer(GLLoader.GL_FRAMEBUFFER, _pingPongFeedbackFramebuffer);
                    _gl.glViewport(0, 0, w, h);
                    _gl.glClearColor(0, 0, 0, 1);
                    _gl.glClear(GLLoader.GL_COLOR_BUFFER_BIT);
                    
                    // Use passthrough shader to copy texture
                    _gl.glUseProgram(_passthroughProgram);
                    _gl.glActiveTexture(GLLoader.GL_TEXTURE0);
                    _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _postProcessTextures[1]);
                    _gl.glUniform1i(_gl.glGetUniformLocation(_passthroughProgram, "u_texture"), 0);
                    _gl.glUniform2f(_gl.glGetUniformLocation(_passthroughProgram, "u_resolution"), w, h);
                    _gl.glBindVertexArray(_vao);
                    _gl.glDrawArrays(GLLoader.GL_TRIANGLES, 0, 3);
                }
                
                // Final render to screen if we have any active shaders
                if (currentTexture != _texture) // Something was processed
                {
                    _gl.glBindFramebuffer(GLLoader.GL_FRAMEBUFFER, (uint)fb);
                    _gl.glViewport(0, 0, w, h);
                    _gl.glClearColor(0, 0, 0, 1);
                    _gl.glClear(GLLoader.GL_COLOR_BUFFER_BIT);
                    
                    // Simple passthrough shader to render final result to screen
                    _gl.glUseProgram(_passthroughProgram);
                    _gl.glActiveTexture(GLLoader.GL_TEXTURE0);
                    _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, currentTexture);
                    _gl.glUniform1i(_gl.glGetUniformLocation(_passthroughProgram, "u_texture"), 0);
                    _gl.glUniform2f(_gl.glGetUniformLocation(_passthroughProgram, "u_resolution"), w, h);
                    
                    _gl.glBindVertexArray(_vao);
                    _gl.glDrawArrays(GLLoader.GL_TRIANGLES, 0, 3);
                }
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
            if (_passthroughProgram != 0) _gl.glDeleteProgram(_passthroughProgram);
            for (int i = 0; i < 5; i++)
            {
                if (_postProcessPrograms[i] != 0) _gl.glDeleteProgram(_postProcessPrograms[i]);
            }
            if (_texture != 0) _gl.glDeleteTextures(1, ref _texture);
            if (_framebuffer != 0) _gl.glDeleteFramebuffers(1, ref _framebuffer);
            
            // Cleanup post-process buffers
            for (int i = 0; i < 5; i++)
            {
                if (_postProcessTextures[i] != 0) _gl.glDeleteTextures(1, ref _postProcessTextures[i]);
                if (_postProcessFramebuffers[i] != 0) _gl.glDeleteFramebuffers(1, ref _postProcessFramebuffers[i]);
            }
            
            // Cleanup ping-pong feedback buffer
            if (_pingPongFeedbackTexture != 0) _gl.glDeleteTextures(1, ref _pingPongFeedbackTexture);
            if (_pingPongFeedbackFramebuffer != 0) _gl.glDeleteFramebuffers(1, ref _pingPongFeedbackFramebuffer);
            
            // Reset size tracking
            _lastWidth = 0;
            _lastHeight = 0;
        }
    }

    public void LoadFragmentShaderFromFile(string path, out string message) {
        _currentFragPath = path;
        message = "Shader path set successfully";
    }

    private void CacheUniforms() {
        if (_gl is null) return;
        _uTime = _gl.glGetUniformLocation(_program, "u_time");
        _uRes  = _gl.glGetUniformLocation(_program, "u_resolution");
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

        // Create post-processing buffers for each slot and ping-pong feedback
        CreatePostProcessBuffers(width, height);
    }

    private void CreatePostProcessBuffers(int width, int height)
    {
        if (_gl is null) return;

        // Create dedicated buffers for each post-process slot
        for (int i = 0; i < 5; i++)
        {
            // Create texture
            _gl.glGenTextures(1, out _postProcessTextures[i]);
            _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _postProcessTextures[i]);
            _gl.glTexImage2D(GLLoader.GL_TEXTURE_2D, 0, (int)GLLoader.GL_RGBA, width, height, 0, GLLoader.GL_RGBA, GLLoader.GL_UNSIGNED_BYTE, IntPtr.Zero);
            _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MIN_FILTER, (int)GLLoader.GL_LINEAR);
            _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MAG_FILTER, (int)GLLoader.GL_LINEAR);

            // Create framebuffer
            _gl.glGenFramebuffers(1, out _postProcessFramebuffers[i]);
            _gl.glBindFramebuffer(GLLoader.GL_FRAMEBUFFER, _postProcessFramebuffers[i]);
            _gl.glFramebufferTexture2D(GLLoader.GL_FRAMEBUFFER, GLLoader.GL_COLOR_ATTACHMENT0, GLLoader.GL_TEXTURE_2D, _postProcessTextures[i], 0);
        }

        // Create dedicated ping-pong feedback buffer (for delay effect)
        _gl.glGenTextures(1, out _pingPongFeedbackTexture);
        _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _pingPongFeedbackTexture);
        _gl.glTexImage2D(GLLoader.GL_TEXTURE_2D, 0, (int)GLLoader.GL_RGBA, width, height, 0, GLLoader.GL_RGBA, GLLoader.GL_UNSIGNED_BYTE, IntPtr.Zero);
        _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MIN_FILTER, (int)GLLoader.GL_LINEAR);
        _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MAG_FILTER, (int)GLLoader.GL_LINEAR);

        _gl.glGenFramebuffers(1, out _pingPongFeedbackFramebuffer);
        _gl.glBindFramebuffer(GLLoader.GL_FRAMEBUFFER, _pingPongFeedbackFramebuffer);
        _gl.glFramebufferTexture2D(GLLoader.GL_FRAMEBUFFER, GLLoader.GL_COLOR_ATTACHMENT0, GLLoader.GL_TEXTURE_2D, _pingPongFeedbackTexture, 0);

        _logCallback?.Invoke("Post-process buffers created successfully");
    }

    private void LoadPostProcessShaders()
    {
        if (_gl is null) return;

        try
        {
            // Debug: Log current directory info
            _logCallback?.Invoke($"Current directory: {Directory.GetCurrentDirectory()}");
            _logCallback?.Invoke($"Base directory: {AppDomain.CurrentDomain.BaseDirectory}");
            
            // Initialize all slots
            for (int i = 0; i < 5; i++)
            {
                _postProcessPrograms[i] = 0;
                _postProcessActive[i] = false;
                _postProcessValues[i] = 0.0f;
            }
            
            // Set default values for known shaders (all 0.0 to 1.0 range)
            _postProcessValues[0] = 1.0f; // Saturation default (fully saturated)
            _postProcessValues[1] = 0.0f; // Ping-pong delay default
            // All slots start as inactive (OFF)
            
            // Load shader files into slots
            string[] shaderFiles = {
                "001_saturation.glsl",
                "002_ping_pong_delay.glsl",
                "003_barrel.glsl",
                "", // Slot 4 - empty
                ""  // Slot 5 - empty
            };
            
            for (int i = 0; i < 5; i++)
            {
                if (!string.IsNullOrEmpty(shaderFiles[i]))
                {
                    var shaderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Shaders", "postprocess", shaderFiles[i]);
                    if (!File.Exists(shaderPath))
                    {
                        shaderPath = Path.Combine(Directory.GetCurrentDirectory(), "Shaders", "postprocess", shaderFiles[i]);
                    }
                    
                    if (File.Exists(shaderPath))
                    {
                        var shaderSource = File.ReadAllText(shaderPath);
                        _postProcessPrograms[i] = BuildProgram(VertexSrc, shaderSource, out var buildLog);
                        if (_postProcessPrograms[i] == 0)
                        {
                            _logCallback?.Invoke($"Failed to build shader {i+1} ({shaderFiles[i]}): {buildLog}");
                        }
                        else
                        {
                            _logCallback?.Invoke($"Shader {i+1} ({shaderFiles[i]}) loaded successfully");
                        }
                    }
                    else
                    {
                        _logCallback?.Invoke($"Shader file not found for slot {i+1}: {shaderPath}");
                    }
                }
                else
                {
                    _logCallback?.Invoke($"Slot {i+1}: Empty (passthrough)");
                }
            }
        }
        catch (Exception ex)
        {
            _logCallback?.Invoke($"Error loading post-process shaders: {ex.Message}");
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

