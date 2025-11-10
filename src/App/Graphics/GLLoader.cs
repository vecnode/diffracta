using Avalonia.OpenGL;
using System;
using System.Runtime.InteropServices;

namespace Diffracta.Graphics;

// ========================
// GLLoader - OpenGL Function Wrapper
// ========================
// This class provides a type-safe wrapper around OpenGL functions accessed through Avalonia's GlInterface.
// It dynamically loads OpenGL function pointers at runtime and provides delegates for calling them.
// This approach is necessary because OpenGL functions are platform-specific and loaded dynamically.
internal sealed class GLLoader
{
    // ========================
    // Fields
    // ========================
    private readonly GlInterface _gl; // Avalonia's OpenGL interface for accessing function pointers

    // ========================
    // Constructor
    // ========================
    public GLLoader(GlInterface gl) { _gl = gl; }

    // ========================
    // Helper Methods
    // ========================
    /// <summary>
    /// Dynamically loads an OpenGL function by name and converts it to a typed delegate.
    /// Throws an exception if the function is not found (OpenGL function not available).
    /// </summary>
    /// <typeparam name="T">The delegate type matching the OpenGL function signature</typeparam>
    /// <param name="name">The name of the OpenGL function (e.g., "glViewport")</param>
    /// <returns>A delegate that can be called to invoke the OpenGL function</returns>
    private T Load<T>(string name) where T : Delegate
    {
        var ptr = _gl.GetProcAddress(name);
        if (ptr == IntPtr.Zero) throw new InvalidOperationException($"GL function not found: {name}");
        return Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }

    // ========================
    // OpenGL Function Delegates
    // ========================
    // These delegates define the function signatures for OpenGL calls.
    // All use Cdecl calling convention as required by OpenGL.
    
    // Viewport and Clear Functions
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glViewport_d(int x,int y,int w,int h);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glClearColor_d(float r,float g,float b,float a);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glClear_d(uint mask);

