using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using NHotkey;
using NHotkey.Wpf;

namespace BondTech.HotKeyManagement.WPF._4
{
    public sealed class GlobalHotKey
    {
        public string Name { get; }

        public ModifierKeys ModifierKeys { get; }

        public Key Key { get; }

        public GlobalHotKey(string name, ModifierKeys modifierKeys, Key key)
        {
            Name = name;
            ModifierKeys = modifierKeys;
            Key = key;
        }
    }

    public sealed class GlobalHotKeyEventArgs : EventArgs
    {
        public GlobalHotKey HotKey { get; }

        public GlobalHotKeyEventArgs(GlobalHotKey hotKey)
        {
            HotKey = hotKey;
        }
    }

    public sealed class HotKeyManager : IDisposable
    {
        private readonly Dictionary<string, EventHandler<HotkeyEventArgs>> _handlersByName = new Dictionary<string, EventHandler<HotkeyEventArgs>>(StringComparer.Ordinal);

        public event EventHandler<GlobalHotKeyEventArgs> GlobalHotKeyPressed;

        public HotKeyManager(Window owner)
        {
        }

        public void AddGlobalHotKey(GlobalHotKey hotKey)
        {
            if (hotKey == null || string.IsNullOrWhiteSpace(hotKey.Name))
            {
                return;
            }

            RemoveGlobalHotKey(hotKey);

            EventHandler<HotkeyEventArgs> handler = (_, __) =>
            {
                GlobalHotKeyPressed?.Invoke(this, new GlobalHotKeyEventArgs(hotKey));
            };

            NHotkey.Wpf.HotkeyManager.Current.AddOrReplace(hotKey.Name, hotKey.Key, hotKey.ModifierKeys, handler);
            _handlersByName[hotKey.Name] = handler;
        }

        public void RemoveGlobalHotKey(GlobalHotKey hotKey)
        {
            if (hotKey == null || string.IsNullOrWhiteSpace(hotKey.Name))
            {
                return;
            }

            NHotkey.Wpf.HotkeyManager.Current.Remove(hotKey.Name);
            _handlersByName.Remove(hotKey.Name);
        }

        public void Dispose()
        {
            foreach (var name in _handlersByName.Keys.ToArray())
            {
                NHotkey.Wpf.HotkeyManager.Current.Remove(name);
            }

            _handlersByName.Clear();
        }
    }

    public static class Keys
    {
        public static Key[] GetPressdKeys()
        {
            return Enum.GetValues(typeof(Key))
                .Cast<Key>()
                .Where(Keyboard.IsKeyDown)
                .ToArray();
        }

        public static Key[] ClearMousKeys(Key[] keys)
        {
            if (keys == null)
            {
                return Array.Empty<Key>();
            }

            return keys.Where(key => key != Key.None && key != Key.Cancel).ToArray();
        }

        public static Key[] ClearRepeatedKeys(Key[] keys)
        {
            if (keys == null)
            {
                return Array.Empty<Key>();
            }

            return keys.Distinct().ToArray();
        }

        public static Key ConvertFromWpfKey(Key key)
        {
            return key;
        }
    }
}
