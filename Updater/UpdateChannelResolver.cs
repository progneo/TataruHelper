namespace Updater
{
    internal static class UpdateChannelResolver
    {
        public static string ResolveExplicitChannel(bool prerelease)
        {
            return prerelease ? "prerelease" : "stable";
        }
    }
}
