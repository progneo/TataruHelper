namespace Translation.Models
{
    public static class TranslationEngineDefaults
    {
        /// <summary>
        /// Whether an engine is offered before the user has said anything about
        /// it.
        ///
        /// Engines that need a server the user has to run themselves stay off.
        /// On by default they would sit in the picker pointing at nothing, and
        /// worse, be tried as a stand-in every time the selected engine fails -
        /// knocking on a port that nobody is listening on, on every line.
        ///
        /// A keyed engine does not have that problem: with no key it raises
        /// before it reaches the network, and the stand-in search skips it.
        /// </summary>
        public static bool IsOnByDefault(TranslationEngineName engine)
        {
            switch (engine)
            {
                case TranslationEngineName.Ollama:
                case TranslationEngineName.LmStudio:
                case TranslationEngineName.LibreTranslate:

                // Off for a different reason: as of 2026-08-10 the keyless page
                // answers 401 {"ShowCaptcha":false} to every translate call,
                // from this machine at least. The page still hands out session
                // values exactly as read here, so the engine is kept rather than
                // deleted - but on by default it would fetch a page and be
                // refused on every failed line, for nothing.
                case TranslationEngineName.BingFree:
                    return false;
                default:
                    return true;
            }
        }
    }
}
