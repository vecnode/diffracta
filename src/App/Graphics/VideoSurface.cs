using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using FFMpegCore;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Diffracta.Graphics;

public sealed class VideoSurface : OpenGlControlBase
{
    private GLLoader? _gl;
    private uint _program;
    private uint _vao;
    private uint _vbo;
    private uint _texture;
    private int _uRes = -1;

    private volatile int _videoWidth;
    private volatile int _videoHeight;

    private readonly object _frameLock = new();
    private byte[]? _pendingFrame; // RGBA
    private bool _hasNewFrame;

    private CancellationTokenSource? _cts;

    private const string VertexSrc = """
        #version 300 es
        precision mediump float;
        layout(location = 0) in vec2 aPos;
        layout(location = 1) in vec2 aUV;
        out vec2 vUV;
        void main(){ vUV = aUV; gl_Position = vec4(aPos, 0.0, 1.0); }
    """;

    private const string FragSrc = """
        #version 300 es
        precision mediump float;
        in vec2 vUV;
        out vec4 FragColor;
        uniform sampler2D u_texture;
        uniform vec2 u_resolution;
        void main(){ vec2 uv = vec2(vUV.x, 1.0 - vUV.y); FragColor = texture(u_texture, uv); }
    """;

    public void Start(string filePath)
    {
        Stop();
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => DecodeLoopAsync(filePath, _cts.Token));
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        _cts = null;
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        _gl = new GLLoader(gl);
        _gl.Initialize();

        // Fullscreen triangle (same as ShaderSurface)
        float[] vertices = {
            -1.0f, -1.0f,  0.0f, 0.0f,
             3.0f, -1.0f,  2.0f, 0.0f,
            -1.0f,  3.0f,  0.0f, 2.0f
        };

        _gl.glGenVertexArrays(1, out _vao);
        _gl.glBindVertexArray(_vao);
        _gl.glGenBuffers(1, out _vbo);
        _gl.glBindBuffer(GLLoader.GL_ARRAY_BUFFER, _vbo);
        _gl.glBufferData(GLLoader.GL_ARRAY_BUFFER, vertices.Length * sizeof(float), vertices, GLLoader.GL_STATIC_DRAW);
        _gl.glVertexAttribPointer(0, 2, GLLoader.GL_FLOAT, false, 4 * sizeof(float), 0);
        _gl.glEnableVertexAttribArray(0);
        _gl.glVertexAttribPointer(1, 2, GLLoader.GL_FLOAT, false, 4 * sizeof(float), 2 * sizeof(float));
        _gl.glEnableVertexAttribArray(1);

        _program = BuildProgram(VertexSrc, FragSrc);
        _uRes = _gl.glGetUniformLocation(_program, "u_resolution");