    // Shader Compilation and Program Management
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate uint glCreateShader_d(uint type);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glShaderSource_d(uint shader, int count, string[] strings, int[] lengths);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glCompileShader_d(uint shader);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glGetShaderiv_d(uint shader, uint pname, out int param);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glGetShaderInfoLog_d(uint shader, int maxLength, out int length, System.Text.StringBuilder infoLog);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate uint glCreateProgram_d();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glAttachShader_d(uint program, uint shader);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glLinkProgram_d(uint program);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glGetProgramiv_d(uint program, uint pname, out int param);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glGetProgramInfoLog_d(uint program, int maxLength, out int length, System.Text.StringBuilder infoLog);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glDeleteShader_d(uint shader);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glUseProgram_d(uint program);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int  glGetUniformLocation_d(uint program, string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glUniform1f_d(int loc, float v0);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glUniform2f_d(int loc, float v0, float v1);

    // Vertex Array Object (VAO) Functions
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glGenVertexArrays_d(int n, out uint arrays);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glBindVertexArray_d(uint array);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glDrawArrays_d(uint mode, int first, int count);

    // Vertex Buffer Object (VBO) Functions
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glGenBuffers_d(int n, out uint buffers);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glBindBuffer_d(uint target, uint buffer);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glBufferData_d(uint target, int size, float[] data, uint usage);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glVertexAttribPointer_d(uint index, int size, uint type, bool normalized, int stride, int offset);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glEnableVertexAttribArray_d(uint index);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glDeleteBuffers_d(int n, ref uint buffers);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glDeleteVertexArrays_d(int n, ref uint arrays);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glDeleteProgram_d(uint program);

    // Texture Functions
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glGenTextures_d(int n, out uint textures);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glBindTexture_d(uint target, uint texture);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glTexImage2D_d(uint target, int level, int internalformat, int width, int height, int border, uint format, uint type, IntPtr pixels);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glTexParameteri_d(uint target, uint pname, int param);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glDeleteTextures_d(int n, ref uint textures);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glActiveTexture_d(uint texture);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glUniform1i_d(int location, int value);

    // Framebuffer Functions
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glGenFramebuffers_d(int n, out uint framebuffers);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glBindFramebuffer_d(uint target, uint framebuffer);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glFramebufferTexture2D_d(uint target, uint attachment, uint textarget, uint texture, int level);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate uint glCheckFramebufferStatus_d(uint target);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void glDeleteFramebuffers_d(int n, ref uint framebuffers);

    // ========================
    // OpenGL Constants
    // ========================
    // These constants match OpenGL enum values and are used throughout the rendering code.
    
    // Buffer and Clear Constants
    public const uint GL_COLOR_BUFFER_BIT = 0x00004000;
    
    // Shader Type Constants
    public const uint GL_FRAGMENT_SHADER  = 0x8B30;
    public const uint GL_VERTEX_SHADER    = 0x8B31;
    public const uint GL_COMPILE_STATUS   = 0x8B81;
    public const uint GL_LINK_STATUS      = 0x8B82;
    
    // Drawing Constants
    public const uint GL_TRIANGLES        = 0x0004;
    public const uint GL_ARRAY_BUFFER     = 0x8892;
    public const uint GL_STATIC_DRAW      = 0x88E4;
    public const uint GL_FLOAT           = 0x1406;

    // Texture Constants
    public const uint GL_TEXTURE_2D       = 0x0DE1;
    public const uint GL_TEXTURE0         = 0x84C0;
    public const uint GL_RGBA             = 0x1908;
    public const uint GL_UNSIGNED_BYTE    = 0x1401;
    public const uint GL_TEXTURE_MIN_FILTER = 0x2801;
    public const uint GL_TEXTURE_MAG_FILTER = 0x2800;
    public const uint GL_LINEAR           = 0x2601;

    // Framebuffer Constants
    public const uint GL_FRAMEBUFFER      = 0x8D40;
    public const uint GL_COLOR_ATTACHMENT0 = 0x8CE0;
    public const uint GL_FRAMEBUFFER_COMPLETE = 0x8CD5;

    // ========================
    // Loaded Function Pointers
    // ========================
    // These fields hold the actual function delegates loaded at runtime.
    // They are initialized by the Initialize() method and used throughout the rendering code.
    
    // Viewport and Clear
    public glViewport_d glViewport = null!;
    public glClearColor_d glClearColor = null!;
    public glClear_d glClear = null!;

    // Shader and Program Management
    public glCreateShader_d glCreateShader = null!;
    public glShaderSource_d glShaderSource = null!;
    public glCompileShader_d glCompileShader = null!;
    public glGetShaderiv_d glGetShaderiv = null!;
    public glGetShaderInfoLog_d glGetShaderInfoLog = null!;
    public glCreateProgram_d glCreateProgram = null!;
    public glAttachShader_d glAttachShader = null!;
    public glLinkProgram_d glLinkProgram = null!;
    public glGetProgramiv_d glGetProgramiv = null!;
    public glGetProgramInfoLog_d glGetProgramInfoLog = null!;
    public glDeleteShader_d glDeleteShader = null!;
    public glUseProgram_d glUseProgram = null!;
    public glGetUniformLocation_d glGetUniformLocation = null!;
    public glUniform1f_d glUniform1f = null!;
    public glUniform2f_d glUniform2f = null!;

    // Vertex Array Operations
    public glGenVertexArrays_d glGenVertexArrays = null!;
    public glBindVertexArray_d glBindVertexArray = null!;
    public glDrawArrays_d glDrawArrays = null!;

    // Buffer Operations
    public glGenBuffers_d glGenBuffers = null!;
    public glBindBuffer_d glBindBuffer = null!;
    public glBufferData_d glBufferData = null!;
    public glVertexAttribPointer_d glVertexAttribPointer = null!;
    public glEnableVertexAttribArray_d glEnableVertexAttribArray = null!;
    public glDeleteBuffers_d glDeleteBuffers = null!;
    public glDeleteVertexArrays_d glDeleteVertexArrays = null!;
    public glDeleteProgram_d glDeleteProgram = null!;

    // Texture Operations
    public glGenTextures_d glGenTextures = null!;
    public glBindTexture_d glBindTexture = null!;
    public glTexImage2D_d glTexImage2D = null!;
    public glTexParameteri_d glTexParameteri = null!;
    public glDeleteTextures_d glDeleteTextures = null!;
    public glActiveTexture_d glActiveTexture = null!;
    public glUniform1i_d glUniform1i = null!;

    // Framebuffer Operations
    public glGenFramebuffers_d glGenFramebuffers = null!;
    public glBindFramebuffer_d glBindFramebuffer = null!;
    public glFramebufferTexture2D_d glFramebufferTexture2D = null!;
    public glCheckFramebufferStatus_d glCheckFramebufferStatus = null!;
    public glDeleteFramebuffers_d glDeleteFramebuffers = null!;

    // ========================
    // Initialization
    // ========================
    /// <summary>
    /// Loads all OpenGL function pointers at runtime.
    /// This must be called before any OpenGL operations can be performed.
    /// Throws an exception if any required function cannot be loaded.
    /// </summary>
    public void Initialize()
    {
        try
        {
            // Load viewport and clear functions
            glViewport = Load<glViewport_d>("glViewport");
            glClearColor = Load<glClearColor_d>("glClearColor");
            glClear = Load<glClear_d>("glClear");

            // Load shader compilation and program management functions
            glCreateShader = Load<glCreateShader_d>("glCreateShader");
            glShaderSource = Load<glShaderSource_d>("glShaderSource");
            glCompileShader = Load<glCompileShader_d>("glCompileShader");
            glGetShaderiv = Load<glGetShaderiv_d>("glGetShaderiv");
            glGetShaderInfoLog = Load<glGetShaderInfoLog_d>("glGetShaderInfoLog");
            glCreateProgram = Load<glCreateProgram_d>("glCreateProgram");
            glAttachShader = Load<glAttachShader_d>("glAttachShader");
            glLinkProgram = Load<glLinkProgram_d>("glLinkProgram");
            glGetProgramiv = Load<glGetProgramiv_d>("glGetProgramiv");
            glGetProgramInfoLog = Load<glGetProgramInfoLog_d>("glGetProgramInfoLog");
            glDeleteShader = Load<glDeleteShader_d>("glDeleteShader");
            glUseProgram = Load<glUseProgram_d>("glUseProgram");
            glGetUniformLocation = Load<glGetUniformLocation_d>("glGetUniformLocation");
            glUniform1f = Load<glUniform1f_d>("glUniform1f");
            glUniform2f = Load<glUniform2f_d>("glUniform2f");

            // Load vertex array object functions
            glGenVertexArrays = Load<glGenVertexArrays_d>("glGenVertexArrays");
            glBindVertexArray = Load<glBindVertexArray_d>("glBindVertexArray");
            glDrawArrays = Load<glDrawArrays_d>("glDrawArrays");

            // Load buffer management functions
            glGenBuffers = Load<glGenBuffers_d>("glGenBuffers");
            glBindBuffer = Load<glBindBuffer_d>("glBindBuffer");
            glBufferData = Load<glBufferData_d>("glBufferData");
            glVertexAttribPointer = Load<glVertexAttribPointer_d>("glVertexAttribPointer");
            glEnableVertexAttribArray = Load<glEnableVertexAttribArray_d>("glEnableVertexAttribArray");
            glDeleteBuffers = Load<glDeleteBuffers_d>("glDeleteBuffers");
            glDeleteVertexArrays = Load<glDeleteVertexArrays_d>("glDeleteVertexArrays");
            glDeleteProgram = Load<glDeleteProgram_d>("glDeleteProgram");

            // Load texture and framebuffer functions
            glGenTextures = Load<glGenTextures_d>("glGenTextures");
            glBindTexture = Load<glBindTexture_d>("glBindTexture");
            glTexImage2D = Load<glTexImage2D_d>("glTexImage2D");
            glTexParameteri = Load<glTexParameteri_d>("glTexParameteri");
            glDeleteTextures = Load<glDeleteTextures_d>("glDeleteTextures");
            glActiveTexture = Load<glActiveTexture_d>("glActiveTexture");
            glUniform1i = Load<glUniform1i_d>("glUniform1i");

            glGenFramebuffers = Load<glGenFramebuffers_d>("glGenFramebuffers");
            glBindFramebuffer = Load<glBindFramebuffer_d>("glBindFramebuffer");
            glFramebufferTexture2D = Load<glFramebufferTexture2D_d>("glFramebufferTexture2D");
            glCheckFramebufferStatus = Load<glCheckFramebufferStatus_d>("glCheckFramebufferStatus");
            glDeleteFramebuffers = Load<glDeleteFramebuffers_d>("glDeleteFramebuffers");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to initialize OpenGL functions: {ex.Message}", ex);
        }
    }
}
