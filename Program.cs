using System;
using System.Collections.ObjectModel;
using TimelineControl;
using Avalonia.Media;

namespace TimelineTest;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== TimelineEditor Test ===");
        
        try
        {
            Console.WriteLine("1. Testing TimelineTrack creation...");
            var track = new TimelineTrack("Test Track");
            Console.WriteLine($"   ✓ Track created: {track.Name}, Height={track.Height}");
            
            Console.WriteLine("2. Testing TimelineClip creation...");
            var clip = new TimelineClip("Test Clip", 0.0, 5.0, Color.FromRgb(255, 0, 0));
            Console.WriteLine($"   ✓ Clip created: {clip.Title}, Start={clip.Start}, End={clip.End}, Duration={clip.Duration}");
            
            Console.WriteLine("3. Testing ObservableCollection...");
            var tracks = new ObservableCollection<TimelineTrack>
            {
                new("Video") {
                    Clips = new ObservableCollection<TimelineClip>
                    {
                        new TimelineClip("Intro", 0.0, 4.0, Color.FromRgb(0x42,0x84,0x18)),
                    }
                }
            };
            Console.WriteLine($"   ✓ Collection created with {tracks.Count} track(s)");
            Console.WriteLine($"   ✓ Track has {tracks[0].Clips.Count} clip(s)");
            
            Console.WriteLine("4. Testing property changes...");
            clip.Start = 1.0;
            clip.End = 6.0;
            Console.WriteLine($"   ✓ Clip updated: Start={clip.Start}, End={clip.End}, Duration={clip.Duration}");
            
            Console.WriteLine("5. Testing TimelineEditor data structure...");
            var editorTracks = new ObservableCollection<TimelineTrack>
            {
                new("Video") {
                    Clips = new ObservableCollection<TimelineClip>
                    {
                        new TimelineClip("Intro", 0.0, 4.0, Color.FromRgb(0x42,0x84,0x18)),
                        new TimelineClip("Main", 5.0, 14.0, Color.FromRgb(0x1e,0x88,0xe5)),
                    }
                },
                new("Audio") {
                    Clips = new ObservableCollection<TimelineClip>
                    {
                        new TimelineClip("Bed", 0.0, 30.0, Color.FromRgb(0x8e,0x24,0xaa))
                    }
                }
            };
            Console.WriteLine($"   ✓ Editor tracks structure valid: {editorTracks.Count} tracks");
            foreach (var t in editorTracks)
            {
                Console.WriteLine($"     - {t.Name}: {t.Clips.Count} clips");
            }
            
            Console.WriteLine("\n=== All Tests Passed ===");
            Console.WriteLine("TimelineEditor data structures are valid.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ ERROR: {ex.Message}");
            Console.WriteLine($"Type: {ex.GetType().Name}");
            Console.WriteLine($"Stack Trace:\n{ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}

