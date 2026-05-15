using BondTech.HotKeyManagement.WPF._4;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.WinUtils;
using System;
using System.Windows.Input;

namespace FFXIVTataruHelper.Services.HotKeys
{
    public sealed class HotKeyBindingService : IHotKeyBindingService
    {
        private readonly IAppLogger _logger;

        public HotKeyBindingService(IAppLogger logger)
        {
            _logger = logger;
        }

        public void RegisterHotKeyDown(HotKeyManager hotKeyManager, ref GlobalHotKey globalHotKey, HotKeyCombination hotKeyCombination, KeyEventArgs e, bool isDisposed)
        {
            if (isDisposed)
            {
                return;
            }

            try
            {
                var key = Helper.RealKey(e);
                var pressedKeys = Keys.ClearMousKeys(Keys.ClearRepeatedKeys(Keys.GetPressdKeys()));

                if (pressedKeys.Length <= 1)
                {
                    hotKeyCombination.ClearKeys();
                }

                hotKeyCombination.AddKey(key);

                if (!hotKeyCombination.IsInitialized && globalHotKey != null)
                {
                    hotKeyManager.RemoveGlobalHotKey(globalHotKey);
                }
            }
            catch (Exception ex)
            {
                _logger.WriteLog(ex);
            }
        }

        public void RegisterHotKeyUp(HotKeyManager hotKeyManager, ref GlobalHotKey globalHotKey, HotKeyCombination hotKeyCombination, KeyEventArgs e, bool isDisposed)
        {
            if (isDisposed)
            {
                return;
            }

            try
            {
                var pressedKeys = Keys.ClearMousKeys(Keys.ClearRepeatedKeys(Keys.GetPressdKeys()));

                if (pressedKeys.Length != 0)
                {
                    return;
                }

                Keyboard.ClearFocus();

                if (globalHotKey != null)
                {
                    hotKeyManager.RemoveGlobalHotKey(globalHotKey);
                }

                if (!hotKeyCombination.IsInitialized)
                {
                    return;
                }

                var key = Keys.ConvertFromWpfKey(hotKeyCombination.NormalKey);
                globalHotKey = new GlobalHotKey(hotKeyCombination.Name, hotKeyCombination.ModifierKey, key);
                hotKeyManager.AddGlobalHotKey(globalHotKey);
            }
            catch (Exception ex)
            {
                _logger.WriteLog(ex);
            }
        }

        public void ReRegisterGlobalHotKey(HotKeyManager hotKeyManager, ref GlobalHotKey globalHotKey, HotKeyCombination hotKeyCombination, bool isDisposed)
        {
            if (isDisposed)
            {
                return;
            }

            if (globalHotKey != null)
            {
                hotKeyManager.RemoveGlobalHotKey(globalHotKey);
                globalHotKey = null;
            }

            if (!hotKeyCombination.IsInitialized)
            {
                return;
            }

            var key = Keys.ConvertFromWpfKey(hotKeyCombination.NormalKey);
            globalHotKey = new GlobalHotKey(hotKeyCombination.Name, hotKeyCombination.ModifierKey, key);
            hotKeyManager.AddGlobalHotKey(globalHotKey);
        }
    }
}
