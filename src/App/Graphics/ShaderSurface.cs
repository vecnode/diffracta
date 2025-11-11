using Avalonia;
using Avalonia.OpenGL.Controls;
using Avalonia.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Diffracta.Graphics;

// ========================
// ShaderSurface - OpenGL Shader Rendering Control
// ========================
// This class manages the rendering of shaders in the application, including:
// - Main shader rendering (the primary visual output)
// - Processing node pipeline (VFX effects applied in sequence)
// - Framebuffer management for multi-pass rendering
// 
// Rendering Pipeline:
//   1. Main Shader -> Framebuffer (if processing needed) or directly to Screen
//   2. VFX Processing Chain (6 nodes: Saturation, Ping-Pong, Barrel, Node 4, Node 5, Blackout)
//   3. Final Render -> Screen
public sealed class ShaderSurface : OpenGlControlBase {
    // ========================
    // OpenGL Context and Programs
    // ========================
    private GLLoader? _gl; // OpenGL function loader/wrapper
    private uint _program = 0; // Main shader program (the primary visual shader)
    private uint _passthroughProgram = 0; // Simple passthrough shader for copying textures to screen
    
    // ========================
    // Processing Node Arrays (VFX Chain)
    // ========================
    // These arrays manage 6 VFX processing nodes that can be applied in sequence:
    // Slot 0: Saturation
    // Slot 1: Ping-Pong Delay
    // Slot 2: Barrel Distortion
    // Slot 3: Processing Node 4 (empty/reserved)
    // Slot 4: Processing Node 5 (empty/reserved)
    // Slot 5: Blackout
    private uint[] _processing_nodePrograms = new uint[6]; // Compiled shader programs for each VFX node
    private bool[] _processing_nodeActive = new bool[6];   // Which VFX nodes are currently active/enabled
    private float[] _processing_nodeValues = new float[6]; // Parameter values for each VFX node (0.0 to 1.0 range)
    
    // ========================
    // Vertex and Buffer Objects
    // ========================
    private uint _vao = 0; // Vertex Array Object (contains vertex data layout)
    private uint _vbo = 0; // Vertex Buffer Object (contains fullscreen triangle vertices)
    
    // ========================
    // Main Shader Framebuffer
    // ========================
    // Used for two-pass rendering: main shader renders to this framebuffer,
    // then processing nodes read from it and render to screen.
    private uint _framebuffer = 0; // Framebuffer for main shader output
    private uint _texture = 0; // Texture containing main shader output
    
    // ========================
    // Processing Node Framebuffers
    // ========================
    // Each VFX node has its own dedicated framebuffer and texture.
    // This allows each node to read from the previous node's output and write its own result.
    private uint[] _processing_nodeFramebuffers = new uint[6]; // Dedicated framebuffer per VFX node
    private uint[] _processing_nodeTextures = new uint[6];     // Output texture per VFX node
    
    // ========================
    // Ping-Pong Delay Feedback
    // ========================
    // The Ping-Pong Delay effect (slot 1) needs to read from the previous frame's output
    // to create a delay/echo effect. This buffer stores that previous frame.
    private uint _pingPongFeedbackTexture = 0; // Texture storing previous frame for ping-pong delay
    private uint _pingPongFeedbackFramebuffer = 0; // Framebuffer for ping-pong feedback
    
    // ========================
    // Timing and Uniforms
    // ========================
    private readonly Stopwatch _clock = Stopwatch.StartNew(); // Clock for u_time uniform (animation)
    private int _uTime = -1; // Uniform location for u_time (cached for performance)
    private int _uRes = -1;  // Uniform location for u_resolution (cached for performance)
    
    // ========================
    // Shader Loading State
    // ========================
    private string? _currentFragPath; // Path to shader file that should be loaded
    private string? _loadedFragPath;  // Path to shader file that is currently loaded
    private Action<string>? _logCallback; // Callback for logging messages
    
    // ========================
    // Framebuffer Size Tracking
    // ========================
    // Framebuffers must be recreated when the control size changes.
    private int _lastWidth = 0;  // Last known width (for detecting size changes)
    private int _lastHeight = 0; // Last known height (for detecting size changes)

    // ========================
    // Public API - Logging
    // ========================
    public void SetLogCallback(Action<string> callback) {
        _logCallback = callback;
    }
    
    // ========================
    // Public API - Processing Node Properties (Legacy/Convenience)
    // ========================
    // These properties provide convenient access to specific processing nodes.
    // They map to the generic GetSlotActive/SetSlotActive/GetSlotValue/SetSlotValue methods.
    
    public float Saturation {
        get => _processing_nodeValues[0]; // Slot 0: Saturation value (0 = full color, 1 = grayscale)
        set => _processing_nodeValues[0] = Math.Clamp(value, 0.0f, 1.0f);
    }

    public float PingPongDelay {
        get => _processing_nodeValues[1]; // Slot 1: Ping-pong delay amount
        set => _processing_nodeValues[1] = Math.Clamp(value, 0.0f, 1.0f);
    }

    public bool SaturationActive {
        get => _processing_nodeActive[0]; // Slot 0: Whether saturation is active
        set => _processing_nodeActive[0] = value;
    }

    public bool PingPongActive {
        get => _processing_nodeActive[1]; // Slot 1: Whether ping-pong delay is active
        set => _processing_nodeActive[1] = value;
    }

