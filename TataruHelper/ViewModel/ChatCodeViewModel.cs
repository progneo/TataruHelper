using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace FFXIVTataruHelper.ViewModel
{
    public class ChatCodeViewModel : INotifyPropertyChanged, IEquatable<ChatCodeViewModel>
    {
        [JsonProperty]
        string _code;

        [JsonProperty]
        string _name;

        [JsonProperty]
        Color _color;

        [JsonProperty]
        bool _isChecked;

        [JsonIgnore]
        public string Code
        {
            get { return _code; }
            private set
            {
                _code = value;
                OnPropertyChanged();
            }
        }

        [JsonIgnore]
        public string Name
        {
            get { return _name; }
            set
            {
                var val = value.Replace("Ck", "");
                _name = "Ck" + val;
                OnPropertyChanged();
            }
        }

        [JsonIgnore]
        public Color Color
        {
            get { return _color; }
            set
            {
                if (_color == value) return;

                _color = value;
                OnPropertyChanged();
            }
        }

        [JsonIgnore]
        public bool IsChecked
        {
            get { return _isChecked; }
            set
            {
                if (_isChecked == value) return;

                _isChecked = value;
                OnPropertyChanged();
            }
        }

        public ChatCodeViewModel()
        {
            Code = string.Empty;
            Name = string.Empty;
            Color = Color.FromArgb(255, 255, 255, 255);
            IsChecked = false;
        }

        public ChatCodeViewModel(string code, string name, Color color, bool isChecked)
        {
            Code = code;
            Name = name;
            Color = color;
            IsChecked = isChecked;
        }

        public ChatCodeViewModel(ChatCodeViewModel chatCodeViewModel)
        {
            Code = chatCodeViewModel._code;
            Name = chatCodeViewModel._name;
            Color = chatCodeViewModel._color;
            IsChecked = chatCodeViewModel._isChecked;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            var localPropertyChanged = PropertyChanged;
            if (localPropertyChanged != null)
                localPropertyChanged(this, new PropertyChangedEventArgs(prop));
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as ChatCodeViewModel);
        }

        public bool Equals(ChatCodeViewModel code)
        {
            // If parameter is null, return false.
            if (Object.ReferenceEquals(code, null))
                return false;

            // Optimization for a common success case.
            if (Object.ReferenceEquals(this, code))

                // If run-time types are not exactly the same, return false.
                if (this.GetType() != code.GetType())
                    return false;

            return this._code == code._code;
        }

        public static bool operator ==(ChatCodeViewModel left, ChatCodeViewModel right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (ReferenceEquals(left, null))
            {
                return false;
            }

            if (ReferenceEquals(right, null))
            {
                return false;
            }

            return left.Equals(right);
        }

        public static bool operator !=(ChatCodeViewModel left, ChatCodeViewModel right) => !(left == right);

        public override int GetHashCode()
        {
            return _code.GetHashCode();
        }
    }
}
