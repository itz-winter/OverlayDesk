using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;

namespace OverlayDesk;

internal static class VlcEngine
{
    private static LibVLC? _engine;
    public static LibVLC Shared
    {
        get
        {
            if (_engine == null)
            {
                Core.Initialize();
                _engine = new LibVLC("--no-video-title-show", "--no-osd");
            }
            return _engine;
        }
    }

    public static void Shutdown()
    {
        _engine?.Dispose();
        _engine = null;
    }
}




internal sealed class MediaView : Panel
{
    private readonly MediaPlayer _player;
    private readonly VideoView? _video;
    private readonly Label? _label;
    private readonly string _path;
    private bool _started;

    public MediaView(string path, bool video)
    {
        _path = path;
        BackColor = Color.Black;
        _player = new MediaPlayer(VlcEngine.Shared);
        if (video)
        {
            _video = new VideoView { Dock = DockStyle.Fill, BackColor = Color.Black, MediaPlayer = _player };
            Controls.Add(_video);
        }
        else
        {
            _label = new Label
            {
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                Text = Path.GetFileName(path) + "\n\nClick to pause / resume", ForeColor = Color.White,
                BackColor = Color.FromArgb(32, 32, 32)
            };
            _label.Click += (_, _) => TogglePause();
            Controls.Add(_label);
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        
        
        BeginInvoke((Action)StartPlayback);
    }

    private void StartPlayback()
    {
        if (_started || IsDisposed || !IsHandleCreated) return;
        _started = true;
        try
        {
            if (_video != null) _ = _video.Handle;
            using var media = new Media(VlcEngine.Shared, new Uri(Path.GetFullPath(_path)));
            if (!_player.Play(media)) throw new InvalidOperationException("VLC could not start playback.");
        }
        catch (Exception ex)
        {
            if (!IsDisposed) MessageBox.Show(FindForm(), ex.Message,
                "Media playback error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    public void TogglePause()
    {
        if (_player.IsPlaying) _player.Pause();
        else _player.Play();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _player.Stop();
            if (_video != null) _video.MediaPlayer = null;
            _player.Dispose();
        }
        base.Dispose(disposing);
    }
}
