namespace FFXIVTataruHelper.Factories
{
    public interface IOutgoingChatWindowFactory
    {
        void Bind(TataruUIModel tataruUIModel, MainWindow mainWindow);

        OutgoingChatWindow GetOrCreate();
    }
}