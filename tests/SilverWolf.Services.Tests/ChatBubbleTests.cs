using SilverWolf.App.Models;
using Xunit;

// Only the enum used by the model is stubbed; tests never load WinUI/native UI.
// These verify property notifications/state logic, not visual rendering.
namespace Microsoft.UI.Xaml
{
    public enum Visibility { Visible, Collapsed }
}

namespace SilverWolf.Services.Tests
{
    public sealed class ChatBubbleTests
    {
        [Fact]
        public void PendingCompletionStopsLoadingAndRevealsContent()
        {
            var bubble = new ChatBubble { Pending = true, SedangMemuat = true };
            var changed = new List<string?>();
            bubble.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            bubble.Pending = false;
            Assert.False(bubble.SedangMemuat);
            Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, bubble.VisibilitasMemuat);
            Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, bubble.VisibilitasKonten);
            Assert.Contains(nameof(bubble.VisibilitasMemuat), changed);
            Assert.Contains(nameof(bubble.VisibilitasKonten), changed);
        }

        [Fact]
        public void ErrorStopsLoadingAndNotifiesDerivedErrorBindings()
        {
            var bubble = new ChatBubble { Pending = true, SedangMemuat = true };
            var changed = new List<string?>();
            bubble.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            bubble.Error = "backend menjawab 503";
            Assert.False(bubble.Pending);
            Assert.False(bubble.SedangMemuat);
            Assert.True(bubble.PunyaGalat);
            Assert.Contains("503", bubble.TeksGalat);
            Assert.Contains(nameof(bubble.TeksGalat), changed);
            Assert.Contains(nameof(bubble.PunyaGalat), changed);
        }

        [Fact]
        public void ThinkingAndAudioPreparationLabelsAreObservable()
        {
            var bubble = new ChatBubble();
            var changed = new List<string?>();
            bubble.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            bubble.TeksMemuat = "sedang menyiapkan suara…";
            bubble.CatatanSuara = "Suara gagal; teks tetap tersedia.";
            Assert.Contains(nameof(bubble.TeksMemuat), changed);
            Assert.Contains(nameof(bubble.CatatanSuara), changed);
        }
    }
}
