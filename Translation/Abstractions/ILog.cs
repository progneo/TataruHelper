// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: http://www.viva64.com

using System.Runtime.CompilerServices;

namespace Translation.Abstractions
{
    public interface ILog
    {
        void WriteLog(string InputString, [CallerMemberName] string memberName = "",
            [CallerLineNumber] int sourceLineNumber = 0);
    }
}