    // ========================
    // Public API - Generic Processing Node Access
    // ========================
    // These methods provide generic access to all 6 VFX processing nodes by slot index.
    
    /// <summary>
    /// Gets whether a processing node is active (enabled).
    /// </summary>
    /// <param name="slot">Slot index (0-5)</param>
    /// <returns>True if the node is active, false otherwise</returns>
    public bool GetSlotActive(int slot) {
        return slot >= 0 && slot < 6 ? _processing_nodeActive[slot] : false;
    }

    /// <summary>
    /// Sets whether a processing node is active (enabled).
    /// </summary>
    /// <param name="slot">Slot index (0-5)</param>
    /// <param name="active">True to enable, false to disable</param>
    public void SetSlotActive(int slot, bool active) {
        if (slot >= 0 && slot < 6) {
            _processing_nodeActive[slot] = active;
        }
    }

    /// <summary>
    /// Gets the parameter value for a processing node.
    /// </summary>
    /// <param name="slot">Slot index (0-5)</param>
    /// <returns>The node's parameter value (0.0 to 1.0)</returns>
    public float GetSlotValue(int slot) {
        return slot >= 0 && slot < 6 ? _processing_nodeValues[slot] : 0.0f;
    }

    /// <summary>
    /// Sets the parameter value for a processing node.
    /// </summary>
    /// <param name="slot">Slot index (0-5)</param>
    /// <param name="value">The value to set (will be clamped to 0.0-1.0)</param>
    public void SetSlotValue(int slot, float value) {
        if (slot >= 0 && slot < 6) {
            _processing_nodeValues[slot] = Math.Clamp(value, 0.0f, 1.0f);
        }
    }

    // ========================
    // Public API - Shader State Queries
    // ========================
    /// <summary>
    /// Checks if the main shader is loaded and ready to render.
    /// </summary>
    public bool IsMainShaderLoaded => !string.IsNullOrEmpty(_loadedFragPath) && _program != 0;
    
    /// <summary>
    /// Gets the name/description of a processing node shader.
    /// </summary>
    /// <param name="slot">Slot index (0-5)</param>
    /// <returns>The shader name, or empty string if not loaded</returns>
    public string GetProcessingNodeShaderName(int slot) {
        if (slot < 0 || slot >= 6) return "";
        
        // Check if shader program is loaded
        if (_processing_nodePrograms[slot] == 0) return "";
        
        // Return shader names based on slot
        return slot switch {
            0 => "Saturation",
            1 => "Ping-Pong Delay",
            2 => "Barrel Distortion",
            3 => "(Empty) Processing Node 4",
            4 => "(Empty) Processing Node 5",
            5 => "Blackout",
            _ => ""
        };
    }
    
    /// <summary>
    /// Checks if a processing node shader is loaded and ready to use.
    /// </summary>
    /// <param name="slot">Slot index (0-5)</param>
    /// <returns>True if the shader is loaded, false otherwise</returns>
    public bool IsProcessingNodeShaderLoaded(int slot) {
        if (slot < 0 || slot >= 6) return false;
        // All shaders are loaded from files now, check if program exists
        return _processing_nodePrograms[slot] != 0;
    }

    // ========================
    // Embedded Shader Sources
    // ========================
    // These are hardcoded shader sources used by the rendering system.
    
    /// <summary>
    /// Vertex shader for fullscreen triangle rendering.
    /// Uses a single triangle that covers the entire screen (extended beyond viewport).
    /// This is more efficient than using a quad with two triangles.
    /// </summary>
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

    /// <summary>
    /// Fallback fragment shader used when shader loading fails or no shader is loaded.
    /// Displays a simple animated gradient for debugging/visual feedback.
    /// </summary>
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

    // ========================
    // DEAD CODE - Unused Shader
    // ========================
    // NOTE: PostProcessFrag is defined but never used in the codebase.
    // It appears to be an old/legacy shader that was replaced by file-based processing node shaders.
    // Keeping it for reference, but it's not compiled or used anywhere.
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
    
    /// <summary>
    /// Passthrough fragment shader for copying textures to the screen.
    /// Used for final render step and ping-pong feedback buffer updates.
    /// Uses vUV from vertex shader to properly sample the texture.
    /// </summary>
    private const string PassthroughFrag = """
        #version 300 es
        precision mediump float;
        out vec4 FragColor;
        in vec2 vUV;
        uniform sampler2D u_texture;
        uniform vec2 u_resolution;
        void main() {
            // Use vUV from vertex shader for proper texture sampling
            // The vertex shader sets vUV based on the fullscreen triangle coordinates
            FragColor = texture(u_texture, vUV);
        }
        """;
    

