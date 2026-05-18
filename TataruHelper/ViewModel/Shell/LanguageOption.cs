namespace FFXIVTataruHelper.ViewModel.Shell;

public sealed class LanguageOption
{
    public LanguageOption(string value, string title)
    {
        Value = value;
        Title = title;
    }

    public string Value { get; }

    public string Title { get; }
}