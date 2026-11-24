using System;
using System.Windows;
using System.Windows.Media;

namespace RGRTU.Messenger.Client.Models
{
    public class ChatMessageViewModel
    {
        public int SenderId { get; init; }
        public string SenderName { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; }
        public bool IsOwn { get; init; }

        public string TimeText => Timestamp.ToString("HH:mm");
        public HorizontalAlignment Alignment => IsOwn ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        public Visibility SenderVisibility => IsOwn ? Visibility.Collapsed : Visibility.Visible;

        public Brush BubbleBrush { get; init; } = Brushes.White;
        public Brush BorderBrush { get; init; } = Brushes.LightGray;
        public Brush SenderBrush { get; init; } = Brushes.SteelBlue;

        public static ChatMessageViewModel Create(int senderId, string senderName, string text, DateTime timestamp, int currentUserId)
        {
            bool isOwn = senderId == currentUserId;
            var (bubble, border, sender) = GetPalette(senderId);
            return new ChatMessageViewModel
            {
                SenderId = senderId,
                SenderName = senderName,
                Text = text,
                Timestamp = timestamp,
                IsOwn = isOwn,
                BubbleBrush = isOwn ? new LinearGradientBrush(Color.FromRgb(219, 234, 254), Color.FromRgb(191, 219, 254), 45) : bubble,
                BorderBrush = isOwn ? new SolidColorBrush(Color.FromRgb(147, 197, 253)) : border,
                SenderBrush = sender
            };
        }

        private static (Brush bubble, Brush border, Brush sender) GetPalette(int userId)
        {
            // Deterministic varied palette per user.
            var palettes = new[]
            {
                (Color.FromRgb(236, 253, 245), Color.FromRgb(167, 243, 208), Color.FromRgb(5, 150, 105)),
                (Color.FromRgb(255, 247, 237), Color.FromRgb(253, 186, 116), Color.FromRgb(234, 88, 12)),
                (Color.FromRgb(245, 243, 255), Color.FromRgb(196, 181, 253), Color.FromRgb(124, 58, 237)),
                (Color.FromRgb(254, 242, 242), Color.FromRgb(252, 165, 165), Color.FromRgb(220, 38, 38)),
                (Color.FromRgb(240, 249, 255), Color.FromRgb(125, 211, 252), Color.FromRgb(2, 132, 199)),
                (Color.FromRgb(253, 244, 255), Color.FromRgb(240, 171, 252), Color.FromRgb(192, 38, 211)),
                (Color.FromRgb(254, 252, 232), Color.FromRgb(253, 224, 71), Color.FromRgb(161, 98, 7)),
                (Color.FromRgb(240, 253, 250), Color.FromRgb(94, 234, 212), Color.FromRgb(13, 148, 136)),
            };

            int idx = Math.Abs(userId * 31) % palettes.Length;
            var p = palettes[idx];
            // Add slight gradient for non-monotone look.
            var alt = palettes[(idx + 3) % palettes.Length].Item1;
            var bubble = new LinearGradientBrush(p.Item1, Color.FromRgb(
                (byte)((p.Item1.R + alt.R) / 2),
                (byte)((p.Item1.G + alt.G) / 2),
                (byte)((p.Item1.B + alt.B) / 2)), 135);
            return (bubble, new SolidColorBrush(p.Item2), new SolidColorBrush(p.Item3));
        }
    }
}