    // ========================
    // OpenGL Lifecycle - Initialization
    // ========================
    /// <summary>
    /// Called when OpenGL context is initialized.
    /// Sets up vertex buffers, loads processing node shaders, and prepares for rendering.
    /// </summary>
    protected override void OnOpenGlInit(GlInterface gl) {
        try
        {
            _gl = new GLLoader(gl);
            _gl.Initialize();
            
            // Initialize processing node buffers to zero
            for (int i = 0; i < 6; i++)
            {
                _processing_nodeFramebuffers[i] = 0;
                _processing_nodeTextures[i] = 0;
            }

            // ========================
            // Create Fullscreen Triangle Vertex Data
            // ========================
            // We use a single triangle that extends beyond the viewport to cover the entire screen.
            // This is more efficient than a quad (which requires 2 triangles).
            // The triangle vertices are:
            //   Bottom-left: (-1, -1) with UV (0, 0)
            //   Bottom-right: (3, -1) with UV (2, 0) - extended to cover right edge
            //   Top-left: (-1, 3) with UV (0, 2) - extended to cover top edge
            float[] vertices = {
                // Position (x, y)    UV (u, v)
                -1.0f, -1.0f,        0.0f, 0.0f,  // Bottom-left
                 3.0f, -1.0f,        2.0f, 0.0f,  // Bottom-right (extended)
                -1.0f,  3.0f,        0.0f, 2.0f   // Top-left (extended)
            };

            // Create Vertex Array Object (VAO) - stores vertex attribute layout
            _gl.glGenVertexArrays(1, out _vao);
            _gl.glBindVertexArray(_vao);

            // Create Vertex Buffer Object (VBO) - stores actual vertex data
            _gl.glGenBuffers(1, out _vbo);
            _gl.glBindBuffer(GLLoader.GL_ARRAY_BUFFER, _vbo);
            _gl.glBufferData(GLLoader.GL_ARRAY_BUFFER, vertices.Length * sizeof(float), vertices, GLLoader.GL_STATIC_DRAW);

            // Set up position attribute (location = 0): 2 floats, stride 16 bytes (4 floats), offset 0
            _gl.glVertexAttribPointer(0, 2, GLLoader.GL_FLOAT, false, 4 * sizeof(float), 0);
            _gl.glEnableVertexAttribArray(0);

            // Set up UV attribute (location = 1): 2 floats, stride 16 bytes, offset 8 bytes (2 floats)
            _gl.glVertexAttribPointer(1, 2, GLLoader.GL_FLOAT, false, 4 * sizeof(float), 2 * sizeof(float));
            _gl.glEnableVertexAttribArray(1);

            // Don't build main shader here - wait until first render
            // This allows the control to initialize even if no shader is loaded yet
            _program = 0; // Will be built on first render
            
            // Load all processing node shaders from files
            LoadProcessingNodeShaders();
            
            // Build passthrough program for final render and texture copying
            _passthroughProgram = BuildProgram(VertexSrc, PassthroughFrag, out var passthroughLog);
            if (_passthroughProgram == 0)
            {
                _logCallback?.Invoke($"Failed to build passthrough program: {passthroughLog}");
            }
            else
            {
                _logCallback?.Invoke("Passthrough program built successfully");
            }
            
            _logCallback?.Invoke($"Processing node shaders loaded. Programs: [{string.Join(", ", _processing_nodePrograms)}]");

            // Request first frame render
            RequestNextFrameRendering();
        }
        catch (Exception ex)
        {
            // Log error but don't crash the app
            // This allows the UI to continue functioning even if OpenGL initialization fails
            var errorMsg = $"OpenGL initialization failed: {ex.Message}";
            System.Diagnostics.Debug.WriteLine(errorMsg);
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            Console.WriteLine(errorMsg);
            _logCallback?.Invoke(errorMsg);
            // Set a flag to prevent rendering
            _program = 0;
        }
    }

