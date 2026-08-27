using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using AnyUninstaller.Avalonia.ViewModels;
using SimpleTreeMap;

namespace AnyUninstaller.Avalonia.Controls
{
    public class AvaloniaTreeMap : Control
    {
        public static readonly StyledProperty<IEnumerable<ApplicationEntryViewModel>?> ItemsSourceProperty =
            AvaloniaProperty.Register<AvaloniaTreeMap, IEnumerable<ApplicationEntryViewModel>?>(nameof(ItemsSource));

        public static readonly StyledProperty<ApplicationEntryViewModel?> SelectedItemProperty =
            AvaloniaProperty.Register<AvaloniaTreeMap, ApplicationEntryViewModel?>(
                nameof(SelectedItem),
                defaultBindingMode: BindingMode.TwoWay);

        public IEnumerable<ApplicationEntryViewModel>? ItemsSource
        {
            get => GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public ApplicationEntryViewModel? SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public event EventHandler<ApplicationEntryViewModel>? ItemClicked;

        private List<SliceRectangle<object>>? _rectangles;
        private readonly Pen _borderPen = new(new SolidColorBrush(Color.FromArgb(180, 13, 17, 23)), 1.5);
        private readonly IBrush _selectedBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246));
        private readonly Pen _selectedPen = new(new SolidColorBrush(Color.FromRgb(255, 255, 255)), 2);
        private readonly IBrush _backgroundBrush = new SolidColorBrush(Color.FromRgb(13, 17, 23));
        private readonly IBrush _textBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));

        // Modern curated color palette
        private static readonly Color[] Palette = new[]
        {
            Color.FromRgb(59, 130, 246),  // Blue
            Color.FromRgb(16, 185, 129),  // Emerald
            Color.FromRgb(245, 158, 11),  // Amber
            Color.FromRgb(168, 85, 247),  // Purple
            Color.FromRgb(6, 182, 212),   // Cyan
            Color.FromRgb(244, 63, 94),   // Rose
            Color.FromRgb(99, 102, 241),  // Indigo
            Color.FromRgb(234, 88, 12),   // Orange
            Color.FromRgb(20, 184, 166),  // Teal
            Color.FromRgb(132, 204, 22)   // Lime
        };

        static AvaloniaTreeMap()
        {
            AffectsRender<AvaloniaTreeMap>(ItemsSourceProperty, SelectedItemProperty);
        }

        public AvaloniaTreeMap()
        {
            ClipToBounds = true;
            AnyUninstaller.Avalonia.Services.AppSettingsService.Instance.SettingsChanged += () =>
            {
                InvalidateVisual();
            };
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ItemsSourceProperty || change.Property == BoundsProperty)
            {
                RecalculateSlices();
                InvalidateVisual();
            }
            else if (change.Property == SelectedItemProperty)
            {
                InvalidateVisual();
            }
        }

        private void RecalculateSlices()
        {
            if (ItemsSource == null || Bounds.Width <= 10 || Bounds.Height <= 10)
            {
                _rectangles = null;
                return;
            }

            var items = ItemsSource
                .Where(x => x.EstimatedSize.GetKbSize() > 0)
                .OrderByDescending(x => x.EstimatedSize.GetKbSize())
                .Take(200) // Limit to top 200 items for snappy performance
                .ToList();

            if (items.Count == 0)
            {
                _rectangles = null;
                return;
            }

            var elements = new List<Element<object>>();
            int colorIndex = 0;
            foreach (var item in items)
            {
                var color = Palette[colorIndex % Palette.Length];
                colorIndex++;
                var gdiColor = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
                elements.Add(new Element<object>
                {
                    Object = item,
                    Value = item.EstimatedSize.GetKbSize(),
                    Color = gdiColor,
                    Text = item.DisplayName
                });
            }

            try
            {
                var slice = SliceMaker.GetSlice(elements, 1, 0.35);
                _rectangles = SliceMaker.GetRectangles(slice, (int)Bounds.Width, (int)Bounds.Height).ToList();
            }
            catch
            {
                _rectangles = null;
            }
        }

        public override void Render(DrawingContext context)
        {
            var bgBrush = (Application.Current?.Resources["CardInnerBgBrush"] as IBrush) ?? _backgroundBrush;
            var borderBrush = (Application.Current?.Resources["CardBorderBrush"] as IBrush) ?? (Application.Current?.Resources["CardBgBrush"] as IBrush) ?? _backgroundBrush;
            var currentBorderPen = new Pen(borderBrush, 1.5);

            context.DrawRectangle(bgBrush, null, new Rect(0, 0, Bounds.Width, Bounds.Height));

            if (_rectangles == null || _rectangles.Count == 0)
                return;

            var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

            foreach (var r in _rectangles)
            {
                var element = r.Slice?.Elements?.FirstOrDefault();
                if (element?.Object is not ApplicationEntryViewModel item)
                    continue;

                var rect = new Rect(r.X + 1, r.Y + 1, Math.Max(0, r.Width - 2), Math.Max(0, r.Height - 2));
                if (rect.Width <= 2 || rect.Height <= 2)
                    continue;

                bool isSelected = SelectedItem == item || item.IsChecked;

                IBrush fillBrush;
                if (isSelected)
                {
                    fillBrush = _selectedBrush;
                }
                else
                {
                    var c = element.Color;
                    fillBrush = new SolidColorBrush(Color.FromArgb(220, c.R, c.G, c.B));
                }

                context.DrawRectangle(fillBrush, isSelected ? _selectedPen : currentBorderPen, new RoundedRect(rect, 4, 4));

                // Only draw label if the block can cleanly display the FULL name without truncation or word-splitting
                if (rect.Width >= 38 && rect.Height >= 24)
                {
                    var fontSize = Math.Clamp(Math.Min(rect.Width / 7.5, rect.Height / 3.8), 9.0, 11.5);
                    var maxAvailableWidth = Math.Max(10, rect.Width - 8);
                    var maxAvailableHeight = Math.Max(10, rect.Height - 6);

                    // 1. Verify every individual word in the name fits within the box width
                    var words = item.DisplayName.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    bool wordsFit = true;
                    foreach (var word in words)
                    {
                        var wordMeasure = new FormattedText(
                            word,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            fontSize,
                            _textBrush
                        );
                        if (wordMeasure.Width > maxAvailableWidth)
                        {
                            wordsFit = false;
                            break;
                        }
                    }

                    if (wordsFit)
                    {
                        // Try displaying both full name and size
                        string displayText = $"{item.DisplayName}\n{item.EstimatedSize}";

                        var formattedText = new FormattedText(
                            displayText,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            fontSize,
                            _textBrush
                        )
                        {
                            MaxTextWidth = maxAvailableWidth,
                            TextAlignment = TextAlignment.Center
                        };

                        // 2. Render only if all lines fit within the vertical bounds
                        if (formattedText.Height <= maxAvailableHeight)
                        {
                            var textY = rect.Y + (rect.Height - formattedText.Height) / 2;
                            var textX = rect.X + 4;

                            context.DrawText(formattedText, new Point(textX, textY));
                        }
                        else
                        {
                            // If name + size was too tall, test if full name alone fits cleanly
                            var nameOnlyText = new FormattedText(
                                item.DisplayName,
                                CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight,
                                typeface,
                                fontSize,
                                _textBrush
                            )
                            {
                                MaxTextWidth = maxAvailableWidth,
                                TextAlignment = TextAlignment.Center
                            };

                            if (nameOnlyText.Height <= maxAvailableHeight)
                            {
                                var textY = rect.Y + (rect.Height - nameOnlyText.Height) / 2;
                                var textX = rect.X + 4;

                                context.DrawText(nameOnlyText, new Point(textX, textY));
                            }
                        }
                    }
                }
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (_rectangles == null) return;

            var point = e.GetCurrentPoint(this).Position;
            var hovered = _rectangles.FirstOrDefault(r =>
                point.X >= r.X && point.X <= r.X + r.Width &&
                point.Y >= r.Y && point.Y <= r.Y + r.Height);

            if (hovered?.Slice?.Elements?.FirstOrDefault()?.Object is ApplicationEntryViewModel item)
            {
                Cursor = new Cursor(StandardCursorType.Hand);
                ToolTip.SetTip(this, $"{item.DisplayName}\nSize: {item.EstimatedSize}\nPublisher: {item.Publisher}");
            }
            else
            {
                Cursor = Cursor.Default;
                ToolTip.SetTip(this, null);
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (_rectangles == null) return;

            var point = e.GetCurrentPoint(this).Position;
            var clicked = _rectangles.FirstOrDefault(r =>
                point.X >= r.X && point.X <= r.X + r.Width &&
                point.Y >= r.Y && point.Y <= r.Y + r.Height);

            if (clicked?.Slice?.Elements?.FirstOrDefault()?.Object is ApplicationEntryViewModel item)
            {
                SelectedItem = item;
                ItemClicked?.Invoke(this, item);
            }
        }
    }
}
