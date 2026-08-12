using ETS2LA.Shared;

namespace LocalizationLibrary;

public class LocalizationLibrary : LibraryPlugin
{
    public override PluginInformation Info => new PluginInformation
    {
        Id = "srlily.i18n.library",
        Version = "1.1.2",
        Name = "LocalizationLibrary",
        Description = "Translation engine for ETS2LA. Loads language packs and translates UI strings.",
        AuthorName = "Srlily",
        SupportedETS2LA = "3.4.37",
    };
}
