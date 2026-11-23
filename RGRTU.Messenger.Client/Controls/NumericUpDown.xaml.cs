using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RGRTU.Messenger.Client.Controls
{
    /// <summary>
    /// Компактный numeric up/down в бело-голубой палитре приложения.
    /// Используется для выбора часов и минут в фильтре журнала событий.
    /// </summary>
    public partial class NumericUpDown : UserControl
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value),
            typeof(int),
            typeof(NumericUpDown),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged));

        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
            nameof(Minimum),
            typeof(int),
            typeof(NumericUpDown),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.None, OnRangeChanged));

        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
            nameof(Maximum),
            typeof(int),
            typeof(NumericUpDown),
            new FrameworkPropertyMetadata(59, FrameworkPropertyMetadataOptions.None, OnRangeChanged));

        /// <summary>
        /// Текущее значение.
        /// </summary>
        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public int Minimum
        {
            get => (int)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public int Maximum
        {
            get => (int)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public event EventHandler? ValueChanged;

        public NumericUpDown()
        {
            InitializeComponent();
            UpdateText();
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (NumericUpDown)d;
            int clamped = Math.Max(control.Minimum, Math.Min(control.Maximum, (int)e.NewValue));

            if (clamped != control.Value)
            {
                control.SetCurrentValue(ValueProperty, clamped);
                return;
            }

            control.UpdateText();
            control.ValueChanged?.Invoke(control, EventArgs.Empty);
        }

        private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (NumericUpDown)d;
            int clamped = Math.Max(control.Minimum, Math.Min(control.Maximum, control.Value));
            if (clamped != control.Value)
                control.SetCurrentValue(ValueProperty, clamped);
            else
                control.UpdateText();
        }

        private void UpdateText()
        {
            if (ValueTextBox != null)
                ValueTextBox.Text = Value.ToString("D2", CultureInfo.InvariantCulture);
        }

        private void CommitText()
        {
            if (int.TryParse(ValueTextBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed))
                Value = parsed;
            else
                UpdateText();
        }

        private void IncreaseButton_Click(object sender, RoutedEventArgs e)
        {
            Value = Value + 1;
        }

        private void DecreaseButton_Click(object sender, RoutedEventArgs e)
        {
            Value = Value - 1;
        }

        private void ValueTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            foreach (char ch in e.Text)
            {
                if (!char.IsDigit(ch))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private void ValueTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitText();
        }

        private void ValueTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                CommitText();
        }
    }
}
