namespace FFXIVTataruHelper.Services.GameMemory
{
    /// <summary>
    /// Where one of the game's own windows stands, in the coordinates the
    /// client draws in: the top-left corner and the size it is drawn at.
    ///
    /// Wanted so a translation can be put where the line it replaces already
    /// is, instead of in a window off to one side. The player moves the game's
    /// dialogue box and scales the interface as they like, and both are read
    /// from the client rather than guessed or configured.
    /// </summary>
    internal readonly struct AddonBounds
    {
        public static AddonBounds Unknown => default;

        private AddonBounds(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            IsKnown = true;
        }

        /// <summary>False when the client did not say, and nothing may be read off it.</summary>
        public bool IsKnown { get; }

        public float X { get; }

        public float Y { get; }

        public float Width { get; }

        public float Height { get; }

        /// <summary>
        /// A rectangle from what the client keeps: the window's position, the
        /// unscaled size of the node it draws into, and the interface scale.
        ///
        /// Unknown rather than a guess when any of it reads as nothing. A
        /// window of no size is one that was read wrongly or is being torn
        /// down, and putting a translation at its corner would drop the line in
        /// the top left of the screen - worse than leaving it where it was.
        /// </summary>
        public static AddonBounds From(short x, short y, ushort width, ushort height, float scale)
        {
            if (width == 0 || height == 0 || !(scale > 0f))
            {
                return Unknown;
            }

            return new AddonBounds(x, y, width * scale, height * scale);
        }
    }
}
