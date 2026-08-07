using System.Collections.Generic;

namespace FFXIVTataruHelper.ViewModel
{
    /// <summary>
    /// Picks the number a new chat window is known by.
    ///
    /// Everything else finds a window by that number - the sidebar, the delete
    /// button, and the binding that ties a window to its settings - so two
    /// windows sharing one is not a cosmetic problem. A window that shares its
    /// number with another cannot be selected or deleted, because both
    /// searches stop at the first match, and settings arriving later for that
    /// number are dropped as already present.
    /// </summary>
    public static class ChatWindowIds
    {
        /// <summary>
        /// One past the highest in use, rather than one past the last in the
        /// list. The list is not ordered by number: delete the middle window
        /// and add another, and "one past the last" hands out a number that is
        /// already taken.
        /// </summary>
        public static long Next(IEnumerable<long> inUse)
        {
            var highest = -1L;

            foreach (var id in inUse)
            {
                if (id > highest)
                {
                    highest = id;
                }
            }

            return highest + 1;
        }
    }
}
