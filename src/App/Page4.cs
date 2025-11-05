using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Diffracta.Graphics;
using FFMpegCore;

namespace Diffracta;

public partial class Page4 : UserControl
{
    public Page4()
    {
        InitializeComponent();

        var startBtn = this.FindControl<Button>("StartVideoButton");
        var surface = this.FindControl<VideoSurface>("HelpVideoSurface");
        var infoText = this.FindControl<TextBlock>("VideoInfoText");
        if (startBtn != null && surface != null)
        {
            startBtn.Click += async (_, __) =>
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Media", "default", "vid_sd_960_506_24fps.mp4");
                // Show media properties
                try
                {
                    var info = await FFProbe.AnalyseAsync(path);
                    var sb = new StringBuilder();
                    sb.AppendLine($"File: {Path.GetFileName(path)}");
                    sb.AppendLine($"Resolution: {info.PrimaryVideoStream?.Width}x{info.PrimaryVideoStream?.Height}");
                    sb.AppendLine($"Codec: {info.PrimaryVideoStream?.CodecName}");
                    sb.AppendLine($"FPS: {info.PrimaryVideoStream?.AvgFrameRate}");
                    sb.AppendLine($"Duration: {info.Duration}");
                    if (infoText != null) infoText.Text = sb.ToString();
                }
                catch (Exception ex)
                {
                    if (infoText != null) infoText.Text = $"Error reading media: {ex.Message}";
                }
                
                surface.Start(path);
            };
        }
    }
}
