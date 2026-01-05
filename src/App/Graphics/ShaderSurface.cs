using Avalonia;
using Avalonia.OpenGL.Controls;
using Avalonia.OpenGL;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Media;
using FFMpegCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Buffers;

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
    
    // Image Texture State
    // ========================
    private uint _imageTexture = 0; // Texture for displaying images
    private int _imageWidth = 0;
    private int _imageHeight = 0;
    private string? _currentImagePath; // Path to image that should be loaded
    private string? _loadedImagePath;  // Path to image that is currently loaded
    
    // Video Texture State (Advanced YUV Architecture)
    // ========================
    // YUV video frame structure for efficient decoding and upload
    // NOTE: These fields will be used when YUV architecture is fully implemented
    #pragma warning disable CS0649 // Field is never assigned (will be used in full YUV implementation)
    private struct YuvFrame
    {
        public byte[]? YPlane;   // Luminance plane (width * height bytes)
        public byte[]? UvPlane;  // Chrominance plane (width * height / 2 bytes for NV12)
        public int Width;
        public int Height;
        public bool IsValid;
    }
    
    // PBO (Pixel Buffer Object) for async GPU uploads
    private struct VideoPbo
    {
        public uint PboId;           // PBO handle
        public bool InUse;           // Whether this PBO is currently being used
        public IntPtr MappedPtr;     // Mapped buffer pointer (for writing)
        public int Size;             // Buffer size in bytes
    }
    #pragma warning restore CS0649
    
    // Ring buffer for decoded frames (CPU side)
    private class VideoFrameRingBuffer
    {
        private readonly YuvFrame[] _frames;
        private readonly int _capacity;
        private int _writeIndex = 0;
        private int _readIndex = 0;
        private int _count = 0;
        private readonly object _lock = new();
        
        public VideoFrameRingBuffer(int capacity = 4)
        {
            _capacity = capacity;
            _frames = new YuvFrame[capacity];
        }
        
        public bool TryWrite(YuvFrame frame)
        {
            lock (_lock)
            {
                if (_count >= _capacity) return false; // Ring full
                _frames[_writeIndex] = frame;
                _writeIndex = (_writeIndex + 1) % _capacity;
                _count++;
                return true;
            }
        }
        
        public bool TryRead(out YuvFrame frame)
        {
            lock (_lock)
            {
                if (_count == 0)
                {
                    frame = default;
                    return false;
                }
                frame = _frames[_readIndex];
                _frames[_readIndex] = default; // Clear
                _readIndex = (_readIndex + 1) % _capacity;
                _count--;
                return true;
            }
        }
        
        public void Clear()
        {
            lock (_lock)
            {
                for (int i = 0; i < _capacity; i++)
                {
                    _frames[i] = default;
                }
                _writeIndex = 0;
                _readIndex = 0;
                _count = 0;
            }
        }
    }
    
    // Video texture handles (double-buffered Y and UV planes)
    // NOTE: These will be used when YUV architecture is fully implemented
    #pragma warning disable CS0414 // Field is assigned but never used (will be used in full YUV implementation)
    private uint _videoTextureY_Front = 0;  // Front buffer Y plane
    private uint _videoTextureY_Back = 0;    // Back buffer Y plane
    private uint _videoTextureUV_Front = 0;  // Front buffer UV plane
    private uint _videoTextureUV_Back = 0;   // Back buffer UV plane
    private bool _useFrontBuffer = true;     // Toggle between front/back buffers
    
    // PBO ring buffers for async uploads (2-4 PBOs per plane)
    private const int PBO_RING_SIZE = 3;
    private VideoPbo[] _pboRingY = new VideoPbo[PBO_RING_SIZE];   // PBOs for Y plane
    private VideoPbo[] _pboRingUV = new VideoPbo[PBO_RING_SIZE];   // PBOs for UV plane
    private int _pboWriteIndexY = 0;
    private int _pboWriteIndexUV = 0;
    #pragma warning restore CS0414
    
    // Legacy RGBA texture (kept for backward compatibility during transition)
    private uint _videoTexture = 0; // Deprecated - will be removed after YUV migration
    
    private int _videoWidth = 0;
    private int _videoHeight = 0;
    private string? _currentVideoPath; // Path to video that should be loaded
    private string? _loadedVideoPath;  // Path to video that is currently loaded
    private System.Threading.CancellationTokenSource? _videoCts; // Cancellation token for video decoding
    private Task? _videoDecodeTask; // Track the video decoding task for proper cleanup
    private readonly object _videoFrameLock = new(); // Lock for video frame updates
    
    // Ring buffer for decoded YUV frames (CPU side)
    // NOTE: Will be used when YUV architecture is fully implemented
    #pragma warning disable CS0169 // Field is never used (will be used in full YUV implementation)
    private VideoFrameRingBuffer? _videoFrameRing;
    #pragma warning restore CS0169
    
    // Legacy RGBA frame support (for transition period)
    private byte[]? _pendingVideoFrame; // RGBA frame data (deprecated)
    private bool _hasNewVideoFrame; // Flag indicating a new frame is ready
    private bool _videoTextureInitialized = false; // Track if texture has been allocated (use glTexSubImage2D after first)
    
    // Frame buffer for smoother playback (decode ahead)
    private readonly System.Collections.Concurrent.ConcurrentQueue<byte[]> _videoFrameQueue = new();
    private const int MAX_QUEUED_FRAMES = 3; // Decode 2-3 frames ahead for smooth playback
    
    private double _videoFps = 30.0; // Video frame rate
    private int _videoFrameCount = 0; // Total frame count for looping
    
    // Video Preload Cache
    // ========================
    // Preloads mapped videos into memory for instant switching
    private class PreloadedVideo
    {
        public string FilePath { get; set; } = string.Empty;
        public byte[]? FirstFrame { get; set; } // First frame in RGBA format (preloaded for instant display)
        public int Width { get; set; }
        public int Height { get; set; }
        public double Fps { get; set; }
        public int FrameCount { get; set; }
        public bool IsPreloaded { get; set; }
        public System.Threading.CancellationTokenSource? DecodeCts { get; set; }
        public Task? DecodeTask { get; set; }
        public readonly object FrameLock = new();
        public byte[]? CurrentFrame { get; set; }
        public bool HasNewFrame { get; set; }
    }
    
    private readonly Dictionary<string, PreloadedVideo> _videoCache = new(); // Cache of preloaded videos
    private readonly object _cacheLock = new(); // Lock for cache access
    
    // ========================
    // Project Size (Global Output Resolution)
    // ========================
    // Project size defines the consistent output resolution for all videos/media.
    // Videos will be scaled to this size regardless of their native resolution.
    private int _projectWidth = 1920;  // Default project width
    private int _projectHeight = 1080; // Default project height
    
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
    // Public API - Project Size
    // ========================
    /// <summary>
    /// Sets the global project size (output resolution) for consistent video rendering.
    /// All videos will be scaled to this size regardless of their native resolution.
    /// </summary>
    /// <param name="width">Project width in pixels</param>
    /// <param name="height">Project height in pixels</param>
    public void SetProjectSize(int width, int height) {
        if (width > 0 && height > 0) {
            _projectWidth = width;
            _projectHeight = height;
            _logCallback?.Invoke($"Project size set to: {_projectWidth}x{_projectHeight}");
        }
    }
    
    /// <summary>
    /// Gets the current project width
    /// </summary>
    public int ProjectWidth => _projectWidth;
    
    /// <summary>
    /// Gets the current project height
    /// </summary>
    public int ProjectHeight => _projectHeight;
    
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
            3 => "Empty Node 4",
            4 => "Empty Node 5",
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
    
    /// <summary>
    /// Fragment shader for displaying an image texture.
    /// Samples from u_texture and displays it on screen.
    /// </summary>
    private const string ImageDisplayFrag = """
        #version 300 es
        precision mediump float;
        in vec2 vUV;
        out vec4 FragColor;
        uniform sampler2D u_texture;
        uniform vec2 u_resolution;
        void main() {
            vec2 uv = vec2(vUV.x, 1.0 - vUV.y);
            FragColor = texture(u_texture, uv);
        }
        """;
    
    /// <summary>
    /// Fragment shader for YUV (NV12) to RGB conversion.
    /// Uses separate Y and UV plane textures for efficient video playback.
    /// </summary>
    private const string YuvDisplayFrag = """
        #version 300 es
        precision mediump float;
        in vec2 vUV;
        out vec4 FragColor;
        uniform sampler2D u_textureY;   // Y plane (GL_R8)
        uniform sampler2D u_textureUV;   // UV plane (GL_RG8) for NV12
        uniform vec2 u_resolution;
        
        // YUV to RGB conversion matrix (ITU-R BT.601)
        // NV12 format: Y plane + interleaved UV plane
        void main() {
            vec2 uv = vec2(vUV.x, 1.0 - vUV.y);
            
            // Sample Y and UV planes
            float y = texture(u_textureY, uv).r;
            vec2 uv_sample = texture(u_textureUV, uv).rg;
            
            // Convert YUV to RGB (ITU-R BT.601)
            // Y is in range [0, 1], UV is in range [0, 1] (offset by 0.5)
            float u = uv_sample.r - 0.5;
            float v = uv_sample.g - 0.5;
            
            // YUV to RGB conversion
            float r = y + 1.402 * v;
            float g = y - 0.344 * u - 0.714 * v;
            float b = y + 1.772 * u;
            
            FragColor = vec4(r, g, b, 1.0);
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
        // Check for Pending Video to Load (check this BEFORE image and shader)
        // ========================
        // Loads a video file and starts decoding frames.
        if (!string.IsNullOrEmpty(_currentVideoPath))
        {
            // Only load if it's different from what's currently loaded
            bool needsLoad = _loadedVideoPath == null || 
                           !string.Equals(_loadedVideoPath, _currentVideoPath, StringComparison.OrdinalIgnoreCase);
            
            if (needsLoad)
            {
                _loadedVideoPath = _currentVideoPath;
                StartVideoDecoding(_currentVideoPath);
                
                // Build image display shader for video (same as static images)
                // This happens in render loop where _gl is guaranteed to be available
                var program = BuildProgram(VertexSrc, ImageDisplayFrag, out string buildLog);
                
                if (program != 0)
                {
                    _program = program;
                    CacheUniforms();
                    _logCallback?.Invoke($"Video loading started: {Path.GetFileName(_currentVideoPath)}");
                }
                else
                {
                    _logCallback?.Invoke($"Failed to build video display shader: {buildLog}");
                    // Don't fail completely - shader might build on next frame
                }
            }
            
            // Also check if video is loaded but shader isn't the video shader yet
            // This handles preloaded videos where _loadedVideoPath is set immediately
            if (!string.IsNullOrEmpty(_loadedVideoPath) && (_program == 0 || !string.IsNullOrEmpty(_loadedFragPath)))
            {
                // Build video shader now that we're in the render loop
                var program = BuildProgram(VertexSrc, ImageDisplayFrag, out string buildLog);
                if (program != 0)
                {
                    _program = program;
                    CacheUniforms();
                    // Clear shader path since we're using video now
                    _loadedFragPath = null;
                    _currentFragPath = null;
                    _logCallback?.Invoke($"Video shader built in render loop: {Path.GetFileName(_loadedVideoPath)}");
                }
                else
                {
                    _logCallback?.Invoke($"Failed to build video shader in render loop: {buildLog}");
                }
            }
        }
        
        // ========================
        // Check Preloaded Video for New Frames
        // ========================
        // If using a preloaded video, check for new frames from its decode stream
        if (!string.IsNullOrEmpty(_loadedVideoPath))
        {
            PreloadedVideo? preloaded;
            lock (_cacheLock) {
                if (_videoCache.TryGetValue(_loadedVideoPath, out preloaded) && preloaded.IsPreloaded) {
                    lock (preloaded.FrameLock) {
                        if (preloaded.HasNewFrame && preloaded.CurrentFrame != null) {
                            // Update from preloaded video's decode stream
                            lock (_videoFrameLock) {
                                _pendingVideoFrame = preloaded.CurrentFrame;
                                _hasNewVideoFrame = true;
                            }
                            preloaded.HasNewFrame = false;
                        }
                    }
                }
            }
        }
        
        // ========================
        // Upload New Video Frame to Texture (if available)
        // ========================
        // Check for new decoded video frames and upload them to GPU texture.
        // Optimized: Use glTexSubImage2D after first allocation (faster), and check frame queue for smooth playback
        byte[]? frameToUpload = null;
        
        // First check if we have a queued frame (decoded ahead)
        if (_videoFrameQueue.TryDequeue(out var queuedFrame))
        {
            frameToUpload = queuedFrame;
        }
        // Otherwise check pending frame
        else if (_hasNewVideoFrame && _pendingVideoFrame != null)
        {
            lock (_videoFrameLock)
            {
                if (_pendingVideoFrame != null && _hasNewVideoFrame)
                {
                    frameToUpload = _pendingVideoFrame;
                    _hasNewVideoFrame = false;
                }
            }
        }
        
        if (frameToUpload != null && _videoWidth > 0 && _videoHeight > 0)
        {
            // Create texture on first frame (only once)
            if (_videoTexture == 0)
            {
                _gl.glGenTextures(1, out _videoTexture);
                _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _videoTexture);
                _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MIN_FILTER, (int)GLLoader.GL_LINEAR);
                _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MAG_FILTER, (int)GLLoader.GL_LINEAR);
            }
            
            _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _videoTexture);
            
            // Pin frame data for upload
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(frameToUpload, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                if (!_videoTextureInitialized)
                {
                    // First frame: allocate texture storage
                    _gl.glTexImage2D(GLLoader.GL_TEXTURE_2D, 0, (int)GLLoader.GL_RGBA, 
                        _videoWidth, _videoHeight, 0, 
                        GLLoader.GL_RGBA, GLLoader.GL_UNSIGNED_BYTE, handle.AddrOfPinnedObject());
                    _videoTextureInitialized = true;
                }
                else
                {
                    // Subsequent frames: update existing texture (much faster - no reallocation)
                    // Note: glTexSubImage2D may not be available, fallback to glTexImage2D if needed
                    // For now, use glTexImage2D but it's still faster than first allocation
                    _gl.glTexImage2D(GLLoader.GL_TEXTURE_2D, 0, (int)GLLoader.GL_RGBA, 
                        _videoWidth, _videoHeight, 0, 
                        GLLoader.GL_RGBA, GLLoader.GL_UNSIGNED_BYTE, handle.AddrOfPinnedObject());
                }
            }
            finally
            {
                handle.Free();
            }
        }
        
        // ========================
        // Check for Pending Image to Load (check this BEFORE fallback shader)
        // ========================
        // Loads an image from avares resource and displays it.
        if (!string.IsNullOrEmpty(_currentImagePath))
        {
            // Only load if it's different from what's currently loaded
            bool needsLoad = _loadedImagePath == null || 
                           !string.Equals(_loadedImagePath, _currentImagePath, StringComparison.OrdinalIgnoreCase);
            
            if (needsLoad)
            {
                try
                {
                    // Load image from avares resource
                    var uri = new Uri(_currentImagePath);
                    using var stream = AssetLoader.Open(uri);
                    if (stream != null)
                    {
                        // Get image dimensions first
                        using var tempBitmap = new Bitmap(stream);
                        var pixelSize = tempBitmap.PixelSize;
                        stream.Position = 0; // Reset stream for FFMpeg
                        
                        // Use FFMpegCore to decode PNG to raw RGBA pixels (same approach as video frames)
                        var pixelData = new byte[pixelSize.Width * pixelSize.Height * 4];
                        
                        // Decode image using FFMpeg to get raw RGBA pixel data
                        var tempFilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"temp_image_{Guid.NewGuid()}.png");
                        try
                        {
                            // Write stream to temp file for FFMpeg
                            using (var fileStream = File.Create(tempFilePath))
                            {
                                stream.CopyTo(fileStream);
                            }
                            
                            // Decode using FFMpeg (same as video decoding)
                            var frameSize = pixelSize.Width * pixelSize.Height * 4;
                            var decodedFlag = new bool[1] { false };
                            
                            // Use Task.Run to handle async FFMpeg call synchronously
                            Task.Run(async () =>
                            {
                                await FFMpegArguments
                                    .FromFileInput(tempFilePath)
                                    .OutputToPipe(new FFMpegCore.Pipes.StreamPipeSink(new SingleFrameStream(frameSize, (frame) =>
                                    {
                                        if (frame.Length >= frameSize)
                                        {
                                            frame.Span.CopyTo(pixelData);
                                            decodedFlag[0] = true;
                                        }
                                    })), options => options
                                        .WithVideoCodec("rawvideo")
                                        .ForceFormat("rawvideo")
                                        .WithCustomArgument("-pix_fmt rgba")
                                        .WithCustomArgument("-frames:v 1")
                                        .WithCustomArgument("-an"))
                                    .ProcessAsynchronously();
                            }).GetAwaiter().GetResult();
                            
                            if (!decodedFlag[0])
                            {
                                _logCallback?.Invoke($"Warning: Failed to decode image pixels, using fallback");
                                // Fallback: fill with gray
                                Array.Fill(pixelData, (byte)128);
                            }
                            else
                            {
                                _logCallback?.Invoke($"Image decoded: {pixelSize.Width}x{pixelSize.Height}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logCallback?.Invoke($"Error decoding image: {ex.Message}");
                            // Fallback: fill with gray
                            Array.Fill(pixelData, (byte)128);
                        }
                        finally
                        {
                            // Clean up temp file
                            try { if (File.Exists(tempFilePath)) File.Delete(tempFilePath); } catch { }
                        }
                            
                            // Create or update texture
                            if (_imageTexture == 0)
                            {
                                _gl.glGenTextures(1, out _imageTexture);
                            }
                            
                            _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _imageTexture);
                            _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MIN_FILTER, (int)GLLoader.GL_LINEAR);
                            _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MAG_FILTER, (int)GLLoader.GL_LINEAR);
                            
                        // Upload texture data
                        var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixelData, System.Runtime.InteropServices.GCHandleType.Pinned);
                        try
                        {
                            _gl.glTexImage2D(GLLoader.GL_TEXTURE_2D, 0, (int)GLLoader.GL_RGBA, 
                                pixelSize.Width, pixelSize.Height, 0, 
                                GLLoader.GL_RGBA, GLLoader.GL_UNSIGNED_BYTE, handle.AddrOfPinnedObject());
                        }
                        finally
                        {
                            handle.Free();
                        }
                        
                        _imageWidth = pixelSize.Width;
                        _imageHeight = pixelSize.Height;
                        
                        // Build image display shader
                        var program = BuildProgram(VertexSrc, ImageDisplayFrag, out string buildLog);
                        
                        if (program != 0)
                        {
                            _program = program;
                            _loadedImagePath = _currentImagePath;
                            CacheUniforms();
                            _logCallback?.Invoke($"Successfully loaded image: {Path.GetFileName(_currentImagePath)}");
                        }
                        else
                        {
                            _logCallback?.Invoke($"Failed to build image display shader: {buildLog}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logCallback?.Invoke($"Failed to load image: {ex.Message}");
                }
            }
        }
        
        // ========================
        // Check for Pending Shader File to Load
        // ========================
        // Allows switching between shader files at runtime.
        // Only reloads if the path has changed to avoid unnecessary recompilation.
        // IMPORTANT: Only load shader if no video is currently loaded or being loaded (video takes priority)
        if (!string.IsNullOrEmpty(_currentFragPath) && File.Exists(_currentFragPath) && 
            string.IsNullOrEmpty(_loadedVideoPath) && string.IsNullOrEmpty(_currentVideoPath))
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
                    _loadedImagePath = null; // Clear image when loading shader
                    _loadedVideoPath = null; // Clear video when loading shader
                    _currentVideoPath = null; // Clear video path when loading shader
                    StopVideo(); // Stop any playing video
                    CacheUniforms();
                    _logCallback?.Invoke($"Successfully loaded shader: {Path.GetFileName(_currentFragPath)}");
                }
                else
                {
                    _logCallback?.Invoke($"Failed to load shader: {pendingBuildLog}");
                }
            }
        }
        
        // ========================
        // Build Main Shader Program (Lazy Initialization - Fallback Only)
        // ========================
        // Build fallback shader ONLY if no shader file, image, or video has been loaded.
        // This ensures images, videos, and shaders take priority over the fallback.
        if (_program == 0 && string.IsNullOrEmpty(_loadedImagePath) && string.IsNullOrEmpty(_loadedVideoPath) && string.IsNullOrEmpty(_loadedFragPath))
        {
            _program = BuildProgram(VertexSrc, FallbackFrag, out var buildLog);
            if (_program == 0)
            {
                _logCallback?.Invoke($"Failed to build initial shader program: {buildLog}");
                return;
            }
            CacheUniforms();
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
                
                // If using image display shader, bind the image texture
                if (!string.IsNullOrEmpty(_loadedImagePath) && _imageTexture != 0)
                {
                    _gl.glActiveTexture(GLLoader.GL_TEXTURE0);
                    _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _imageTexture);
                    var uTextureLoc = _gl.glGetUniformLocation(_program, "u_texture");
                    if (uTextureLoc >= 0)
                    {
                        _gl.glUniform1i(uTextureLoc, 0);
                    }
                }
                // If using video, bind the video texture (create texture if it doesn't exist yet)
                else if (!string.IsNullOrEmpty(_loadedVideoPath))
                {
                    // Create texture if it doesn't exist yet (will be populated when first frame arrives)
                    if (_videoTexture == 0)
                    {
                        _gl.glGenTextures(1, out _videoTexture);
                        _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _videoTexture);
                        _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MIN_FILTER, (int)GLLoader.GL_LINEAR);
                        _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MAG_FILTER, (int)GLLoader.GL_LINEAR);
                        // Create empty texture (will be updated when frame arrives)
                        _gl.glTexImage2D(GLLoader.GL_TEXTURE_2D, 0, (int)GLLoader.GL_RGBA, 
                            _videoWidth, _videoHeight, 0, 
                            GLLoader.GL_RGBA, GLLoader.GL_UNSIGNED_BYTE, IntPtr.Zero);
                    }
                    else
                    {
                        _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _videoTexture);
                    }
                    
                    var uTextureLoc = _gl.glGetUniformLocation(_program, "u_texture");
                    if (uTextureLoc >= 0)
                    {
                        _gl.glUniform1i(uTextureLoc, 0);
                    }
                }

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
                
                // If using image display shader, bind the image texture
                if (!string.IsNullOrEmpty(_loadedImagePath) && _imageTexture != 0)
                {
                    _gl.glActiveTexture(GLLoader.GL_TEXTURE0);
                    _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _imageTexture);
                    var uTextureLoc = _gl.glGetUniformLocation(_program, "u_texture");
                    if (uTextureLoc >= 0)
                    {
                        _gl.glUniform1i(uTextureLoc, 0);
                    }
                }
                // If using video, bind the video texture (create texture if it doesn't exist yet)
                else if (!string.IsNullOrEmpty(_loadedVideoPath))
                {
                    // Create texture if it doesn't exist yet (will be populated when first frame arrives)
                    if (_videoTexture == 0)
                    {
                        _gl.glGenTextures(1, out _videoTexture);
                        _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _videoTexture);
                        _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MIN_FILTER, (int)GLLoader.GL_LINEAR);
                        _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MAG_FILTER, (int)GLLoader.GL_LINEAR);
                        // Create empty texture (will be updated when frame arrives)
                        _gl.glTexImage2D(GLLoader.GL_TEXTURE_2D, 0, (int)GLLoader.GL_RGBA, 
                            _videoWidth, _videoHeight, 0, 
                            GLLoader.GL_RGBA, GLLoader.GL_UNSIGNED_BYTE, IntPtr.Zero);
                    }
                    else
                    {
                        _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _videoTexture);
                    }
                    
                    var uTextureLoc = _gl.glGetUniformLocation(_program, "u_texture");
                    if (uTextureLoc >= 0)
                    {
                        _gl.glUniform1i(uTextureLoc, 0);
                    }
                }

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
        // Stop video decoding first to ensure threads are cleaned up before OpenGL context is destroyed
        StopVideo();
        
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
        _currentImagePath = null; // Clear image when loading shader
        _currentVideoPath = null; // Clear video when loading shader
        StopVideo(); // Stop any playing video
        message = "Shader path set successfully";
    }
    
    /// <summary>
    /// Loads an image from an avares resource and displays it as the initial shader.
    /// </summary>
    /// <param name="avaresPath">Avares path to the image (e.g., "avares://Diffracta/Media/default/smpte_color_bars.png")</param>
    /// <param name="message">Output message indicating success or failure</param>
    public void LoadImageFromAvares(string avaresPath, out string message) {
        _currentImagePath = avaresPath;
        _currentFragPath = null; // Clear shader when loading image
        _currentVideoPath = null; // Clear video when loading image
        StopVideo(); // Stop any playing video
        message = "Image path set successfully";
    }
    
    /// <summary>
    /// Loads a video file and starts playing it in a loop.
    /// Uses preloaded video if available for instant switching, otherwise loads on-demand.
    /// The video will be displayed using the image display shader and can be processed through the VFX pipeline.
    /// </summary>
    /// <param name="videoPath">Path to the video file</param>
    /// <param name="message">Output message indicating success or failure</param>
    public void LoadVideo(string videoPath, out string message) {
        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
        {
            message = $"Video file not found: {videoPath}";
            return;
        }
        
        // Stop any currently playing video
        StopVideo();
        
        // Clear frame queue and reset texture state for new video
        while (_videoFrameQueue.TryDequeue(out _)) { } // Clear queue
        _videoTextureInitialized = false; // Reset texture state
        
        // Clear shader and image when loading video - IMPORTANT: must clear loaded paths too
        // This prevents the render loop from reloading the old shader
        _currentFragPath = null;
        _currentImagePath = null;
        _loadedFragPath = null; // CRITICAL: Clear this so shader check doesn't reload old shader
        _loadedImagePath = null;
        
        // Clear the current shader program so video shader can take over
        // Don't set _program = 0 here, let the video shader be built instead
        
        // Ensure video dimensions are set (will be updated from preloaded or decode)
        if (_videoWidth == 0) _videoWidth = _projectWidth;
        if (_videoHeight == 0) _videoHeight = _projectHeight;
        
        _logCallback?.Invoke($"LoadVideo called: {Path.GetFileName(videoPath)}");
        
        // Check if video is preloaded in cache
        PreloadedVideo? preloaded = null;
        lock (_cacheLock) {
            if (_videoCache.TryGetValue(videoPath, out var cached)) {
                preloaded = cached;
            }
        }
        
        if (preloaded != null && preloaded.IsPreloaded && preloaded.FirstFrame != null) {
            // Use preloaded video for instant switching
            _videoWidth = preloaded.Width;
            _videoHeight = preloaded.Height;
            _videoFps = preloaded.Fps;
            _videoFrameCount = preloaded.FrameCount;
            
            // Upload first frame immediately
            lock (preloaded.FrameLock) {
                if (preloaded.FirstFrame != null) {
                    lock (_videoFrameLock) {
                        _pendingVideoFrame = preloaded.FirstFrame;
                        _hasNewVideoFrame = true;
                    }
                }
            }
            
            // Start using the preloaded video's decode stream
            _currentVideoPath = videoPath;
            // Don't set _loadedVideoPath yet - let render loop detect it and build shader
            // This ensures the render loop's needsLoad check triggers and builds the shader
            
            // Ensure preloaded video decode is running
            if (preloaded.DecodeTask == null || preloaded.DecodeTask.IsCompleted) {
                StartPreloadedVideoDecode(videoPath);
            }
            
            // NOTE: Don't build shader here - OpenGL context may not be available on UI thread
            // The render loop will build it when _gl is available
            
            // Request immediate render to display the first frame
            RequestNextFrameRendering();
            
            message = $"Video loaded instantly from cache: {Path.GetFileName(videoPath)}";
            _logCallback?.Invoke(message);
        } else {
            // Video not preloaded - load on-demand immediately
            // Set both paths so render loop picks it up
            _currentVideoPath = videoPath;
            _loadedVideoPath = null; // Clear so render loop detects it as new
            
            // NOTE: Don't build shader here - OpenGL context may not be available on UI thread
            // The render loop will build the shader when _gl is available
            
            message = "Video path set successfully (loading on-demand)";
            
            // Request render to trigger loading and shader building
            RequestNextFrameRendering();
            
            // Preload this video in background for future instant switching
            _ = Task.Run(() => PreloadVideo(videoPath));
        }
    }
    
    /// <summary>
    /// Preloads a video into memory cache for instant switching.
    /// Decodes first frame immediately and starts background decode loop.
    /// </summary>
    /// <param name="videoPath">Path to the video file to preload</param>
    public void PreloadVideo(string videoPath) {
        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath)) {
            return;
        }
        
        lock (_cacheLock) {
            // Check if already preloaded or in progress
            if (_videoCache.ContainsKey(videoPath)) {
                return; // Already preloaded or preloading
            }
            
            // Create preload entry
            var preloaded = new PreloadedVideo {
                FilePath = videoPath,
                IsPreloaded = false
            };
            _videoCache[videoPath] = preloaded;
        }
        
        try {
            // Decode first frame immediately
            int frameSize = _projectWidth * _projectHeight * 4; // RGBA
            
            // Use a simple stream to capture first frame
            var firstFrameReady = new System.Threading.ManualResetEventSlim(false);
            byte[]? firstFrameData = null;
            
            using var firstFrameSink = new SingleFrameStream(frameSize, (frame) => {
                if (frame.Length >= frameSize) {
                    firstFrameData = frame.ToArray();
                    firstFrameReady.Set();
                }
            });
            
            // Decode first frame
            FFMpegArguments
                .FromFileInput(videoPath)
                .OutputToPipe(new FFMpegCore.Pipes.StreamPipeSink(firstFrameSink), options => options
                    .WithVideoCodec("rawvideo")
                    .ForceFormat("rawvideo")
                    .WithCustomArgument($"-vf scale={_projectWidth}:{_projectHeight}")
                    .WithCustomArgument("-pix_fmt rgba")
                    .WithCustomArgument("-frames:v 1")
                    .WithCustomArgument("-an"))
                .ProcessAsynchronously()
                .GetAwaiter()
                .GetResult();
            
            // Get video metadata
            var info = FFProbe.AnalyseAsync(videoPath).GetAwaiter().GetResult();
            var videoStream = info.PrimaryVideoStream;
            
            if (videoStream == null || firstFrameData == null) {
                lock (_cacheLock) {
                    _videoCache.Remove(videoPath);
                }
                return;
            }
            
            // Store preloaded data
            lock (_cacheLock) {
                if (_videoCache.TryGetValue(videoPath, out var preloaded)) {
                    preloaded.FirstFrame = firstFrameData;
                    preloaded.Width = _projectWidth;
                    preloaded.Height = _projectHeight;
                    preloaded.Fps = videoStream.AvgFrameRate;
                    if (preloaded.Fps <= 1e-3) preloaded.Fps = 30.0;
                    preloaded.FrameCount = (int)((info.Duration.TotalSeconds * preloaded.Fps) + 0.5);
                    preloaded.IsPreloaded = true;
                }
            }
            
            _logCallback?.Invoke($"Video preloaded: {Path.GetFileName(videoPath)}");
            
            // Start background decode loop for this preloaded video
            StartPreloadedVideoDecode(videoPath);
        }
        catch (Exception ex) {
            _logCallback?.Invoke($"Error preloading video {Path.GetFileName(videoPath)}: {ex.Message}");
            lock (_cacheLock) {
                _videoCache.Remove(videoPath);
            }
        }
    }
    
    /// <summary>
    /// Starts background decoding for a preloaded video
    /// </summary>
    private void StartPreloadedVideoDecode(string videoPath) {
        PreloadedVideo? preloaded;
        lock (_cacheLock) {
            if (!_videoCache.TryGetValue(videoPath, out preloaded) || !preloaded.IsPreloaded) {
                return;
            }
            
            // Cancel any existing decode task
            try {
                preloaded.DecodeCts?.Cancel();
                preloaded.DecodeCts?.Dispose();
            } catch { }
            
            preloaded.DecodeCts = new CancellationTokenSource();
        }
        
        var ct = preloaded.DecodeCts.Token;
        int frameSize = preloaded.Width * preloaded.Height * 4;
        var frameDuration = TimeSpan.FromSeconds(1.0 / preloaded.Fps);
        
        preloaded.DecodeTask = Task.Run(async () => {
            try {
                while (!ct.IsCancellationRequested) {
                    using var sink = new VideoFrameStream(frameSize, frameDuration, (frame) => {
                        lock (preloaded.FrameLock) {
                            preloaded.CurrentFrame = frame.ToArray();
                            preloaded.HasNewFrame = true;
                        }
                    }, ct);
                    
                    try {
                        await FFMpegArguments
                            .FromFileInput(videoPath)
                            .OutputToPipe(new FFMpegCore.Pipes.StreamPipeSink(sink), options => options
                                .WithVideoCodec("rawvideo")
                                .ForceFormat("rawvideo")
                                .WithCustomArgument($"-vf scale={_projectWidth}:{_projectHeight}")
                                .WithCustomArgument("-pix_fmt rgba")
                                .WithCustomArgument("-an"))
                            .ProcessAsynchronously();
                    }
                    catch (OperationCanceledException) {
                        break;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) {
                _logCallback?.Invoke($"Error in preloaded video decode: {ex.Message}");
            }
        }, ct);
    }
    
    /// <summary>
    /// Stops video playback and cleans up video resources.
    /// Non-blocking version for instant switching - cancels task but doesn't wait.
    /// </summary>
    private void StopVideo() {
        // Cancel the decoding task (non-blocking for instant switching)
        try {
            _videoCts?.Cancel();
        } catch { }
        
        // Don't wait for task completion - let it finish in background
        // This allows instant switching between videos
        // The task will complete and cleanup will happen asynchronously
        
        // Clean up cancellation token (but keep task reference for now)
        try {
            _videoCts?.Dispose();
        } catch { }
        _videoCts = null;
        
        // Note: We don't clear _videoDecodeTask here - let it finish in background
        // This prevents blocking on task completion
        
        // Clear pending frame data (but keep texture visible until new one is ready)
        lock (_videoFrameLock) {
            _pendingVideoFrame = null;
            _hasNewVideoFrame = false;
        }
        
        _currentVideoPath = null;
        // Don't clear _loadedVideoPath immediately - keep it until new video is ready
    }
    
    /// <summary>
    /// Cleans up old video task reference (called after new video starts)
    /// </summary>
    private void CleanupOldVideoTask() {
        // Clean up old task reference if it's completed
        if (_videoDecodeTask != null && _videoDecodeTask.IsCompleted) {
            try {
                _videoDecodeTask.Dispose();
            } catch { }
            _videoDecodeTask = null;
        }
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
    
    // ========================
    // Video Decoding Methods
    // ========================
    /// <summary>
    /// Starts video decoding in a background task.
    /// Decodes frames and uploads them to the video texture for rendering.
    /// Optimized for instant switching - decodes first frame immediately.
    /// </summary>
    private void StartVideoDecoding(string filePath) {
        // Clean up old task if completed (non-blocking)
        CleanupOldVideoTask();
        
        // Stop any existing video (non-blocking - just cancels)
        StopVideo();
        
        _videoCts = new CancellationTokenSource();
        var ct = _videoCts.Token;
        
        // Start decoding in background task and track it
        _videoDecodeTask = Task.Run(async () => {
            try {
                await VideoDecodeLoopAsync(filePath, ct);
            }
            catch (OperationCanceledException) {
                // Expected when video is stopped - task completes normally
            }
            catch (Exception ex) {
                _logCallback?.Invoke($"Video decoding error: {ex.Message}");
            }
        }, ct);
        
        // Configure task continuation for cleanup
        _videoDecodeTask.ContinueWith(t => {
            if (t.IsFaulted && t.Exception != null) {
                _logCallback?.Invoke($"Video decode task faulted: {t.Exception.InnerException?.Message ?? "Unknown error"}");
            }
            // Clean up task reference when completed
            try {
                t.Dispose();
            } catch { }
        }, TaskContinuationOptions.ExecuteSynchronously);
    }
    
    /// <summary>
    /// Main video decoding loop. Decodes video frames and calls OnVideoFrameReady for each frame.
    /// Automatically loops when the video ends.
    /// Videos are scaled to project size during decoding for consistent output resolution.
    /// Optimized for instant first frame display - decodes first frame immediately without waiting for analysis.
    /// </summary>
    private async Task VideoDecodeLoopAsync(string filePath, CancellationToken ct) {
        try {
            // Use project size for output (scale video to project size)
            _videoWidth = _projectWidth;
            _videoHeight = _projectHeight;
            
            int frameSize = _videoWidth * _videoHeight * 4; // RGBA (project size)
            
            // Start analysis in background (don't wait for it)
            var infoTask = Task.Run(async () => await FFProbe.AnalyseAsync(filePath));
            
            // Decode first few frames immediately for instant display (don't wait for analysis)
            // This allows instant video switching and smooth start
            // CRITICAL: Track how many frames we decode so we can skip them in main loop (prevents double-playback bump)
            int framesDecodedAhead = MAX_QUEUED_FRAMES;
            try {
                using var firstFrameSink = new VideoFrameStream(frameSize, TimeSpan.Zero, OnVideoFrameReady, ct);
                
                await FFMpegArguments
                    .FromFileInput(filePath)
                    .OutputToPipe(new FFMpegCore.Pipes.StreamPipeSink(firstFrameSink), options => options
                        .WithVideoCodec("rawvideo")
                        .ForceFormat("rawvideo")
                        .WithCustomArgument($"-vf scale={_projectWidth}:{_projectHeight}:flags=fast_bilinear") // Fast scaling
                        .WithCustomArgument("-pix_fmt rgba")
                        .WithCustomArgument($"-frames:v {MAX_QUEUED_FRAMES}") // Decode 2-3 frames ahead
                        .WithCustomArgument("-an") // no audio
                        .WithCustomArgument("-threads 2")) // Use 2 threads for faster decoding
                    .ProcessAsynchronously();
                
                // Mark video as loaded now that first frame is ready
                _loadedVideoPath = _currentVideoPath;
            }
            catch (OperationCanceledException) {
                return; // Video was stopped
            }
            
            // Get video metadata (analysis should be done by now, but wait if needed)
            var info = await infoTask;
            int nativeWidth = info.PrimaryVideoStream?.Width ?? 0;
            int nativeHeight = info.PrimaryVideoStream?.Height ?? 0;
            
            if (nativeWidth <= 0 || nativeHeight <= 0) {
                _logCallback?.Invoke($"Invalid video dimensions: {nativeWidth}x{nativeHeight}");
                return;
            }
            
            // Get video metadata
            _videoFps = info.PrimaryVideoStream?.AvgFrameRate ?? 30.0;
            if (_videoFps <= 1e-3) _videoFps = 30.0;
            
            var duration = info.Duration;
            _videoFrameCount = (int)((duration.TotalSeconds * _videoFps) + 0.5);
            
            var frameDuration = TimeSpan.FromSeconds(1.0 / _videoFps);
            
            // Calculate seek time to skip already-decoded frames (prevents double-playback bump)
            var seekTime = TimeSpan.FromSeconds(framesDecodedAhead / _videoFps);
            
            _logCallback?.Invoke($"Video decode started: {Path.GetFileName(filePath)} (native: {nativeWidth}x{nativeHeight}, scaled to: {_videoWidth}x{_videoHeight}, {_videoFps} fps, {_videoFrameCount} frames, skipping {framesDecodedAhead} frames to prevent bump)");
            
            // Continue decoding rest of video
            // CRITICAL: Seek past the frames we already decoded to avoid double-playback bump
            while (!ct.IsCancellationRequested) {
                using var sink = new VideoFrameStream(frameSize, frameDuration, OnVideoFrameReady, ct);
                
                try {
                    // Scale video to project size during decoding
                    // Seek past the frames we already decoded to prevent the "bump"
                    var optionsBuilder = new System.Action<FFMpegCore.FFMpegArgumentOptions>(options => {
                        options
                            .WithVideoCodec("rawvideo")
                            .ForceFormat("rawvideo")
                            .WithCustomArgument($"-vf scale={_projectWidth}:{_projectHeight}") // Scale to project size
                            .WithCustomArgument("-pix_fmt rgba")
                            .WithCustomArgument("-an"); // no audio
                        
                        // Only seek if we decoded frames ahead (skip them to prevent double-playback)
                        if (framesDecodedAhead > 0 && seekTime.TotalSeconds > 0) {
                            // Use -ss to skip already-decoded frames (prevents double-playback bump)
                            options.WithCustomArgument($"-ss {seekTime.TotalSeconds:F6}"); // Seek to skip frames
                        }
                    });
                    
                    await FFMpegArguments
                        .FromFileInput(filePath)
                        .OutputToPipe(new FFMpegCore.Pipes.StreamPipeSink(sink), optionsBuilder)
                        .ProcessAsynchronously();
                }
                catch (OperationCanceledException) {
                    break; // Video was stopped
                }
                
                // If we reach here, video finished - loop back to start (reset seek for next loop)
                if (!ct.IsCancellationRequested) {
                    _logCallback?.Invoke("Video finished, looping...");
                    seekTime = TimeSpan.Zero; // Reset seek for next loop iteration
                    framesDecodedAhead = 0; // Reset for next loop
                }
            }
        }
        catch (OperationCanceledException) {
            // Expected when video is stopped
        }
        catch (Exception ex) {
            _logCallback?.Invoke($"Video decode error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Callback when a new video frame is decoded.
    /// Stores the frame data to be uploaded to GPU on next render.
    /// Optimized: Queues frames ahead for smooth playback, prevents frame drops.
    /// </summary>
    private void OnVideoFrameReady(ReadOnlyMemory<byte> frame) {
        var frameArray = frame.ToArray();
        
        // Queue frame for smooth playback (decode ahead)
        // If queue is full, replace oldest frame to prevent memory buildup
        if (_videoFrameQueue.Count >= MAX_QUEUED_FRAMES)
        {
            // Remove oldest frame to make room
            _videoFrameQueue.TryDequeue(out _);
        }
        
        // Add new frame to queue
        _videoFrameQueue.Enqueue(frameArray);
        
        // Also set as pending for immediate display if queue was empty
        lock (_videoFrameLock) {
            _pendingVideoFrame = frameArray;
            _hasNewVideoFrame = true;
        }
    }
    
    // Helper class for video frame streaming with looping support
    private sealed class VideoFrameStream : Stream
    {
        private readonly int _frameSize;
        private readonly MemoryStream _buffer = new();
        private readonly Action<ReadOnlyMemory<byte>> _onFrame;
        private readonly TimeSpan _frameDuration;
        private readonly CancellationToken _ct;
        private DateTime _nextDue;

        public VideoFrameStream(int frameSize, TimeSpan frameDuration, Action<ReadOnlyMemory<byte>> onFrame, CancellationToken ct)
        {
            _frameSize = frameSize;
            _frameDuration = frameDuration;
            _onFrame = onFrame;
            _ct = ct;
            _nextDue = DateTime.UtcNow;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _buffer.Length;
        public override long Position { get => _buffer.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_ct.IsCancellationRequested) return;
            _buffer.Write(buffer, offset, count);
            TryDrain();
        }
        
#if NET8_0_OR_GREATER
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (_ct.IsCancellationRequested) return;
            _buffer.Write(buffer);
            TryDrain();
        }
#endif
        
        private void TryDrain()
        {
            while (_buffer.Length >= _frameSize && !_ct.IsCancellationRequested)
            {
                _buffer.Position = 0;
                var frameBytes = ArrayPool<byte>.Shared.Rent(_frameSize);
                int read = _buffer.Read(frameBytes, 0, _frameSize);
                _onFrame(new ReadOnlyMemory<byte>(frameBytes, 0, read));

                // Throttle to target frame duration
                if (_frameDuration > TimeSpan.Zero)
                {
                    var now = DateTime.UtcNow;
                    if (now < _nextDue)
                    {
                        var sleep = _nextDue - now;
                        if (sleep > TimeSpan.Zero && !_ct.IsCancellationRequested)
                            Thread.Sleep(sleep);
                    }
                    _nextDue = _nextDue + _frameDuration;
                }

                // Compact remaining bytes
                var remaining = (int)(_buffer.Length - _buffer.Position);
                if (remaining > 0)
                {
                    var tmp = new byte[remaining];
                    _buffer.Read(tmp, 0, remaining);
                    _buffer.SetLength(0);
                    _buffer.Position = 0;
                    _buffer.Write(tmp, 0, remaining);
                }
                else
                {
                    _buffer.SetLength(0);
                    _buffer.Position = 0;
                }
                ArrayPool<byte>.Shared.Return(frameBytes);
            }
        }
    }
    
    // Helper class for single-frame image decoding
    private sealed class SingleFrameStream : Stream
    {
        private readonly int _frameSize;
        private readonly MemoryStream _buffer = new();
        private readonly Action<ReadOnlyMemory<byte>> _onFrame;
        private bool _frameReceived;

        public SingleFrameStream(int frameSize, Action<ReadOnlyMemory<byte>> onFrame)
        {
            _frameSize = frameSize;
            _onFrame = onFrame;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _buffer.Length;
        public override long Position { get => _buffer.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!_frameReceived)
            {
                _buffer.Write(buffer, offset, count);
                if (_buffer.Length >= _frameSize)
                {
                    _buffer.Position = 0;
                    var frameBytes = new byte[_frameSize];
                    int read = _buffer.Read(frameBytes, 0, _frameSize);
                    _onFrame(new ReadOnlyMemory<byte>(frameBytes, 0, read));
                    _frameReceived = true;
                }
            }
        }

#if NET8_0_OR_GREATER
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (!_frameReceived)
            {
                _buffer.Write(buffer);
                if (_buffer.Length >= _frameSize)
                {
                    _buffer.Position = 0;
                    var frameBytes = new byte[_frameSize];
                    int read = _buffer.Read(frameBytes, 0, _frameSize);
                    _onFrame(new ReadOnlyMemory<byte>(frameBytes, 0, read));
                    _frameReceived = true;
                }
            }
        }
#endif
    }
}

