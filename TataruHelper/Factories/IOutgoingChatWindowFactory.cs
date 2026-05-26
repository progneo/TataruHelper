using System.Windows.Input;

using FFXIVTataruHelper.Compatibility.HotKeys;

namespace FFXIVTataruHelper.Factories
{
    public interface IOutgoingChatWindowFactory
    {
        void Bind(TataruUIModel tataruUIModel, MainWindow mainWindow, HotKeyManager hotKeyManager);

        OutgoingChatWindow GetOrCreate();

        void ReapplyHotKey();

        void CaptureHotKeyDown(KeyEventArgs e);

        void CaptureHotKeyUp(KeyEventArgs e);
    }
}