        // Create empty texture
        _gl.glGenTextures(1, out _texture);
        _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _texture);
        _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MIN_FILTER, (int)GLLoader.GL_LINEAR);
        _gl.glTexParameteri(GLLoader.GL_TEXTURE_2D, GLLoader.GL_TEXTURE_MAG_FILTER, (int)GLLoader.GL_LINEAR);

        RequestNextFrameRendering();
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_gl is null) return;

        var scale = VisualRoot?.RenderScaling ?? 1.0;
        int w = Math.Max(1, (int)(Bounds.Width * scale));
        int h = Math.Max(1, (int)(Bounds.Height * scale));

        // If there's a new decoded frame, upload it
        if (_hasNewFrame && _pendingFrame != null && _videoWidth > 0 && _videoHeight > 0)
        {
            lock (_frameLock)
            {
                _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _texture);
                if (_videoWidth > 0 && _videoHeight > 0)
                {
                    // Upload RGBA frame (pin managed buffer)
                    var arr = _pendingFrame;
                    if (arr != null)
                    {
                        var handle = System.Runtime.InteropServices.GCHandle.Alloc(arr, System.Runtime.InteropServices.GCHandleType.Pinned);
                        try
                        {
                            _gl.glTexImage2D(GLLoader.GL_TEXTURE_2D, 0, (int)GLLoader.GL_RGBA, _videoWidth, _videoHeight, 0, GLLoader.GL_RGBA, GLLoader.GL_UNSIGNED_BYTE, handle.AddrOfPinnedObject());
                        }
                        finally { handle.Free(); }
                    }
                }
                _hasNewFrame = false;
            }
        }

        _gl.glBindFramebuffer(GLLoader.GL_FRAMEBUFFER, (uint)fb);
        _gl.glViewport(0, 0, w, h);
        _gl.glClearColor(0, 0, 0, 1);
        _gl.glClear(GLLoader.GL_COLOR_BUFFER_BIT);

        _gl.glUseProgram(_program);
        if (_uRes >= 0) _gl.glUniform2f(_uRes, w, h);
        _gl.glActiveTexture(GLLoader.GL_TEXTURE0);
        _gl.glBindTexture(GLLoader.GL_TEXTURE_2D, _texture);
        _gl.glUniform1i(_gl.glGetUniformLocation(_program, "u_texture"), 0);
        _gl.glBindVertexArray(_vao);
        _gl.glDrawArrays(GLLoader.GL_TRIANGLES, 0, 3);

        RequestNextFrameRendering();
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        Stop();
        if (_gl != null)
        {
            if (_vbo != 0) _gl.glDeleteBuffers(1, ref _vbo);
            if (_vao != 0) _gl.glDeleteVertexArrays(1, ref _vao);
            if (_program != 0) _gl.glDeleteProgram(_program);
            if (_texture != 0) _gl.glDeleteTextures(1, ref _texture);
        }
    }

    private uint BuildProgram(string vertex, string fragment)
    {
        if (_gl is null) return 0;
        uint vs = Compile(GLLoader.GL_VERTEX_SHADER, vertex);
        uint fs = Compile(GLLoader.GL_FRAGMENT_SHADER, fragment);
        uint prog = _gl.glCreateProgram();
        _gl.glAttachShader(prog, vs);
        _gl.glAttachShader(prog, fs);
        _gl.glLinkProgram(prog);
        _gl.glGetProgramiv(prog, GLLoader.GL_LINK_STATUS, out int linked);
        if (linked == 0)
        {
            var sb = new StringBuilder(2048);
            _gl.glGetProgramInfoLog(prog, sb.Capacity, out int len, sb);
            throw new InvalidOperationException("GL link error: " + sb.ToString(0, Math.Max(0, len)));
        }
        _gl.glDeleteShader(vs);
        _gl.glDeleteShader(fs);
        return prog;
    }

    private uint Compile(uint type, string src)
    {
        if (_gl is null) return 0;
        uint sh = _gl.glCreateShader(type);
        var arr = new[] { src };
        var lengths = new[] { src.Length };
        _gl.glShaderSource(sh, 1, arr, lengths);
        _gl.glCompileShader(sh);
        _gl.glGetShaderiv(sh, GLLoader.GL_COMPILE_STATUS, out int ok);
        if (ok == 0)
        {
            var sb = new StringBuilder(2048);
            _gl.glGetShaderInfoLog(sh, sb.Capacity, out int len, sb);
            throw new InvalidOperationException("GL compile error: " + sb.ToString(0, Math.Max(0, len)));
        }
        return sh;
    }

    private async Task DecodeLoopAsync(string filePath, CancellationToken ct)
    {
        try
        {
            // Ensure ffmpeg is found via PATH (user-provided) or configured elsewhere
            var info = await FFProbe.AnalyseAsync(filePath);
            _videoWidth = info.PrimaryVideoStream?.Width ?? 0;
            _videoHeight = info.PrimaryVideoStream?.Height ?? 0;
            if (_videoWidth <= 0 || _videoHeight <= 0) return;

            int frameSize = _videoWidth * _videoHeight * 4; // RGBA

            // Determine target FPS
            double fps = info.PrimaryVideoStream?.AvgFrameRate ?? 0.0;
            if (fps <= 1e-3) fps = 30.0;
            var frameDuration = TimeSpan.FromSeconds(1.0 / fps);

            using var sink = new RawFrameStream(frameSize, frameDuration, OnFrameReady);

            await FFMpegArguments
                .FromFileInput(filePath)
                .OutputToPipe(new FFMpegCore.Pipes.StreamPipeSink(sink), options => options
                    .WithVideoCodec("rawvideo")
                    .ForceFormat("rawvideo")
                    .WithCustomArgument("-pix_fmt rgba")
                    .WithCustomArgument("-an")) // no audio
                .ProcessAsynchronously();
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    private void OnFrameReady(ReadOnlyMemory<byte> frame)
    {
        lock (_frameLock)
        {
            _pendingFrame = frame.ToArray();
            _hasNewFrame = true;
        }
    }

    private sealed class RawFrameStream : Stream
    {
        private readonly int _frameSize;
        private readonly MemoryStream _buffer = new();
        private readonly Action<ReadOnlyMemory<byte>> _onFrame;
        private readonly TimeSpan _frameDuration;
        private DateTime _nextDue;

        public RawFrameStream(int frameSize, TimeSpan frameDuration, Action<ReadOnlyMemory<byte>> onFrame)
        {
            _frameSize = frameSize;
            _frameDuration = frameDuration;
            _onFrame = onFrame;
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
            _buffer.Write(buffer, offset, count);
            TryDrain();
        }
#if NET8_0_OR_GREATER
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _buffer.Write(buffer);
            TryDrain();
        }
#endif
        private void TryDrain()
        {
            while (_buffer.Length >= _frameSize)
            {
                _buffer.Position = 0;
                var frameBytes = ArrayPool<byte>.Shared.Rent(_frameSize);
                int read = _buffer.Read(frameBytes, 0, _frameSize);
                _onFrame(new ReadOnlyMemory<byte>(frameBytes, 0, read));

                // throttle to target frame duration
                if (_frameDuration > TimeSpan.Zero)
                {
                    var now = DateTime.UtcNow;
                    if (now < _nextDue)
                    {
                        var sleep = _nextDue - now;
                        if (sleep > TimeSpan.Zero)
                            Thread.Sleep(sleep);
                    }
                    _nextDue = _nextDue + _frameDuration;
                }

                // compact remaining bytes
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
}