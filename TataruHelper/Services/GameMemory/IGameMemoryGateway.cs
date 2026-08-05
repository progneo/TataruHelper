using Sharlayan.Core;
using Sharlayan.Models;
using Sharlayan.Models.ReadResults;

namespace FFXIVTataruHelper.Services.GameMemory
{
    public interface IGameMemoryGateway
    {
        void SetProcess(ProcessModel processModel, string gameLanguage, string patchVersion, bool useLocalCache, bool scanAllMemoryRegions);

        void UnsetProcess();

        ChatLogResult GetChatLog(int previousArrayIndex, int previousOffset);

        ChatLogResult GetDirectDialog();

        bool CheckChatEquality(ChatLogItem item1, ChatLogItem item2);



        /// <summary>


        /// The character's own name, empty until they are loaded. The game writes it


        /// into lines addressed to them, so a hand-made translation of such a line


        /// cannot be recognised without it.


        /// </summary>


        string GetPlayerName();
    }
}