    // ========================
    // OpenGL Lifecycle - Rendering
    // ========================
    /// <summary>
    /// Called every frame to render the shader output.
    /// Handles both single-pass (direct to screen) and two-pass (with processing nodes) rendering.
    /// </summary>
    /// <param name="gl">OpenGL interface</param>
    /// <param name="fb">Framebuffer ID (0 = default/screen framebuffer)</param>
    protected override void OnOpenGlRender(GlInterface gl, int fb) {
        if (_gl is null) return;

        // ========================
        // Build Main Shader Program (Lazy Initialization)
        // ========================
        // Build shader program on first render if not already built.
        // Uses fallback shader if no shader file is loaded.
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
        
        // ========================
        // Check for Pending Shader File to Load
        // ========================
        // Allows switching between shader files at runtime.
        // Only reloads if the path has changed to avoid unnecessary recompilation.
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
            // ========================
            // Calculate Viewport Size
            // ========================
            // Account for display scaling (high DPI displays)
            var scale = VisualRoot?.RenderScaling ?? 1.0;
            int w = Math.Max(1, (int)(Bounds.Width * scale));
            int h = Math.Max(1, (int)(Bounds.Height * scale));
            
            // Debug: Log viewport size if it's invalid
            if (w <= 0 || h <= 0)
            {
                _logCallback?.Invoke($"WARNING: Invalid viewport size: {w}x{h} (Bounds: {Bounds.Width}x{Bounds.Height}, Scale: {scale})");
                return; // Can't render with invalid size
            }

            // ========================
            // Handle Framebuffer Resize
            // ========================
            // Framebuffers must be recreated when size changes.
            // Old framebuffers are deleted to prevent memory leaks.
            if (w != _lastWidth || h != _lastHeight) {
                _lastWidth = w;
                _lastHeight = h;
                
                // Delete main framebuffer and texture
                if (_framebuffer != 0) {
                    _gl.glDeleteFramebuffers(1, ref _framebuffer);
                    _gl.glDeleteTextures(1, ref _texture);
                    _framebuffer = 0;
                    _texture = 0;
                }
                
                // Delete processing node buffers
                for (int i = 0; i < 6; i++) {
                    if (_processing_nodeFramebuffers[i] != 0) {
                        _gl.glDeleteFramebuffers(1, ref _processing_nodeFramebuffers[i]);
                    }
                    if (_processing_nodeTextures[i] != 0) {
                        _gl.glDeleteTextures(1, ref _processing_nodeTextures[i]);
                    }
                }
                
                // Delete ping-pong feedback buffer
                if (_pingPongFeedbackFramebuffer != 0) {
                    _gl.glDeleteFramebuffers(1, ref _pingPongFeedbackFramebuffer);
                    _gl.glDeleteTextures(1, ref _pingPongFeedbackTexture);
                    _pingPongFeedbackFramebuffer = 0;
                    _pingPongFeedbackTexture = 0;
                }
            }

            // ========================
            // Determine Rendering Path
            // ========================
            // Check if we need two-pass rendering (processing nodes active) or single-pass (direct to screen).
            // When no VFX nodes are active, use single-pass (direct to screen) for best performance.
            bool needsProcessingNodes = false;
            for (int i = 0; i < 6; i++)
            {
                if (_processing_nodeActive[i] && _processing_nodePrograms[i] != 0)
                {
                    needsProcessingNodes = true;
                    break;
                }
            }
            
            // Debug: Log rendering path selection (only once per path change)
            // Note: This will log every frame, but helps diagnose rendering issues
            // TODO: Add frame counter to only log once per second or on path change

            if (needsProcessingNodes)
            {
                // ========================
                // TWO-PASS RENDERING PATH
                // ========================
                // Pipeline: Main Shader -> Framebuffer -> VFX Chain -> Screen
                
                // Create framebuffer and texture if needed (lazy initialization)
                if (_framebuffer == 0)
                {
                    CreateFramebuffer(w, h);
                }

                // ========================
                // PASS 1: Render Main Shader to Framebuffer
                // ========================
                // The main shader renders to an off-screen framebuffer so processing nodes can read from it.
                if (_program == 0)
                {
                    // Should not happen - program should be built by now
                    _logCallback?.Invoke("ERROR: Main shader program is 0 in two-pass rendering!");
                    return;
                }
                
                _gl.glBindFramebuffer(GLLoader.GL_FRAMEBUFFER, _framebuffer);
                _gl.glViewport(0, 0, w, h);
                _gl.glClearColor(0, 0, 0, 1);
                _gl.glClear(GLLoader.GL_COLOR_BUFFER_BIT);

                _gl.glUseProgram(_program);
                if (_uTime >= 0) _gl.glUniform1f(_uTime, (float)_clock.Elapsed.TotalSeconds);
                if (_uRes  >= 0) _gl.glUniform2f(_uRes, w, h);

                _gl.glBindVertexArray(_vao);
                _gl.glDrawArrays(GLLoader.GL_TRIANGLES, 0, 3);

                // ========================
                // PASS 2: Apply Processing Pipeline
                // ========================
                // Start with main shader output texture
                uint currentTexture = _texture;

                // ========================
                // Process VFX Nodes in Sequence
                // ========================
                // All 6 VFX nodes are processed in order: Saturation, Ping-Pong, Barrel, Node 4, Node 5, Blackout.
                // Each node reads from the previous node's output and writes to its own dedicated buffer.
                // We track the last active node to ensure we always show the final processed output (last texture).
                int lastActiveNodeIndex = -1;
                for (int i = 0; i < 6; i++)
                {
                    if (_processing_nodeActive[i] && _processing_nodePrograms[i] != 0)
                    {
                        // Use dedicated buffer for this slot
                        uint targetBuffer = _processing_nodeFramebuffers[i];
                        uint targetTexture = _processing_nodeTextures[i];
                        
                        // Render to this slot's dedicated buffer
                        _gl.glBindFramebuffer(GLLoader.GL_FRAMEBUFFER, targetBuffer);
                        _gl.glViewport(0, 0, w, h);
                        
                        // Clear buffer for all effects except ping-pong delay (which needs feedback)
                        if (i == 1) // Ping-Pong Delay
                        {
                            // Don't clear - ping-pong delay needs previous frame's data preserved
                            // This allows the delay effect to accumulate over time
                        }
                        else
                        {
                            _gl.glClearColor(0, 0, 0, 1);
                            _gl.glClear(GLLoader.GL_COLOR_BUFFER_BIT);
                        }
                        
                        _gl.glUseProgram(_processing_nodePrograms[i]);
                        
                        // Bind the input texture (from previous effect in chain)
                        _gl.glActiveTexture(GLLoader.GL_TEXTURE0);
                        _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, currentTexture);
                        _gl.glUniform1i(_gl.glGetUniformLocation(_processing_nodePrograms[i], "u_texture"), 0);
                        
                        // Ping-pong delay (slot 1) needs additional feedback texture
                        if (i == 1)
                        {
                            // Bind previous frame's output as feedback for delay/echo effect
                            _gl.glActiveTexture(GLLoader.GL_TEXTURE0 + 1);
                            _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _pingPongFeedbackTexture);
                            _gl.glUniform1i(_gl.glGetUniformLocation(_processing_nodePrograms[i], "u_feedback"), 1);
                            
                            // Set delay amount parameter
                            _gl.glUniform1f(_gl.glGetUniformLocation(_processing_nodePrograms[i], "u_delay_amount"), _processing_nodeValues[i]);
                        }
                        
                        // Set effect-specific uniforms
                        if (i == 0) // Saturation
                        {
                            _gl.glUniform1f(_gl.glGetUniformLocation(_processing_nodePrograms[i], "u_saturation"), _processing_nodeValues[i]);
                        }
                        else if (i == 2) // Barrel Distortion
                        {
                            _gl.glUniform1f(_gl.glGetUniformLocation(_processing_nodePrograms[i], "u_barrel_strength"), _processing_nodeValues[i]);
                        }
                        else if (i == 5) // Blackout
                        {
                            _gl.glUniform1f(_gl.glGetUniformLocation(_processing_nodePrograms[i], "u_blackout"), _processing_nodeValues[i]);
                        }
                        
                        // Set resolution uniform (all shaders need this)
                        _gl.glUniform2f(_gl.glGetUniformLocation(_processing_nodePrograms[i], "u_resolution"), w, h);
                        
                        // Draw the effect
                        _gl.glBindVertexArray(_vao);
                        _gl.glDrawArrays(GLLoader.GL_TRIANGLES, 0, 3);
                        
                        // Update current texture for next effect in the chain
                        currentTexture = targetTexture;
                        lastActiveNodeIndex = i; // Track the last active node
                    }
                }
                
