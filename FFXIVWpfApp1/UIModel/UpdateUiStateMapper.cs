using Updater.EventArguments;

namespace FFXIVTataruHelper
{
    public sealed class UpdateUiTransition
    {
        public bool DisableCheckButton { get; set; }
        public bool HideUserStartedText { get; set; }
        public bool ShowDownloading { get; set; }
        public bool HideDownloading { get; set; }
        public bool ShowRestartReady { get; set; }
        public bool ShowNoUpdatesByUserRequest { get; set; }
        public bool ShowErrorByUserRequest { get; set; }
        public bool CompleteUserFlow { get; set; }
    }

    public static class UpdateUiStateMapper
    {
        public static UpdateUiTransition Map(
            UpdateState state,
            bool updateCheckByUser,
            bool restartReadyVisible,
            bool downloadingVisible)
        {
            var transition = new UpdateUiTransition();

            switch (state)
            {
                case UpdateState.Initializing:
                    transition.DisableCheckButton = true;
                    break;

                case UpdateState.Downloading:
                    transition.HideUserStartedText = true;
                    transition.ShowDownloading = true;
                    break;

                case UpdateState.ReadyToRestart:
                    transition.HideUserStartedText = true;
                    transition.ShowRestartReady = true;
                    transition.HideDownloading = true;
                    break;

                case UpdateState.Finished:
                    transition.CompleteUserFlow = true;
                    transition.ShowNoUpdatesByUserRequest = updateCheckByUser && !restartReadyVisible && !downloadingVisible;
                    break;

                case UpdateState.Error:
                    transition.CompleteUserFlow = true;
                    transition.ShowErrorByUserRequest = updateCheckByUser;
                    break;
            }

            return transition;
        }
    }
}
