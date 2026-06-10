using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

using FFXIVTataruHelper.ViewModel;

namespace FFXIVTataruHelper.Services.UI
{
    public sealed class ChatMessageParagraphBuilder
    {
        private readonly ChatWindowViewModel _viewModel;

        public ChatMessageParagraphBuilder(ChatWindowViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public Paragraph BuildMessageParagraph(string translatedMsg, Color color, DateTime timeStamp)
        {
            string leadingSpaces = _viewModel.SpacingCount > 0
                ? new string(' ', _viewModel.SpacingCount)
                : string.Empty;

            string name = null;
            string text = translatedMsg;

            int nameInd = translatedMsg.IndexOf(":", StringComparison.Ordinal);
            if (nameInd > 0)
            {
                name = translatedMsg.Substring(0, nameInd);
                text = translatedMsg.Substring(nameInd, translatedMsg.Length - nameInd);
            }

            if (timeStamp != default(DateTime))
            {
                if (!string.IsNullOrEmpty(name))
                {
                    name = timeStamp.ToString("HH:mm") + " " + name;
                }
                else
                {
                    text = timeStamp.ToString("HH:mm") + " " + text;
                }
            }

            if (_viewModel.MessagesInContainer)
            {
                return BuildContainedMessageParagraph(leadingSpaces, name, text, color);
            }

            return BuildPlainMessageParagraph(leadingSpaces, name, text, color);
        }

        public void ApplyMessageContainerVisual(Border border)
        {
            if (border == null)
            {
                return;
            }

            var baseColor = border.Tag is Color color ? color : Colors.White;
            var backgroundAlpha = (byte)Math.Clamp(_viewModel.MessageContainerAlpha, 0, 255);
            var borderAlpha = (byte)Math.Clamp(_viewModel.MessageContainerBorderAlpha, 0, 255);

            border.Padding = new Thickness(_viewModel.MessageContainerPadding);
            border.Background = new SolidColorBrush(
                Color.FromArgb(backgroundAlpha, baseColor.R, baseColor.G, baseColor.B));
            border.BorderThickness = new Thickness(_viewModel.MessageContainerBorderThickness);
            border.BorderBrush = new SolidColorBrush(
                Color.FromArgb(borderAlpha, baseColor.R, baseColor.G, baseColor.B));
        }

        private Paragraph BuildPlainMessageParagraph(string leadingSpaces, string name, string text, Color color)
        {
            var paragraph = new Paragraph
            {
                Margin = new Thickness(0, _viewModel.LineBreakHeight, 0, 0), TextAlignment = TextAlignment.Left
            };

            if (!string.IsNullOrEmpty(leadingSpaces))
            {
                paragraph.Inlines.Add(CreateRun(leadingSpaces, color, FontWeights.Normal));
            }

            if (!string.IsNullOrEmpty(name))
            {
                paragraph.Inlines.Add(CreateRun(name, color, FontWeights.Bold));
            }

            paragraph.Inlines.Add(CreateRun(text, color, FontWeights.Normal));
            return paragraph;
        }

        private Paragraph BuildContainedMessageParagraph(string leadingSpaces, string name, string text, Color color)
        {
            var messageText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontFamily = _viewModel.ChatFont,
                FontSize = _viewModel.ChatFontSize,
                Foreground = new SolidColorBrush(color)
            };

            if (!string.IsNullOrEmpty(leadingSpaces))
            {
                messageText.Inlines.Add(new Run(leadingSpaces));
            }

            if (!string.IsNullOrEmpty(name))
            {
                messageText.Inlines.Add(new Run(name) { FontWeight = FontWeights.Bold });
            }

            messageText.Inlines.Add(new Run(text));

            var messageBorder = new Border { CornerRadius = new CornerRadius(6), Tag = color, Child = messageText };
            ApplyMessageContainerVisual(messageBorder);

            var paragraph = new Paragraph
            {
                Margin = new Thickness(0, _viewModel.LineBreakHeight, 0, 0), TextAlignment = TextAlignment.Left
            };

            paragraph.Inlines.Add(new InlineUIContainer(messageBorder));
            return paragraph;
        }

        private Run CreateRun(string text, Color color, FontWeight fontWeight)
        {
            return new Run(text)
            {
                Foreground = new SolidColorBrush(color),
                FontWeight = fontWeight,
                FontFamily = _viewModel.ChatFont,
                FontSize = _viewModel.ChatFontSize
            };
        }
    }
}