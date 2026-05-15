using NUnit.Framework;
using Updater.EventArguments;

namespace FFXIVTataruHelper.Tests
{
    [TestFixture]
    public class UpdateUiStateMapperTests
    {
        [Test]
        public void Map_Initializing_DisablesCheckButton()
        {
            var transition = UpdateUiStateMapper.Map(UpdateState.Initializing, true, false, false);

            Assert.That(transition.DisableCheckButton, Is.True);
            Assert.That(transition.CompleteUserFlow, Is.False);
        }

        [Test]
        public void Map_Finished_UserRequestWithoutUpdate_ShowsNoUpdatesMessage()
        {
            var transition = UpdateUiStateMapper.Map(UpdateState.Finished, true, false, false);

            Assert.That(transition.CompleteUserFlow, Is.True);
            Assert.That(transition.ShowNoUpdatesByUserRequest, Is.True);
        }

        [Test]
        public void Map_Error_UserRequest_ShowsErrorMessage()
        {
            var transition = UpdateUiStateMapper.Map(UpdateState.Error, true, false, false);

            Assert.That(transition.CompleteUserFlow, Is.True);
            Assert.That(transition.ShowErrorByUserRequest, Is.True);
        }

        [Test]
        public void Map_ReadyToRestart_ShowsRestartAndHidesDownload()
        {
            var transition = UpdateUiStateMapper.Map(UpdateState.ReadyToRestart, true, true, true);

            Assert.That(transition.ShowRestartReady, Is.True);
            Assert.That(transition.HideDownloading, Is.True);
            Assert.That(transition.HideUserStartedText, Is.True);
        }
    }
}
