using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;




namespace TimelineControl
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Public host UserControl (expose bindable properties; auto-seeds demo data)
    // ─────────────────────────────────────────────────────────────────────────────
    public partial class Utils_TimelineEditor : UserControl
    {
        public static readonly StyledProperty<double> ZoomProperty =
            AvaloniaProperty.Register<Utils_TimelineEditor, double>(nameof(Zoom), 120d);

        public static readonly StyledProperty<double> CursorTimeProperty =
            AvaloniaProperty.Register<Utils_TimelineEditor, double>(nameof(CursorTime), 0d);

        public static readonly StyledProperty<TimeSpan> DurationProperty =
            AvaloniaProperty.Register<Utils_TimelineEditor, TimeSpan>(nameof(Duration), TimeSpan.FromSeconds(30));

        public static readonly StyledProperty<bool> SnapEnabledProperty =
            AvaloniaProperty.Register<Utils_TimelineEditor, bool>(nameof(SnapEnabled), true);

        public static readonly StyledProperty<ObservableCollection<TimelineTrack>> TracksProperty =
            AvaloniaProperty.Register<Utils_TimelineEditor, ObservableCollection<TimelineTrack>>(nameof(Tracks));

        public double Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
        public double CursorTime { get => GetValue(CursorTimeProperty); set => SetValue(CursorTimeProperty, value); }
        public TimeSpan Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
        public bool SnapEnabled { get => GetValue(SnapEnabledProperty); set => SetValue(SnapEnabledProperty, value); }
        public ObservableCollection<TimelineTrack> Tracks { get => GetValue(TracksProperty); set => SetValue(TracksProperty, value); }

        private int _trackCounter = 0; // Global variable to track track numbers

        public Utils_TimelineEditor()
        {
            // Set default values BEFORE InitializeComponent to ensure bindings work
            SetValue(ZoomProperty, 120d);
            SetValue(CursorTimeProperty, 0d);
            SetValue(DurationProperty, TimeSpan.FromSeconds(30));
            SetValue(SnapEnabledProperty, true);
            
            // Initialize Tracks as empty collection BEFORE InitializeComponent
            Tracks = new ObservableCollection<TimelineTrack>();
            
            // Now initialize XAML - properties are already set
            InitializeComponent();
            
            // Setup scroll synchronization between main canvas and track heads
            SetupScrollSync();
        }

        private void SetupScrollSync()
        {
            try
            {
                var mainScroller = this.FindControl<ScrollViewer>("PART_Scroller");
                var trackHeadScroller = this.FindControl<ScrollViewer>("PART_TrackHeadScroller");
                
                if (mainScroller != null && trackHeadScroller != null)
                {
                    // Sync main canvas scroll to track head scroll
                    mainScroller.ScrollChanged += (sender, e) =>
                    {
                        if (trackHeadScroller != null)
                        {
                            trackHeadScroller.Offset = new Vector(trackHeadScroller.Offset.X, mainScroller.Offset.Y);
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetupScrollSync error: {ex.Message}");
            }
        }

        private void OnResetCursorClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            CursorTime = 0;
        }

        private void OnFitZoomClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                // Find the maximum end time across all clips in all tracks
                double maxTime = Duration.TotalSeconds; // Start with duration as fallback
                
                if (Tracks != null && Tracks.Count > 0)
                {
                    foreach (var track in Tracks)
                    {
                        if (track?.Clips != null)
                        {
                            foreach (var clip in track.Clips)
                            {
                                if (clip != null && clip.End > maxTime)
                                {
                                    maxTime = clip.End;
                                }
                            }
                        }
                    }
                }

                // Get the ScrollViewer to determine available width
                var scroller = this.FindControl<ScrollViewer>("PART_Scroller");
                if (scroller != null && scroller.Bounds.Width > 0 && maxTime > 0)
                {
                    // Calculate zoom to fit: available width / max time
                    // Add some padding (use 95% of width for padding)
                    double availableWidth = scroller.Bounds.Width * 0.95;
                    double calculatedZoom = availableWidth / maxTime;
                    
                    // Clamp to valid zoom range
                    Zoom = Math.Clamp(calculatedZoom, 20, 400);
                }
                else if (maxTime > 0)
                {
                    // Fallback: use a reasonable default width (e.g., 600px)
                    double calculatedZoom = 600.0 / maxTime;
                    Zoom = Math.Clamp(calculatedZoom, 20, 400);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnFitZoomClick error: {ex.Message}");
            }
        }

        private void OnAddTrackClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                _trackCounter++;
                string trackName = _trackCounter == 1 ? "Video" : $"Video {_trackCounter}";
                
                var newTrack = new TimelineTrack(trackName)
                {
                    Clips = new ObservableCollection<TimelineClip>()
                };
                
                if (Tracks == null)
                {
                    Tracks = new ObservableCollection<TimelineTrack>();
                }
                
                Tracks.Add(newTrack);
                
                // Force immediate visual update by finding and invalidating the canvas and track head
                var canvas = this.FindControl<TimelineCanvas>("PART_Canvas");
                if (canvas != null)
                {
                    canvas.InvalidateMeasure();
                    canvas.InvalidateVisual();
                }
                
                var trackHead = this.FindControl<TimelineTrackHead>("PART_TrackHead");
                if (trackHead != null)
                {
                    // Ensure the track head's Tracks property is set (in case binding hasn't updated yet)
                    if (trackHead.Tracks != Tracks)
                    {
                        trackHead.Tracks = Tracks;
                    }
                    
                    // Force update of track heads - the CollectionChanged should handle this,
                    // but we'll also ensure it happens by invalidating layout
                    trackHead.InvalidateMeasure();
                    trackHead.InvalidateArrange();
                    trackHead.InvalidateVisual();
                    
                    System.Diagnostics.Debug.WriteLine($"OnAddTrackClick: Added track '{trackName}', TrackHead.Tracks count: {trackHead.Tracks?.Count ?? 0}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("OnAddTrackClick: PART_TrackHead not found!");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnAddTrackClick error: {ex.Message}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Data model
    // ─────────────────────────────────────────────────────────────────────────────
    public sealed class TimelineTrack : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _name = "";
        public string Name 
        { 
            get => _name; 
            set 
            { 
                if (_name != value)
                {
                    _name = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            } 
        }
        public double Height { get; set; } = 56;
        public ObservableCollection<TimelineClip> Clips { get; set; } = new();

        public TimelineTrack(string name) => Name = name;
    }

    public sealed class TimelineClip : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string Title { get; set; }
        public double Start { get => _start; set { if (Math.Abs(_start - value) > 1e-6) { _start = value; PropertyChanged?.Invoke(this, new(nameof(Start))); } } }
        public double End { get => _end; set { if (Math.Abs(_end - value) > 1e-6) { _end = value; PropertyChanged?.Invoke(this, new(nameof(End))); } } }
        public bool IsSelected { get => _selected; set { if (_selected != value) { _selected = value; PropertyChanged?.Invoke(this, new(nameof(IsSelected))); } } }
        public IBrush Fill { get; }
        public IBrush FillSelected { get; }
        public IPen Border { get; }
        public IPen BorderSelected { get; }

        private double _start;
        private double _end;
        private bool _selected;

        public double Duration => Math.Max(0, End - Start);

        public TimelineClip(string title, double start, double end, Color color)
        {
            Title = title;
            _start = start;
            _end = end;

            var baseBrush = new SolidColorBrush(color);
            Fill = baseBrush;
            FillSelected = new SolidColorBrush(Color.FromArgb(255, (byte)Math.Min(255, color.R + 30), (byte)Math.Min(255, color.G + 30), (byte)Math.Min(255, color.B + 30)));
            Border = new Pen(new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)), 1);
            BorderSelected = new Pen(new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)), 1.25);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Ruler control (ticks + time cursor)
    // ─────────────────────────────────────────────────────────────────────────────
    public sealed class TimelineRuler : Control
    {
        public static readonly StyledProperty<double> ZoomProperty =
            AvaloniaProperty.Register<TimelineRuler, double>(nameof(Zoom), 120d);

        public static readonly StyledProperty<TimeSpan> DurationProperty =
            AvaloniaProperty.Register<TimelineRuler, TimeSpan>(nameof(Duration), TimeSpan.FromSeconds(30));

        public static readonly StyledProperty<double> CursorTimeProperty =
            AvaloniaProperty.Register<TimelineRuler, double>(nameof(CursorTime), 0d);

        public static readonly StyledProperty<bool> ShowClipsProperty =
            AvaloniaProperty.Register<TimelineRuler, bool>(nameof(ShowClips), false);

        public static readonly StyledProperty<ObservableCollection<TimelineTrack>?> TracksProperty =
            AvaloniaProperty.Register<TimelineRuler, ObservableCollection<TimelineTrack>?>(nameof(Tracks), null);

        public TimelineRuler()
        {
            AffectsRender<TimelineRuler>(ZoomProperty, DurationProperty, CursorTimeProperty, ShowClipsProperty, TracksProperty);
        }

        public double Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
        public TimeSpan Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
        public double CursorTime { get => GetValue(CursorTimeProperty); set => SetValue(CursorTimeProperty, value); }
        public bool ShowClips { get => GetValue(ShowClipsProperty); set => SetValue(ShowClipsProperty, value); }
        public ObservableCollection<TimelineTrack>? Tracks { get => GetValue(TracksProperty); set => SetValue(TracksProperty, value); }

        public override void Render(DrawingContext ctx)
        {
            try
            {
                var bounds = Bounds;
                if (bounds.Width <= 0 || bounds.Height <= 0) return;
                
                var width = Math.Max(bounds.Width, Duration.TotalSeconds * Zoom);
                var bg = Brushes.Transparent;
                ctx.FillRectangle(bg, new Rect(0, 0, width, bounds.Height));

                // Draw clips overview bar if ShowClips is enabled
                if (ShowClips && Tracks != null && Tracks.Count > 0)
                {
                    var clipBarHeight = Math.Min(10, bounds.Height - 4); // Leave 2px margin top/bottom
                    var clipBarY = 2; // Start from top with small margin
                    
                    // Draw background for clips bar
                    var barBg = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                    ctx.FillRectangle(barBg, new Rect(0, 0, width, clipBarHeight + 4));
                    
                    foreach (var track in Tracks)
                    {
                        if (track?.Clips == null) continue;
                        foreach (var clip in track.Clips)
                        {
                            if (clip == null) continue;
                            var rx = clip.Start * Zoom;
                            var rw = Math.Max(2, clip.Duration * Zoom); // Minimum 2px width
                            
                            if (rw > 0 && !double.IsNaN(rx) && rx < width)
                            {
                                var clipRect = new Rect(rx, clipBarY, rw, clipBarHeight);
                                ctx.FillRectangle(clip.Fill, clipRect);
                                // Add subtle border
                                ctx.DrawRectangle(new Pen(new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)), 0.5), clipRect);
                            }
                        }
                    }
                }

                var tickPen = new Pen(new SolidColorBrush(Color.FromRgb(60, 60, 60)), 1);
                var labelBrush = Brushes.Gainsboro;

                double step = ChooseTickStep(Zoom);
                for (double s = 0; s <= Duration.TotalSeconds + 0.0001; s += step)
                {
                    double x = s * Zoom + 0.5; // align pixel
                    double h = (Math.Abs(s % (step * 5)) < 1e-6) ? bounds.Height : bounds.Height * 0.6;
                    ctx.DrawLine(tickPen, new Point(x, bounds.Height), new Point(x, bounds.Height - h));

                    if (Math.Abs(s % (step * 5)) < 1e-6 && !ShowClips) // Only show labels if not showing clips
                    {
                        try
                        {
                            var text = new FormattedText(
                                s.ToString(CultureInfo.InvariantCulture),
                                CultureInfo.InvariantCulture,
                                FlowDirection.LeftToRight,
                                new Typeface("Segoe UI"),
                                11,
                                labelBrush);
                            ctx.DrawText(text, new Point(x + 3, 2));
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"TimelineRuler Render text error: {ex.Message}");
                        }
                    }
                }

                // Cursor - draw only if CursorTime is valid and within bounds
                var cursorX = CursorTime * Zoom + 0.5;
                if (!double.IsNaN(cursorX) && !double.IsInfinity(cursorX) && cursorX >= 0 && cursorX <= width)
                {
                    ctx.DrawLine(new Pen(Brushes.Orange, 1.5), new Point(cursorX, 0), new Point(cursorX, bounds.Height));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TimelineRuler Render error: {ex.Message}\n{ex.StackTrace}");
                Console.WriteLine($"TimelineRuler Render error: {ex.Message}");
            }
        }

        private static double ChooseTickStep(double pxPerSec)
        {
            // Choose from whole second steps only (1 bar = 1 second)
            // Select step based on zoom level to avoid dense labels
            var candidates = new[] { 1d, 2d, 5d, 10d, 15d, 30d, 60d };
            foreach (var c in candidates)
            {
                if (c * pxPerSec >= 40) return c;
            }
            return 60d; // fallback to 1 minute intervals
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(Math.Max(availableSize.Width, Duration.TotalSeconds * Zoom), 24);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Main drawing + interaction surface
    // ─────────────────────────────────────────────────────────────────────────────
    public sealed class TimelineCanvas : Control
    {
        public static readonly StyledProperty<double> ZoomProperty =
            AvaloniaProperty.Register<TimelineCanvas, double>(nameof(Zoom), 120d);

        public static readonly StyledProperty<TimeSpan> DurationProperty =
            AvaloniaProperty.Register<TimelineCanvas, TimeSpan>(nameof(Duration), TimeSpan.FromSeconds(30));

        public static readonly StyledProperty<double> CursorTimeProperty =
            AvaloniaProperty.Register<TimelineCanvas, double>(nameof(CursorTime), 0d);

        public static readonly StyledProperty<bool> SnapEnabledProperty =
            AvaloniaProperty.Register<TimelineCanvas, bool>(nameof(SnapEnabled), true);

        public static readonly StyledProperty<ObservableCollection<TimelineTrack>?> TracksProperty =
            AvaloniaProperty.Register<TimelineCanvas, ObservableCollection<TimelineTrack>?>(nameof(Tracks), null);

        public double Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
        public TimeSpan Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
        public double CursorTime { get => GetValue(CursorTimeProperty); set => SetValue(CursorTimeProperty, value); }
        public bool SnapEnabled { get => GetValue(SnapEnabledProperty); set => SetValue(SnapEnabledProperty, value); }
        public ObservableCollection<TimelineTrack>? Tracks 
        { 
            get => GetValue(TracksProperty); 
            set 
            { 
                var oldValue = GetValue(TracksProperty);
                
                // Only update if value actually changed
                if (ReferenceEquals(oldValue, value)) return;
                
                // Unsubscribe from old collection
                if (oldValue != null)
                    oldValue.CollectionChanged -= OnTracksCollectionChanged;
                
                // Set new value (this will trigger AffectsRender automatically)
                SetValue(TracksProperty, value);
                
                // Subscribe to new collection
                if (value != null)
                {
                    value.CollectionChanged += OnTracksCollectionChanged;
                }
            } 
        }

        private void OnTracksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Invalidate when collection changes - AffectsRender will handle it safely
            try
            {
                InvalidateMeasure();
                InvalidateVisual();
            }
            catch
            {
                // Ignore if control isn't ready yet
            }
        }

        private readonly Typeface _typeface = new("Segoe UI");
        private readonly IBrush _laneBrush = new SolidColorBrush(Color.FromRgb(26, 26, 26));
        private readonly IBrush _laneAltBrush = new SolidColorBrush(Color.FromRgb(20, 20, 20));
        private readonly Pen _gridPen = new(new SolidColorBrush(Color.FromRgb(45, 45, 45)), 1);
        private readonly Pen _cursorPen = new(Brushes.Orange, 1.5);
        private readonly Pen _trackBorderPen = new(Brushes.LightGray, 1);

        private TimelineClip? _activeClip;
        private HitKind _hitKind = HitKind.None;
        private Point _dragStart;
        private (double start, double end) _originalBounds;
        private bool _isDraggingCursor = false; // Track if we're dragging to scrub cursor

        private enum HitKind { None, Body, LeftEdge, RightEdge }

        public TimelineCanvas()
        {
            try
            {
                Focusable = true;
                // AffectsRender will handle invalidation automatically when properties change
                AffectsRender<TimelineCanvas>(ZoomProperty, DurationProperty, TracksProperty, CursorTimeProperty);
                AffectsMeasure<TimelineCanvas>(ZoomProperty, DurationProperty, TracksProperty);
                
                PointerPressed += OnPointerPressed;
                PointerReleased += OnPointerReleased;
                PointerMoved += OnPointerMoved;
                PointerWheelChanged += OnWheel;
                DoubleTapped += (_, e) =>
                {
                    try
                    {
                        // double-click to set cursor at position
                        var p = e.GetPosition(this);
                        CursorTime = Math.Clamp(p.X / Zoom, 0, Duration.TotalSeconds);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"TimelineCanvas DoubleTapped error: {ex.Message}");
                    }
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TimelineCanvas Constructor error: {ex.Message}\n{ex.StackTrace}");
                Console.WriteLine($"TimelineCanvas Constructor error: {ex.Message}");
            }
        }

        // ── Layout ───────────────────────────────────────────────────────────────
        protected override Size MeasureOverride(Size availableSize)
        {
            try
            {
                // Prevent infinite loops with invalid values
                if (double.IsNaN(availableSize.Width) || double.IsInfinity(availableSize.Width))
                    availableSize = availableSize.WithWidth(1000);
                if (double.IsNaN(availableSize.Height) || double.IsInfinity(availableSize.Height))
                    availableSize = availableSize.WithHeight(500);
                
                double width = Duration.TotalSeconds * Zoom;
                if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
                    width = 1000;
                
                double height = 0;
                if (Tracks != null && Tracks.Count > 0)
                {
                    // Sum of all track heights (no margins)
                    height = Tracks.Sum(t => t?.Height ?? 56);
                }
                if (height <= 0)
                    height = 200; // Default height
                
                return new Size(Math.Max(availableSize.Width, width), Math.Max(availableSize.Height, height));
            }
            catch
            {
                // Return safe default size if measurement fails
                return new Size(1000, 200);
            }
        }

        // ── Rendering ────────────────────────────────────────────────────────────
        public override void Render(DrawingContext ctx)
        {
            try
            {
                var bounds = Bounds;
                if (bounds.Width <= 0 || bounds.Height <= 0 || double.IsNaN(bounds.Width) || double.IsNaN(bounds.Height))
                    return;
                
                var width = Math.Max(bounds.Width, Duration.TotalSeconds * Zoom);
                if (width <= 0 || double.IsNaN(width) || double.IsInfinity(width))
                    return;

                // background
                var bg = Brushes.Transparent;
                ctx.FillRectangle(bg, new Rect(0, 0, width, bounds.Height));

                // vertical second grid - draw lines at every second (1 second intervals)
                const double gridStep = 1.0; // 1 second per bar
                for (double s = 0; s <= Duration.TotalSeconds + 0.0001; s += gridStep)
                {
                    var x = s * Zoom + 0.5;
                    if (x >= 0 && x <= width)
                        ctx.DrawLine(_gridPen, new Point(x, 0), new Point(x, bounds.Height));
                }

                // tracks
                const double trackMargin = 0.0; // No margin between tracks
                double y = 0; // Start at top with no margin
                if (Tracks != null && Tracks.Count > 0)
                {
                    for (int i = 0; i < Tracks.Count; i++)
                    {
                        var track = Tracks[i];
                        if (track == null || track.Height <= 0) continue;
                        
                        RenderTrack(ctx, track, i, y, width);
                        y += track.Height + trackMargin; // No margin between tracks
                    }
                }

                // cursor - draw only if CursorTime is valid and within bounds
                var cursorX = CursorTime * Zoom + 0.5;
                if (!double.IsNaN(cursorX) && !double.IsInfinity(cursorX) && cursorX >= 0 && cursorX <= width)
                    ctx.DrawLine(_cursorPen, new Point(cursorX, 0), new Point(cursorX, bounds.Height));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TimelineCanvas Render error: {ex.Message}");
                // Don't re-throw in Render - just log it
            }
        }

        // ── Track Rendering ────────────────────────────────────────────────────────
        private void RenderTrack(DrawingContext ctx, TimelineTrack track, int trackIndex, double y, double width)
        {
            if (track == null || track.Height <= 0) return;

            try
            {
                // Track extends 2px more to the bottom (same top position, extends downward)
                var renderHeight = track.Height;
                
                // Draw track background and border
                var laneRect = new Rect(0, y, width, renderHeight);
                ctx.FillRectangle(trackIndex % 2 == 0 ? _laneBrush : _laneAltBrush, laneRect);
                ctx.DrawRectangle(_trackBorderPen, laneRect);

                // Draw clips in this track
                if (track.Clips != null && track.Clips.Count > 0)
                {
                    foreach (var clip in track.Clips)
                    {
                        RenderClip(ctx, clip, y, renderHeight);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TimelineCanvas RenderTrack error: {ex.Message}");
            }
        }

        // ── Clip Rendering ─────────────────────────────────────────────────────────
        private void RenderClip(DrawingContext ctx, TimelineClip clip, double trackY, double trackHeight)
        {
            if (clip == null) return;

            try
            {
                var rx = clip.Start * Zoom;
                var rw = Math.Max(1, clip.Duration * Zoom);
                var ry = trackY + 20;
                var rh = Math.Max(18, trackHeight - 26);

                if (rw > 0 && rh > 0 && !double.IsNaN(rx) && !double.IsNaN(ry))
                {
                    var rect = new Rect(rx, ry, rw, rh);
                    
                    // Draw clip body
                    ctx.FillRectangle(clip.IsSelected ? clip.FillSelected : clip.Fill, rect);
                    ctx.DrawRectangle(clip.IsSelected ? clip.BorderSelected : clip.Border, rect);

                    // Draw resize handles (bar zones)
                    var left = new Rect(rx - 3, ry, 6, rh);
                    var right = new Rect(rx + rw - 3, ry, 6, rh);
                    ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), left);
                    ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), right);

                    // Draw clip title
                    try
                    {
                        var title = new FormattedText(
                            clip.Title ?? "",
                            CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight,
                            _typeface,
                            11,
                            Brushes.White);
                        ctx.DrawText(title, new Point(rx + 6, ry + 2));
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TimelineCanvas RenderClip error: {ex.Message}");
            }
        }

        // Removed ChooseMajorStep - no longer needed as we use 1-second intervals

        // ── Interaction handlers ────────────────────────────────────────────────────
        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var p = e.GetPosition(this);
            var hit = HitTest(p);
            
            if (hit.clip != null)
            {
                _activeClip = hit.clip;
                _hitKind = hit.kind;
                _dragStart = p;
                _originalBounds = (hit.clip.Start, hit.clip.End);
                _isDraggingCursor = false; // Not dragging cursor when clip is selected
                
                // Select clip
                foreach (var track in Tracks ?? Enumerable.Empty<TimelineTrack>())
                    foreach (var clip in track.Clips)
                        clip.IsSelected = clip == hit.clip;
                
                e.Pointer.Capture(this);
                e.Handled = true;
            }
            else
            {
                // Click on empty space - start dragging cursor
                _isDraggingCursor = true;
                _activeClip = null;
                _hitKind = HitKind.None;
                CursorTime = Math.Clamp(p.X / Zoom, 0, Duration.TotalSeconds);
                e.Pointer.Capture(this);
                e.Handled = true;
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            // Fast path: If dragging cursor, update cursor position immediately
            // We have pointer capture, so if _isDraggingCursor is true, we're definitely dragging
            if (_isDraggingCursor)
            {
                var p = e.GetPosition(this);
                CursorTime = Math.Clamp(p.X / Zoom, 0, Duration.TotalSeconds);
                return;
            }
            
            // Handle clip dragging
            if (_activeClip == null || _hitKind == HitKind.None) return;
            
            var p2 = e.GetPosition(this);
            var deltaX = p2.X - _dragStart.X;
            var deltaTime = deltaX / Zoom;
            
            if (_hitKind == HitKind.Body)
            {
                // Move entire clip
                var newStart = SnapEnabled ? SnapToGrid(_originalBounds.start + deltaTime) : _originalBounds.start + deltaTime;
                var duration = _originalBounds.end - _originalBounds.start;
                _activeClip.Start = Math.Max(0, newStart);
                _activeClip.End = Math.Min(Duration.TotalSeconds, _activeClip.Start + duration);
            }
            else if (_hitKind == HitKind.LeftEdge)
            {
                // Resize left edge
                var newStart = SnapEnabled ? SnapToGrid(_originalBounds.start + deltaTime) : _originalBounds.start + deltaTime;
                _activeClip.Start = Math.Max(0, Math.Min(newStart, _activeClip.End - 0.1));
            }
            else if (_hitKind == HitKind.RightEdge)
            {
                // Resize right edge
                var newEnd = SnapEnabled ? SnapToGrid(_originalBounds.end + deltaTime) : _originalBounds.end + deltaTime;
                _activeClip.End = Math.Min(Duration.TotalSeconds, Math.Max(newEnd, _activeClip.Start + 0.1));
            }
            
            InvalidateVisual();
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_activeClip != null)
            {
                _activeClip = null;
                _hitKind = HitKind.None;
                e.Pointer.Capture(null);
                e.Handled = true;
            }
            
            // Clear cursor dragging state
            if (_isDraggingCursor)
            {
                _isDraggingCursor = false;
                e.Pointer.Capture(null);
                e.Handled = true;
            }
        }

        private void OnWheel(object? sender, PointerWheelEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                // Zoom with Ctrl+Wheel
                var delta = e.Delta.Y > 0 ? 1.1 : 0.9;
                Zoom = Math.Clamp(Zoom * delta, 20, 400);
                e.Handled = true;
            }
        }

        private (TimelineClip? clip, HitKind kind) HitTest(Point p)
        {
            if (Tracks == null) return (null, HitKind.None);
            
            const double trackMargin = 0.0; // No margin, matching rendering
            double y = 0; // Start at top with no margin, matching rendering
            foreach (var track in Tracks)
            {
                if (p.Y >= y && p.Y < y + track.Height)
                {
                    var clipY = y + 20;
                    var clipH = Math.Max(18, track.Height - 26);
                    
                    foreach (var clip in track.Clips)
                    {
                        if (p.Y >= clipY && p.Y < clipY + clipH)
                        {
                            var rx = clip.Start * Zoom;
                            var rw = Math.Max(1, clip.Duration * Zoom);
                            
                            if (p.X >= rx - 3 && p.X < rx + 3)
                                return (clip, HitKind.LeftEdge);
                            if (p.X >= rx + rw - 3 && p.X < rx + rw + 3)
                                return (clip, HitKind.RightEdge);
                            if (p.X >= rx && p.X < rx + rw)
                                return (clip, HitKind.Body);
                        }
                    }
                }
                y += track.Height + trackMargin; // No margin between tracks
            }
            
            return (null, HitKind.None);
        }

        private double SnapToGrid(double time)
        {
            if (!SnapEnabled) return time;
            // Snap to whole seconds (1 second intervals)
            const double snapStep = 1.0; // 1 second
            return Math.Round(time / snapStep) * snapStep;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Track Head Control (left sidebar)
    // ─────────────────────────────────────────────────────────────────────────────
    public sealed class TimelineTrackHead : Panel
    {
        public static readonly StyledProperty<ObservableCollection<TimelineTrack>?> TracksProperty =
            AvaloniaProperty.Register<TimelineTrackHead, ObservableCollection<TimelineTrack>?>(
                nameof(Tracks), 
                null,
                coerce: CoerceTracks);

        private static ObservableCollection<TimelineTrack>? CoerceTracks(AvaloniaObject obj, ObservableCollection<TimelineTrack>? value)
        {
            if (obj is TimelineTrackHead trackHead)
            {
                // Get the old value BEFORE the property is set
                var oldValue = trackHead.GetValue(TracksProperty);
                
                // Only update subscription if the collection reference actually changed
                if (!ReferenceEquals(oldValue, value))
                {
                    System.Diagnostics.Debug.WriteLine($"CoerceTracks: Collection reference changed. Old: {(oldValue != null ? "not null" : "null")}, New: {(value != null ? "not null" : "null")}");
                    
                    // Unsubscribe from old collection
                    if (oldValue != null)
                    {
                        oldValue.CollectionChanged -= trackHead.OnTracksCollectionChanged;
                        System.Diagnostics.Debug.WriteLine("CoerceTracks: Unsubscribed from old collection");
                    }
                    
                    // Subscribe to new collection
                    if (value != null)
                    {
                        value.CollectionChanged += trackHead.OnTracksCollectionChanged;
                        System.Diagnostics.Debug.WriteLine($"CoerceTracks: Subscribed to new collection with {value.Count} tracks");
                    }
                }
            }
            
            return value;
        }

        public ObservableCollection<TimelineTrack>? Tracks 
        { 
            get => GetValue(TracksProperty); 
            set => SetValue(TracksProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            
            if (change.Property == TracksProperty)
            {
                // Update track heads when property changes (via binding or direct set)
                UpdateTrackHeads();
                InvalidateMeasure();
                InvalidateArrange();
            }
        }

        private void OnTracksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"OnTracksCollectionChanged: Action={e.Action}, Tracks count: {Tracks?.Count ?? 0}");
                UpdateTrackHeads();
                InvalidateMeasure();
                InvalidateArrange();
                InvalidateVisual();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnTracksCollectionChanged error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private readonly IBrush _laneBrush = new SolidColorBrush(Color.FromRgb(26, 26, 26));
        private readonly IBrush _laneAltBrush = new SolidColorBrush(Color.FromRgb(20, 20, 20));
        private readonly Pen _trackBorderPen = new(Brushes.LightGray, 1);
        private readonly Dictionary<TimelineTrack, Border> _trackHeadControls = new();

        public TimelineTrackHead()
        {
            Background = new SolidColorBrush(Color.FromRgb(59, 59, 59)); // #3B3B3B
            AffectsMeasure<TimelineTrackHead>(TracksProperty);
            
            // Ensure track heads are updated when control is loaded
            Loaded += (s, e) =>
            {
                // Ensure CollectionChanged subscription is set up
                if (Tracks != null)
                {
                    try
                    {
                        // Ensure subscription is set up (coerce should handle this, but ensure it)
                        Tracks.CollectionChanged -= OnTracksCollectionChanged; // Remove if exists
                        Tracks.CollectionChanged += OnTracksCollectionChanged; // Add subscription
                    }
                    catch { }
                    
                    UpdateTrackHeads();
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            };
        }

        private void UpdateTrackHeads()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"UpdateTrackHeads called, Tracks count: {Tracks?.Count ?? 0}");
                
                // Remove controls for tracks that no longer exist
                var tracksToRemove = _trackHeadControls.Keys.Where(t => Tracks == null || !Tracks.Contains(t)).ToList();
                foreach (var track in tracksToRemove)
                {
                    if (_trackHeadControls.TryGetValue(track, out var border))
                    {
                        Children.Remove(border);
                        _trackHeadControls.Remove(track);
                    }
                }

                if (Tracks == null) return;

                // Add or update controls for existing tracks
                for (int i = 0; i < Tracks.Count; i++)
                {
                    var track = Tracks[i];
                    if (track == null) continue;

                    if (!_trackHeadControls.ContainsKey(track))
                    {
                        System.Diagnostics.Debug.WriteLine($"Creating track head for track: {track.Name}, index: {i}");
                        
                        // Create new track head control
                        var border = new Border
                        {
                            Background = i % 2 == 0 ? _laneBrush : _laneAltBrush,
                            BorderBrush = Brushes.LightGray,
                            BorderThickness = new Thickness(0.5), // 0.5px to render as 1px
                            Child = new TextBox
                            {
                                Text = track.Name ?? "",
                                Background = Brushes.Transparent,
                                BorderThickness = new Thickness(1),
                                BorderBrush = Brushes.LightGray,
                                CornerRadius = new CornerRadius(0),
                                Foreground = Brushes.Gray,
                                FontSize = 12,
                                Padding = new Thickness(0),
                                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Top,
                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left
                            }
                        };

                        // Bind TextBox text to track name
                        var textBox = (TextBox)border.Child;
                        textBox.TextChanged += (s, e) =>
                        {
                            if (textBox.Text != null)
                            {
                                track.Name = textBox.Text;
                            }
                        };

                        // Subscribe to track name changes
                        track.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == "Name" && textBox.Text != track.Name)
                            {
                                textBox.Text = track.Name ?? "";
                            }
                        };

                        // Ensure the border is visible and properly sized
                        border.IsVisible = true;
                        border.Width = double.NaN; // Let it fill available width
                        border.Height = track.Height; // Same height as tracks
                        
                        Children.Add(border);
                        _trackHeadControls[track] = border;
                        
                        // Force measure and arrange of the new control
                        border.Measure(new Size(double.PositiveInfinity, track.Height));
                    }
                    else
                    {
                        // Update background color for existing tracks (in case indices changed)
                        if (_trackHeadControls.TryGetValue(track, out var border))
                        {
                            border.Background = i % 2 == 0 ? _laneBrush : _laneAltBrush;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateTrackHeads error: {ex.Message}");
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            try
            {
                if (double.IsNaN(availableSize.Width) || double.IsInfinity(availableSize.Width))
                    availableSize = availableSize.WithWidth(160);
                if (double.IsNaN(availableSize.Height) || double.IsInfinity(availableSize.Height))
                    availableSize = availableSize.WithHeight(500);
                
                // Measure all child controls
                foreach (var child in Children)
                {
                    if (child != null)
                    {
                        child.Measure(availableSize);
                    }
                }
                
                double height = 0;
                if (Tracks != null && Tracks.Count > 0)
                {
                    // Sum of all track heights (no margins, same height as tracks)
                    height = Tracks.Sum(t => t?.Height ?? 56);
                }
                if (height <= 0)
                    height = 200;
                
                return new Size(Math.Max(availableSize.Width, 160), Math.Max(availableSize.Height, height));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TimelineTrackHead MeasureOverride error: {ex.Message}");
                return new Size(160, 200);
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            try
            {
                const double trackMargin = 0.0; // No margin between tracks
                double y = 0; // Start at top with no margin
                
                if (Tracks != null && Tracks.Count > 0)
                {
                    for (int i = 0; i < Tracks.Count; i++)
                    {
                        var track = Tracks[i];
                        if (track == null || track.Height <= 0) continue;
                        
                        if (_trackHeadControls.TryGetValue(track, out var border))
                        {
                            // Update background color based on index
                            border.Background = i % 2 == 0 ? _laneBrush : _laneAltBrush;
                            
                            // Arrange the border (same height as track)
                            border.Arrange(new Rect(0, y, finalSize.Width, track.Height));
                            
                            // Set TextBox width to half of track head width and height based on font size
                            if (border.Child is TextBox textBox)
                            {
                                textBox.Width = finalSize.Width * 0.5;
                                // Height based on font size (12) + padding for comfortable text display
                                textBox.Height = textBox.FontSize + 8;
                            }
                        }
                        
                        y += track.Height + trackMargin; // No margin between tracks
                    }
                }
                
                return finalSize;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TimelineTrackHead ArrangeOverride error: {ex.Message}");
                return finalSize;
            }
        }
    }
}



