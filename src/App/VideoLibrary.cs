using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FFMpegCore;

namespace Diffracta;

/// <summary>
/// Manages video files and their metadata for video synthesis.
/// Provides access to video information without loading frames into memory.
/// </summary>
public class VideoLibrary
{
    private readonly Dictionary<string, VideoMetadata> _videos = new();
    private readonly object _lock = new();
    
    /// <summary>
    /// Supported video file extensions
    /// </summary>
    private static readonly string[] VideoExtensions = 
    {
        ".mp4", ".mpeg", ".mpg", ".mov", ".avi", ".mkv", ".webm", 
        ".flv", ".wmv", ".m4v", ".3gp", ".ogv"
    };
    
    /// <summary>
    /// Gets all video metadata entries
    /// </summary>
    public IReadOnlyDictionary<string, VideoMetadata> Videos
    {
        get
        {
            lock (_lock)
            {
                return new Dictionary<string, VideoMetadata>(_videos);
            }
        }
    }
    
    /// <summary>
    /// Gets the number of videos in the library
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _videos.Count;
            }
        }
    }
    
    /// <summary>
    /// Checks if a file path is a video file based on extension
    /// </summary>
    public static bool IsVideoFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;
            
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return VideoExtensions.Contains(extension);
    }
    
    /// <summary>
    /// Scans a directory for video files and adds them to the library
    /// Returns the list of video files found
    /// </summary>
    public List<VideoMetadata> ScanDirectory(string directoryPath, Action<string>? logCallback = null)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            logCallback?.Invoke($"Directory does not exist: {directoryPath}");
            return new List<VideoMetadata>();
        }
        
        var foundVideos = new List<VideoMetadata>();
        
        try
        {
            logCallback?.Invoke($"Scanning directory: {directoryPath}");
            
            // Get all files in directory
            var files = Directory.GetFiles(directoryPath);
            var videoFiles = files.Where(IsVideoFile).ToList();
            
            logCallback?.Invoke($"Found {videoFiles.Count} video file(s) in directory");
            
            foreach (var filePath in videoFiles)
            {
                try
                {
                    // Check if already in library
                    lock (_lock)
                    {
                        if (_videos.ContainsKey(filePath))
                        {
                            logCallback?.Invoke($"  Skipping (already in library): {Path.GetFileName(filePath)}");
                            foundVideos.Add(_videos[filePath]);
                            continue;
                        }
                    }
                    
                    // Analyze video file
                    var info = FFProbe.AnalyseAsync(filePath).GetAwaiter().GetResult();
                    var videoStream = info.PrimaryVideoStream;
                    
                    if (videoStream == null)
                    {
                        logCallback?.Invoke($"  Skipping (no video stream): {Path.GetFileName(filePath)}");
                        continue;
                    }
                    
                    // Create metadata
                    var metadata = new VideoMetadata
                    {
                        FilePath = filePath,
                        FileName = Path.GetFileName(filePath),
                        Width = videoStream.Width,
                        Height = videoStream.Height,
                        FrameRate = videoStream.AvgFrameRate,
                        Duration = info.Duration,
                        Codec = videoStream.CodecName ?? "unknown",
                        FrameCount = (int)((info.Duration.TotalSeconds * videoStream.AvgFrameRate) + 0.5)
                    };
                    
                    // Add to library
                    lock (_lock)
                    {
                        _videos[filePath] = metadata;
                    }
                    
                    foundVideos.Add(metadata);
                    
                    logCallback?.Invoke($"  Added: {metadata.FileName} ({metadata.Width}x{metadata.Height}, {metadata.FrameRate} fps, {metadata.FrameCount} frames)");
                }
                catch (Exception ex)
                {
                    logCallback?.Invoke($"  Error processing {Path.GetFileName(filePath)}: {ex.Message}");
                }
            }
            
            logCallback?.Invoke($"Scan complete. Total videos in library: {Count}");
        }
        catch (Exception ex)
        {
            logCallback?.Invoke($"Error scanning directory: {ex.Message}");
        }
        
        return foundVideos;
    }
    
    /// <summary>
    /// Gets metadata for a video file
    /// </summary>
    public VideoMetadata? GetVideo(string filePath)
    {
        lock (_lock)
        {
            return _videos.TryGetValue(filePath, out var metadata) ? metadata : null;
        }
    }
    
    /// <summary>
    /// Gets all videos from a specific directory
    /// </summary>
    public List<VideoMetadata> GetVideosFromDirectory(string directoryPath)
    {
        lock (_lock)
        {
            return _videos.Values
                .Where(v => v.FilePath.StartsWith(directoryPath, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
    
    /// <summary>
    /// Removes a video from the library
    /// </summary>
    public bool RemoveVideo(string filePath)
    {
        lock (_lock)
        {
            return _videos.Remove(filePath);
        }
    }
    
    /// <summary>
    /// Clears all videos from the library
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _videos.Clear();
        }
    }
}

/// <summary>
/// Metadata for a video file
/// </summary>
public class VideoMetadata
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public double FrameRate { get; set; }
    public TimeSpan Duration { get; set; }
    public string Codec { get; set; } = string.Empty;
    public int FrameCount { get; set; }
}