                // Ensure currentTexture points to the last active node's output (last texture in pipeline)
                // This guarantees we always show the final processed output, not the first texture
                if (lastActiveNodeIndex >= 0 && _processing_nodeTextures[lastActiveNodeIndex] != 0)
                {
                    currentTexture = _processing_nodeTextures[lastActiveNodeIndex];
                }
                
                // ========================
                // Update Ping-Pong Feedback Buffer
                // ========================
                // Copy current ping-pong output to feedback buffer for next frame.
                // This allows the delay effect to accumulate over time.
                if (_processing_nodeActive[1] && _processing_nodePrograms[1] != 0)
                {
                    // Copy current ping-pong output to feedback buffer for next frame
                    _gl.glBindFramebuffer(GLLoader.GL_FRAMEBUFFER, _pingPongFeedbackFramebuffer);
                    _gl.glViewport(0, 0, w, h);
                    _gl.glClearColor(0, 0, 0, 1);
                    _gl.glClear(GLLoader.GL_COLOR_BUFFER_BIT);
                    
                    // Use passthrough shader to copy texture
                    _gl.glUseProgram(_passthroughProgram);
                    _gl.glActiveTexture(GLLoader.GL_TEXTURE0);
                    _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _processing_nodeTextures[1]);
                    _gl.glUniform1i(_gl.glGetUniformLocation(_passthroughProgram, "u_texture"), 0);
                    _gl.glUniform2f(_gl.glGetUniformLocation(_passthroughProgram, "u_resolution"), w, h);
                    _gl.glBindVertexArray(_vao);
                    _gl.glDrawArrays(GLLoader.GL_TRIANGLES, 0, 3);
                }
                
                // ========================
                // Final Render to Screen
                // ========================
                // Always render the final texture to screen (main shader or processed VFX output).
                // currentTexture will be:
                //   - _texture: main shader output (when no VFX active)
                //   - _processing_nodeTextures[i]: last active VFX node output
                
                // Debug: Verify texture is valid (only log errors)
                
                _gl.glBindFramebuffer(GLLoader.GL_FRAMEBUFFER, (uint)fb);
                _gl.glViewport(0, 0, w, h);
                _gl.glClearColor(0, 0, 0, 1);
                _gl.glClear(GLLoader.GL_COLOR_BUFFER_BIT);
                
                // Simple passthrough shader to render final result to screen
                if (_passthroughProgram == 0)
                {
                    _logCallback?.Invoke("ERROR: Passthrough program is 0! Cannot render to screen.");
                    return;
                }
                
                // Verify texture is valid before binding
                if (currentTexture == 0)
                {
                    _logCallback?.Invoke($"ERROR: currentTexture is 0! Cannot render to screen.");
                    return;
                }
                
                _gl.glUseProgram(_passthroughProgram);
                _gl.glActiveTexture(GLLoader.GL_TEXTURE0);
                _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, currentTexture);
                _gl.glUniform1i(_gl.glGetUniformLocation(_passthroughProgram, "u_texture"), 0);
                _gl.glUniform2f(_gl.glGetUniformLocation(_passthroughProgram, "u_resolution"), w, h);
                
                _gl.glBindVertexArray(_vao);
                _gl.glDrawArrays(GLLoader.GL_TRIANGLES, 0, 3);
            }
            else
            {
                // ========================
                // FALLBACK: SINGLE-PASS RENDERING PATH
                // ========================
                // This should rarely be used now - only if framebuffer creation fails.
                // Most rendering goes through the two-pass path for processing chain support.
                if (_program == 0)
                {
                    // Should not happen - program should be built by now
                    _logCallback?.Invoke("ERROR: Main shader program is 0 in single-pass rendering!");
                    return;
                }
                
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

            // Request next frame render (continuous animation)
            RequestNextFrameRendering();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OpenGL render error: {ex.Message}");
            // Don't request next frame on error to prevent infinite error loop
        }
    }

    // ========================
    // OpenGL Lifecycle - Cleanup
    // ========================
    /// <summary>
    /// Called when OpenGL context is being destroyed.
    /// Cleans up all OpenGL resources to prevent memory leaks.
    /// </summary>
    protected override void OnOpenGlDeinit(GlInterface gl) {
        if (_gl != null)
        {
            // Delete vertex buffers
            if (_vbo != 0) _gl.glDeleteBuffers(1, ref _vbo);
            if (_vao != 0) _gl.glDeleteVertexArrays(1, ref _vao);
            
            // Delete shader programs
            if (_program != 0) _gl.glDeleteProgram(_program);
            if (_passthroughProgram != 0) _gl.glDeleteProgram(_passthroughProgram);
            
            // Delete processing node shader programs
            for (int i = 0; i < 6; i++)
            {
                if (_processing_nodePrograms[i] != 0) _gl.glDeleteProgram(_processing_nodePrograms[i]);
            }
            
            // Delete main framebuffer and texture
            if (_texture != 0) _gl.glDeleteTextures(1, ref _texture);
            if (_framebuffer != 0) _gl.glDeleteFramebuffers(1, ref _framebuffer);
            
            // Delete processing node textures and framebuffers
            for (int i = 0; i < 6; i++)
            {
                if (_processing_nodeTextures[i] != 0) _gl.glDeleteTextures(1, ref _processing_nodeTextures[i]);
                if (_processing_nodeFramebuffers[i] != 0) _gl.glDeleteFramebuffers(1, ref _processing_nodeFramebuffers[i]);
            }
            
            // Cleanup ping-pong feedback buffer
            if (_pingPongFeedbackTexture != 0) _gl.glDeleteTextures(1, ref _pingPongFeedbackTexture);
            if (_pingPongFeedbackFramebuffer != 0) _gl.glDeleteFramebuffers(1, ref _pingPongFeedbackFramebuffer);
            
            // Reset size tracking
            _lastWidth = 0;
            _lastHeight = 0;
        }
    }

    // ========================
    // Public API - Shader Loading
    // ========================
    /// <summary>
    /// Sets the path to a fragment shader file to load.
    /// The shader will be loaded on the next render frame.
    /// </summary>
    /// <param name="path">Path to the .glsl fragment shader file</param>
    /// <param name="message">Output message indicating success or failure</param>
    public void LoadFragmentShaderFromFile(string path, out string message) {
        _currentFragPath = path;
        message = "Shader path set successfully";
    }

    // ========================
    // Internal Helpers - Uniform Caching
    // ========================
    /// <summary>
    /// Caches uniform locations for performance.
    /// Uniform locations don't change, so we can look them up once and reuse them.
    /// </summary>
    private void CacheUniforms() {
        if (_gl is null) return;
        _uTime = _gl.glGetUniformLocation(_program, "u_time");
        _uRes  = _gl.glGetUniformLocation(_program, "u_resolution");
    }

    // ========================
    // Internal Helpers - Framebuffer Creation
    // ========================
    /// <summary>
    /// Creates the main shader framebuffer and texture.
    /// Also triggers creation of all processing node buffers.
    /// </summary>
    /// <param name="width">Framebuffer width in pixels</param>
    /// <param name="height">Framebuffer height in pixels</param>
    private void CreateFramebuffer(int width, int height)
    {
        if (_gl is null) return;

        // Create texture for main shader output
        _gl.glGenTextures(1, out _texture);
        _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _texture);
        _gl.glTexImage2D(GLLoader.GL_TEXTURE_2D, 0, (int)GLLoader.GL_RGBA, width, height, 0, GLLoader.GL_RGBA, GLLoader.GL_UNSIGNED_BYTE, IntPtr.Zero);
        _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MIN_FILTER, (int)GLLoader.GL_LINEAR);
        _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MAG_FILTER, (int)GLLoader.GL_LINEAR);

        // Create framebuffer and attach texture
        _gl.glGenFramebuffers(1, out _framebuffer);
        _gl.glBindFramebuffer(GLLoader.GL_FRAMEBUFFER, _framebuffer);
        _gl.glFramebufferTexture2D(GLLoader.GL_FRAMEBUFFER, GLLoader.GL_COLOR_ATTACHMENT0, GLLoader.GL_TEXTURE_2D, _texture, 0);

        // Check framebuffer status (must be complete before use)
        var status = _gl.glCheckFramebufferStatus(GLLoader.GL_FRAMEBUFFER);
        if (status != GLLoader.GL_FRAMEBUFFER_COMPLETE)
        {
            _logCallback?.Invoke($"Framebuffer creation failed: {status}");
        }
        else
        {
            _logCallback?.Invoke("Framebuffer created successfully");
        }

        // Create all processing node buffers (VFX chain + ping-pong feedback)
        CreateProcessingNodeBuffers(width, height);
    }

    // ========================
    // Internal Helpers - Processing Node Buffer Creation
    // ========================
    /// <summary>
    /// Creates framebuffers and textures for all processing nodes.
    /// Each VFX node gets its own dedicated buffer, plus ping-pong feedback.
    /// </summary>
    /// <param name="width">Buffer width in pixels</param>
    /// <param name="height">Buffer height in pixels</param>
    private void CreateProcessingNodeBuffers(int width, int height)
    {
        if (_gl is null) return;

        // ========================
        // Create VFX Processing Node Buffers
        // ========================
        // Each of the 6 VFX nodes gets its own dedicated framebuffer and texture.
        // This allows each node to read from the previous node's output and write its own result.
        for (int i = 0; i < 6; i++)
        {
            // Create texture for this node's output
            _gl.glGenTextures(1, out _processing_nodeTextures[i]);
            _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _processing_nodeTextures[i]);
            _gl.glTexImage2D(GLLoader.GL_TEXTURE_2D, 0, (int)GLLoader.GL_RGBA, width, height, 0, GLLoader.GL_RGBA, GLLoader.GL_UNSIGNED_BYTE, IntPtr.Zero);
            _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MIN_FILTER, (int)GLLoader.GL_LINEAR);
            _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MAG_FILTER, (int)GLLoader.GL_LINEAR);

            // Create framebuffer and attach texture
            _gl.glGenFramebuffers(1, out _processing_nodeFramebuffers[i]);
            _gl.glBindFramebuffer(GLLoader.GL_FRAMEBUFFER, _processing_nodeFramebuffers[i]);
            _gl.glFramebufferTexture2D(GLLoader.GL_FRAMEBUFFER, GLLoader.GL_COLOR_ATTACHMENT0, GLLoader.GL_TEXTURE_2D, _processing_nodeTextures[i], 0);
        }

        // ========================
        // Create Ping-Pong Feedback Buffer
        // ========================
        // This buffer stores the previous frame's output for the ping-pong delay effect.
        // It's updated each frame to provide feedback for the delay/echo effect.
        _gl.glGenTextures(1, out _pingPongFeedbackTexture);
        _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _pingPongFeedbackTexture);
        _gl.glTexImage2D(GLLoader.GL_TEXTURE_2D, 0, (int)GLLoader.GL_RGBA, width, height, 0, GLLoader.GL_RGBA, GLLoader.GL_UNSIGNED_BYTE, IntPtr.Zero);
        _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MIN_FILTER, (int)GLLoader.GL_LINEAR);
        _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MAG_FILTER, (int)GLLoader.GL_LINEAR);

        _gl.glGenFramebuffers(1, out _pingPongFeedbackFramebuffer);
        _gl.glBindFramebuffer(GLLoader.GL_FRAMEBUFFER, _pingPongFeedbackFramebuffer);
        _gl.glFramebufferTexture2D(GLLoader.GL_FRAMEBUFFER, GLLoader.GL_COLOR_ATTACHMENT0, GLLoader.GL_TEXTURE_2D, _pingPongFeedbackTexture, 0);

        _logCallback?.Invoke("Processing node buffers created successfully");
    }

    // ========================
    // Internal Helpers - Shader Loading
    // ========================
    /// <summary>
    /// Loads all processing node shaders from files.
    /// </summary>
    private void LoadProcessingNodeShaders()
    {
        if (_gl is null) return;

        try
        {
            // Debug: Log current directory info (helps diagnose file path issues)
            _logCallback?.Invoke($"Current directory: {Directory.GetCurrentDirectory()}");
            _logCallback?.Invoke($"Base directory: {AppDomain.CurrentDomain.BaseDirectory}");
            
            // ========================
            // Initialize VFX Processing Node Arrays
            // ========================
            // Initialize all 6 VFX slots to inactive with zero values
            for (int i = 0; i < 6; i++)
            {
                _processing_nodePrograms[i] = 0;
                _processing_nodeActive[i] = false;
                _processing_nodeValues[i] = 0.0f;
            }
            
            // ========================
            // Set Default Values for Known Shaders
            // ========================
            // All values are in 0.0 to 1.0 range.
            // Defaults are set to "off" or "bypass" state.
            _processing_nodeValues[0] = 0.0f; // Saturation default (0 = full color, 1 = grayscale)
            _processing_nodeValues[1] = 0.0f; // Ping-pong delay default
            _processing_nodeValues[5] = 0.0f; // Blackout default (bypass)
            // All slots start as inactive (OFF)
            
            // ========================
            // Load VFX Processing Node Shaders
            // ========================
            // Load shader files into slots 0-5:
            // Slot 0: Saturation
            // Slot 1: Ping-Pong Delay
            // Slot 2: Barrel Distortion
            // Slot 3: Empty (reserved for future use)
            // Slot 4: Empty (reserved for future use)
            // Slot 5: Blackout (VFX node, not master command)
            string[] shaderFiles = {
                "001_saturation.glsl",     // Slot 0: Saturation
                "002_ping_pong_delay.glsl", // Slot 1: Ping-Pong Delay
                "003_barrel.glsl",         // Slot 2: Barrel Distortion
                "",                        // Slot 3 - empty
                "",                        // Slot 4 - empty
                "005_blackout.glsl"        // Slot 5: Blackout (VFX node)
            };
            
            for (int i = 0; i < 6; i++)
            {
                if (!string.IsNullOrEmpty(shaderFiles[i]))
                {
                    // Try base directory first, then current directory
                    var shaderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Shaders", "postprocess", shaderFiles[i]);
                    if (!File.Exists(shaderPath))
                    {
                        shaderPath = Path.Combine(Directory.GetCurrentDirectory(), "Shaders", "postprocess", shaderFiles[i]);
                    }
                    
                    if (File.Exists(shaderPath))
                    {
                        var shaderSource = File.ReadAllText(shaderPath);
                        _processing_nodePrograms[i] = BuildProgram(VertexSrc, shaderSource, out var buildLog);
                        if (_processing_nodePrograms[i] == 0)
                        {
                            _logCallback?.Invoke($"Failed to build processing node {i+1} ({shaderFiles[i]}): {buildLog}");
                        }
                        else
                        {
                            _logCallback?.Invoke($"Processing node {i+1} ({shaderFiles[i]}) loaded successfully");
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
            _logCallback?.Invoke($"Error loading processing node shaders: {ex.Message}");
        }
    }
    
    // ========================
    // Internal Helpers - Shader Compilation
    // ========================
    /// <summary>
    /// Builds a complete shader program from vertex and fragment shader sources.
    /// Compiles both shaders, links them into a program, and returns the program ID.
    /// </summary>
    /// <param name="vertex">Vertex shader source code</param>
    /// <param name="fragment">Fragment shader source code</param>
    /// <param name="buildLog">Output log containing any compilation or linking errors</param>
    /// <returns>Program ID if successful, 0 if compilation or linking failed</returns>
    private uint BuildProgram(string vertex, string fragment, out string buildLog) {
        buildLog = string.Empty;
        if (_gl is null) 
        {
            buildLog = "GL context is null";
            return 0;
        }

        // Compile vertex and fragment shaders
        uint vs = Compile(GLLoader.GL_VERTEX_SHADER, vertex, out var vLog);
        uint fs = Compile(GLLoader.GL_FRAGMENT_SHADER, fragment, out var fLog);

        // If either shader failed to compile, return error
        if (vs == 0 || fs == 0)
        {
            buildLog = $"Vertex Shader Error:\n{vLog}\n\nFragment Shader Error:\n{fLog}";
            return 0;
        }

        // Create program and attach shaders
        uint prog = _gl.glCreateProgram();
        _gl.glAttachShader(prog, vs);
        _gl.glAttachShader(prog, fs);
        _gl.glLinkProgram(prog);

        // Check linking status
        _gl.glGetProgramiv(prog, GLLoader.GL_LINK_STATUS, out int linked);
        if (linked == 0)
        {
            // Get link error log
            var sb = new StringBuilder(2048);
            _gl.glGetProgramInfoLog(prog, sb.Capacity, out int len, sb);
            buildLog = "Link: " + sb.ToString(0, Math.Max(0, len));
            return 0;
        }

        // Clean up individual shaders (they're now part of the program)
        _gl.glDeleteShader(vs);
        _gl.glDeleteShader(fs);
        return prog;
    }

    // ========================
    // Internal Helpers - Shader Source Conversion
    // ========================
    /// <summary>
    /// Converts desktop OpenGL GLSL to OpenGL ES format.
    /// Handles version directives and adds precision qualifiers.
    /// </summary>
    /// <param name="src">Source shader code (desktop GLSL)</param>
    /// <returns>Converted shader code (OpenGL ES compatible)</returns>
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
                    // Desktop GLSL core profile -> OpenGL ES 3.0
                    converted.Add("#version 300 es");
                    converted.Add("precision mediump float;");
                }
                else if (trimmed.Contains("330"))
                {
                    // GLSL 330 -> OpenGL ES 3.0
                    converted.Add("#version 300 es");
                    converted.Add("precision mediump float;");
                }
                else
                {
                    // Keep other version directives as-is
                    converted.Add(line);
                }
            }
            else
            {
                // Keep non-version lines as-is
                converted.Add(line);
            }
        }
        
        return string.Join("\n", converted);
    }

    // ========================
    // Internal Helpers - Shader Compilation
    // ========================
    /// <summary>
    /// Compiles a single shader (vertex or fragment) from source code.
    /// Converts desktop GLSL to OpenGL ES format automatically.
    /// </summary>
    /// <param name="type">Shader type (GL_VERTEX_SHADER or GL_FRAGMENT_SHADER)</param>
    /// <param name="src">Shader source code</param>
    /// <param name="log">Output log containing compilation errors if any</param>
    /// <returns>Shader ID if successful, 0 if compilation failed</returns>
    private uint Compile(uint type, string src, out string log) {
        log = string.Empty;
        if (_gl is null) 
        {
            log = "GL context is null";
            return 0;
        }

        // Create shader object
        uint sh = _gl.glCreateShader(type);
        if (sh == 0)
        {
            log = "Failed to create shader object - glCreateShader returned 0";
            return 0;
        }

        // Convert desktop GLSL to OpenGL ES format
        src = ConvertToOpenGLES(src);

        // Set shader source and compile
        var lengths = new[] { src.Length };
        var arr = new[] { src };
        _gl.glShaderSource(sh, 1, arr, lengths);
        _gl.glCompileShader(sh);

        // Check compilation status
        _gl.glGetShaderiv(sh, GLLoader.GL_COMPILE_STATUS, out int ok);
        
        if (ok == 0)
        {
            // Get compilation error log
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

