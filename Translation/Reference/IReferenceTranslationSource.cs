namespace Translation.Reference
{
    /// <summary>
    /// A translation somebody has already made by hand, looked up rather than
    /// produced.
    ///
    /// The game's own dialogue has been translated by the xivrus project, and
    /// for a line that is in there no machine is going to do better - nor as
    /// quickly, nor without asking a service for it.
    /// </summary>
    public interface IReferenceTranslationSource
    {
        /// <summary>The language these translations are in, or empty when none loaded.</summary>
        string LanguageCode { get; }

        /// <summary>
        /// The character's name, which the game writes into lines addressed to
        /// them. Until it is known those lines cannot be recognised, since what
        /// is stored has the name punched out and what is read has it filled in.
        /// </summary>
        string PlayerName { get; set; }

        bool TryGetTranslation(string sentence, out string translation);
    }
}
