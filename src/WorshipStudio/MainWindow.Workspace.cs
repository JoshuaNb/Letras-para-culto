using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using WorshipStudio.Models;

namespace WorshipStudio;

public partial class MainWindow
{
    private List<Song> _timelineSongs = [];

    private void BindWorkspaceMenu()
    {
        var commands = new (string Menu, string Button)[]
        {
            ("MenuNewSong", "NewButton"), ("MenuImportSongs", "ImportButton"),
            ("MenuBackup", "BackupButton"), ("MenuRestore", "RestoreButton"),
            ("MenuEditSong", "EditButton"), ("MenuDeleteSong", "DeleteButton"),
            ("MenuAddSetlist", "AddSetButton"), ("MenuOpenProjection", "OpenProjectionButton"),
            ("MenuNextSlide", "NextButton"), ("MenuPreviousSlide", "PrevButton"),
            ("MenuHideLyrics", "HideButton"), ("MenuBlackout", "BlackButton"),
            ("MenuAnimations", "AnimationGalleryButton"), ("MenuConnections", "ConnectionsWorkspaceButton"),
            ("MenuReceivedFiles", "ReceivedFilesButton"), ("MenuExportVideo", "ExportButton"),
            ("MenuUpdates", "UpdateButton"), ("MenuHelp", "HelpButton")
        };
        foreach (var (menu, button) in commands)
            C<MenuItem>(menu).Click += (_, _) =>
            {
                var target = C<Button>(button);
                if (target.IsEnabled) target.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            };
        C<MenuItem>("MenuExit").Click += (_, _) => Close();
        C<MenuItem>("MenuPreferences").Click += (_, _) => C<TabControl>("InspectorTabs").SelectedIndex = 2;
    }

    private void RefreshSetlistTimeline()
    {
        var panel = C<StackPanel>("SetlistTimelinePanel");
        var songs = _document.Setlist.Select(id => _document.Songs.FirstOrDefault(song => song.Id == id)).OfType<Song>().ToList();
        C<TextBlock>("SetlistTimelineSummary").Text = songs.Count == 0
            ? "Adicione louvores da biblioteca para montar a ordem do culto"
            : $"{songs.Count} louvor(es) · clique para preparar · duplo clique para projetar";
        if (songs.SequenceEqual(_timelineSongs))
        {
            foreach (var clip in panel.Children.OfType<Button>())
            {
                var selected = clip.Tag is Guid id && _selected?.Id == id;
                var live = clip.Tag is Guid liveId && _liveSongId == liveId && _projection != null;
                clip.Bind(Button.BackgroundProperty, new DynamicResourceExtension(selected ? "WsSelection" : "WsSetlistClip"));
                clip.Bind(Button.BorderBrushProperty, new DynamicResourceExtension(live ? "WsLive" : "WsSuccess"));
            }
            UpdateTimelinePosition();return;
        }
        _timelineSongs = songs;
        panel.Children.Clear();
        for (var i = 0; i < songs.Count; i++)
        {
            var song = songs[i];
            var live = _liveSongId == song.Id && _projection != null;
            var selected = _selected?.Id == song.Id;
            var title = new TextBlock { Text = $"{i + 1:00}  {song.Title}", FontSize = 10, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
            title.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("WsSetlistText"));
            var description = new TextBlock { Text = $"{LyricParser.Parse(song.Lyrics).Count} estrofe(s)", FontSize = 9 };
            description.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("WsSetlistText"));
            var button = new Button
            {
                Name = "SetlistClip_" + song.Id.ToString("N"), Tag = song.Id,
                Content = new StackPanel { Spacing = 1, Children = { title, description } },
                Width = 206, Height = 44, Margin = new Thickness(0, 0, 7, 0), Padding = new Thickness(9, 3),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, BorderThickness = new Thickness(0, 3, 0, 0)
            };
            button.Bind(Button.BackgroundProperty, new DynamicResourceExtension(selected ? "WsSelection" : "WsSetlistClip"));
            button.Bind(Button.BorderBrushProperty, new DynamicResourceExtension(live ? "WsLive" : "WsSuccess"));
            button.Click += (_, _) => SelectTimelineSong(song);
            button.DoubleTapped += (_, _) => { SelectTimelineSong(song); SendPreview(); };
            panel.Children.Add(button);
        }
        UpdateTimelinePosition();
    }

    private void SelectTimelineSong(Song song)
    {
        C<TextBox>("SearchBox").Text = "";
        C<ComboBox>("LibraryMode").SelectedIndex = 1;
        RefreshLibrary(song.Id);
    }

    private void UpdateTimelinePosition()
    {
        var position = this.FindControl<TextBlock>("TimelinePosition");
        if (position == null) return;
        var preview = _slides.Count == 0 ? "00 / 00" : $"{_previewIndex + 1:00} / {_slides.Count:00}";
        var live = _projection == null ? "SAÍDA FECHADA" : _liveSlides.Count == 0 ? "AO VIVO · SEM ESTROFES" : $"AO VIVO {_liveIndex + 1:00} / {_liveSlides.Count:00}";
        position.Text = $"PRÉVIA {preview} · {live}";
    }
}
