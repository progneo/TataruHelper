using System;
using System.IO;
using System.Windows;

using NGettext;

namespace FFXIVTataruHelper
{
    public class LanguageWrapper
    {
        Window _SettingsWindow;

        public enum Languages : int
        {
            None = 0,
            Russian = 1,
            English = 2,
            Spanish = 3,
            Polish = 4,
            Korean = 5,
            PortugueseBR = 6,
            Catalan = 7,
            Italian = 8,
            Ukrainian = 9,
            Chinese = 10,
            ChineseTR = 11,
            Japanese = 12,
        };

        public Languages CurrentLanguage
        {
            get { return _CurrentLanguage; }

            set
            {
                if (value != _CurrentLanguage || true)
                {
                    SetLanguage(value);

                    _CurrentLanguage = value;
                }
            }
        }

        Languages _CurrentLanguage;

        readonly AppSettings _AppSettings;
        readonly string _DirPath;

        public LanguageWrapper(AppSettings appSettings)
        {
            _AppSettings = appSettings ?? new AppSettings();
            _DirPath = _AppSettings.LocalisationDirPath;
            _CurrentLanguage = Languages.None;
        }

        public void Attach(Window window)
        {
            _SettingsWindow = window;
        }

        private void SetLanguage(Languages language)
        {
            if (_SettingsWindow == null)
                return;

            string path = _DirPath;

            if (language != Languages.None)
            {
                switch (language)
                {
                    case Languages.English:
                        path += _AppSettings.en_US_LanguaguePath;
                        break;

                    case Languages.Russian:
                        path += _AppSettings.ru_RU_LanguaguePath;
                        break;

                    case Languages.Spanish:
                        path += _AppSettings.es_ES_LanguaguePath;
                        break;

                    case Languages.Polish:
                        path += _AppSettings.pl_PL_LanguaguePath;
                        break;

                    case Languages.Korean:
                        path += _AppSettings.ko_KR_LanguaguePath;
                        break;

                    case Languages.PortugueseBR:
                        path += _AppSettings.pt_BR_LanguaguePath;
                        break;
                    case Languages.Catalan:
                        path += _AppSettings.ca_Es_LanguaguePath;
                        break;
                    case Languages.Italian:
                        path += _AppSettings.it_IT_LanguaguePath;
                        break;
                    case Languages.Ukrainian:
                        path += _AppSettings.uk_UA_LanguaguePath;
                        break;
                    case Languages.Chinese:
                        path += _AppSettings.zh_CN_LanguaguePath;
                        break;
                    case Languages.ChineseTR:
                        path += _AppSettings.zh_TR_LanguaguePath;
                        break;
                    case Languages.Japanese:
                        path += _AppSettings.ja_LanguaguePath;
                        break;

                    default:
                        path += _AppSettings.en_US_LanguaguePath;
                        break;
                }

                LoadDynamicTranslation(path, language);
            }
        }


        private void LoadDynamicTranslation(string path, Languages requestedLanguage)
        {
            ICatalog catalog = new Catalog();
            try
            {
                using (var fs = File.Open(path, FileMode.Open))
                {
                    catalog = new Catalog(fs);
                }
            }
            catch (Exception e)
            {
                Logger.WriteLog(Convert.ToString(e));
            }

            _SettingsWindow.Resources["SettingsWindowName"] = catalog.GetString("Settings");
            _SettingsWindow.Resources["TrayTooltipText"] = catalog.GetString("Tataru Helper");
            _SettingsWindow.Resources["ChatWindowName"] = catalog.GetString("Chat Window");
            _SettingsWindow.Resources["StreamWindowName"] = catalog.GetString("Stream Chat Window");

            _SettingsWindow.Resources["DocLanguage"] = catalog.GetString("Language");
            //_SettingsWindow.Resources["DocLanguageEn"] = catalog.GetString("English");
            //_SettingsWindow.Resources["DocLanguageRu"] = catalog.GetString("Russian");

            _SettingsWindow.Resources["DockHelp"] = catalog.GetString("Help");
            _SettingsWindow.Resources["DockAbout"] = catalog.GetString("About");
            _SettingsWindow.Resources["ChatWindowsTab"] = catalog.GetString("Chat Windows");

            _SettingsWindow.Resources["ChatAppearance"] = catalog.GetString("Chat Appearance");
            _SettingsWindow.Resources["TranslationSettings"] = catalog.GetString("Translation Settings");
            _SettingsWindow.Resources["GeneralBehavior"] = catalog.GetString("General");
            _SettingsWindow.Resources["SectionTranslation"] = catalog.GetString("Translation");
            _SettingsWindow.Resources["SectionAppearance"] = catalog.GetString("Appearance");
            _SettingsWindow.Resources["SectionGeneral"] = catalog.GetString("General");
            _SettingsWindow.Resources["SidebarGroupChatWindows"] = catalog.GetString("Chat Windows");
            _SettingsWindow.Resources["SidebarGroupPerWindow"] = catalog.GetString("Per Window Settings");
            _SettingsWindow.Resources["SidebarGroupApplication"] = catalog.GetString("Application");
            _SettingsWindow.Resources["ActiveChatWindowLabel"] = catalog.GetString("Active Chat Window");
            _SettingsWindow.Resources["AddButtonText"] = catalog.GetString("Add");
            _SettingsWindow.Resources["DeleteButtonText"] = catalog.GetString("Delete");
            _SettingsWindow.Resources["ShowHideButtonText"] = catalog.GetString("Show/Hide");
            _SettingsWindow.Resources["ResetButtonText"] = catalog.GetString("Reset");
            _SettingsWindow.Resources["WindowListLabel"] = catalog.GetString("Window list");
            _SettingsWindow.Resources["WindowDetailsLabel"] = catalog.GetString("Window details");
            _SettingsWindow.Resources["WindowNameLabel"] = catalog.GetString("Window name");
            _SettingsWindow.Resources["ChatCodeFiltersLabel"] = catalog.GetString("Chat code filters");
            _SettingsWindow.Resources["EngineRoutingLabel"] = catalog.GetString("Engine routing");
            _SettingsWindow.Resources["TypographyLabel"] = catalog.GetString("Typography");
            _SettingsWindow.Resources["WindowSurfaceLabel"] = catalog.GetString("Window surface");
            _SettingsWindow.Resources["ShortcutCaptureLabel"] = catalog.GetString("Shortcut capture");
            _SettingsWindow.Resources["OverlayBehaviorLabel"] = catalog.GetString("Overlay behavior");
            _SettingsWindow.Resources["ApplicationFlagsLabel"] = catalog.GetString("Application flags");
            _SettingsWindow.Resources["ForkRepositoryLabel"] = catalog.GetString("Fork repository:");
            _SettingsWindow.Resources["OriginalRepositoryLabel"] = catalog.GetString("Original repository:");
            _SettingsWindow.Resources["CommunityDiscordLabel"] = catalog.GetString("Community Discord:");

            _SettingsWindow.Resources["ChatWindowBehavior"] = catalog.GetString("Behavior");
            _SettingsWindow.Resources["ChatWindowHotkeys"] = catalog.GetString("Hotkeys");

            _SettingsWindow.Resources["FontSettings"] = catalog.GetString("Font Settings");
            _SettingsWindow.Resources["FontSize"] = catalog.GetString("Font Size");

            _SettingsWindow.Resources["FontColor"] = catalog.GetString("Font Color");
            _SettingsWindow.Resources["FontColor1"] = catalog.GetString("Color 1");
            _SettingsWindow.Resources["FontColor2"] = catalog.GetString("Color 2");

            _SettingsWindow.Resources["FontFamily"] = catalog.GetString("Font");

            _SettingsWindow.Resources["BackgroundColor"] = catalog.GetString("Background Color");

            _SettingsWindow.Resources["ColorPickerStandardButtonHeader"] = catalog.GetString("Standard");
            _SettingsWindow.Resources["ColorPickerAdvancedButtonHeader"] = catalog.GetString("Advanced");
            _SettingsWindow.Resources["ColorPickerAvailableColorsHeader"] = catalog.GetString("Available Colors");
            _SettingsWindow.Resources["ColorPickerStandardColorsHeader"] = catalog.GetString("Standard Colors");
            _SettingsWindow.Resources["ColorPickerRecentColorsHeader"] = catalog.GetString("Recent Colors");

            _SettingsWindow.Resources["ParagraphSettings"] = catalog.GetString("Paragraph Settings");
            _SettingsWindow.Resources["IntervalWidth"] = catalog.GetString("Spacing");
            _SettingsWindow.Resources["LineBreakHeight"] = catalog.GetString("Line Break Height");
            _SettingsWindow.Resources["WindowCornerRadiusLabel"] = catalog.GetString("Corner radius");
            _SettingsWindow.Resources["ContentPaddingLabel"] = catalog.GetString("Content padding");
            _SettingsWindow.Resources["MessagesInContainerLabel"] = catalog.GetString("Messages in container");
            _SettingsWindow.Resources["MessageContainerPaddingLabel"] = catalog.GetString("Message container padding");
            _SettingsWindow.Resources["MessageContainerAlphaLabel"] = catalog.GetString("Message container alpha");
            _SettingsWindow.Resources["MessageContainerBorderThicknessLabel"] =
                catalog.GetString("Message border thickness");
            _SettingsWindow.Resources["MessageContainerBorderAlphaLabel"] = catalog.GetString("Message border alpha");
            _SettingsWindow.Resources["ShowOnlyLastMessageLabel"] = catalog.GetString("Show only last message");

            _SettingsWindow.Resources["ChatCodes"] = catalog.GetString("Chat Codes");

            _SettingsWindow.Resources["TranslationEngine"] = catalog.GetString("Translation Engine");
            _SettingsWindow.Resources["FFLanguage"] = catalog.GetString("FF Language");
            _SettingsWindow.Resources["TraslateTo"] = catalog.GetString("Translate to");

            _SettingsWindow.Resources["ShowHideChatWindowHK"] = catalog.GetString("Show/hide Chat Window");
            _SettingsWindow.Resources["ShowHideChatWindowHKToolTip"] = catalog.GetString(
                "Hotkey to hide a Chat Box when it is not needed and call it up when it is needed. You should use Ctrl/Shift/Alt + Any key or combination of Ctrl+Shift+Alt + Any key, etc. The key combinations must not be repeated. If you cannot assign a key, it is occupied by the application or system. Example: CTRL+Q, CTRL+ALT+R, SHIFT+ALT+CTRL+T, ALT+SHIFT+Y, SHIFT+CTRL+G, etc.");

            _SettingsWindow.Resources["ClickThroughHK"] = catalog.GetString("Click Through");
            _SettingsWindow.Resources["ClickThroughHKToolTip"] = catalog.GetString(
                "Hotkey to turn on/off clicks through the windows. You should use Ctrl/Shift/Alt + Any key or combination of Ctrl+Shift+Alt + Any key, etc. The key combinations must not be repeated. If you cannot assign a key, it is occupied by the application or system. Example: CTRL+Q, CTRL+ALT+R, SHIFT+ALT+CTRL+T, ALT+SHIFT+Y, SHIFT+CTRL+G, etc.");

            _SettingsWindow.Resources["ClearChatHK"] = catalog.GetString("Clear Chat");
            _SettingsWindow.Resources["ClearChatHKToolTip"] = catalog.GetString(
                "Hotkey to clear any text in the chatbox. You should use Ctrl/Shift/Alt + Any key or combination of Ctrl+Shift+Alt + Any key, etc. The key combinations must not be repeated. If you cannot assign a key, it is occupied by the application or system. Example: CTRL+Q, CTRL+ALT+R, SHIFT+ALT+CTRL+T, ALT+SHIFT+Y, SHIFT+CTRL+G, etc.");

            _SettingsWindow.Resources["OtherSett"] = catalog.GetString("Other");
            _SettingsWindow.Resources["ClickThroughCB"] = catalog.GetString("Click Through");
            _SettingsWindow.Resources["AlwaysOnTopCB"] = catalog.GetString("Always On Top");
            _SettingsWindow.Resources["AutoHideCB"] = catalog.GetString("Auto Hide");

            _SettingsWindow.Resources["AlwaysOnTopCB"] = catalog.GetString("Always On Top");
            _SettingsWindow.Resources["HideToTrayCB"] = catalog.GetString("Hide to Tray");
            _SettingsWindow.Resources["ShowTimestampsCb"] = catalog.GetString("Show Timestamps");

            _SettingsWindow.Resources["StreamerWindowCB"] = catalog.GetString("Streamer Window");

            _SettingsWindow.Resources["ShowChatBox"] = catalog.GetString("Show Chat Window");
            _SettingsWindow.Resources["HideChatBox"] = catalog.GetString("Hide Chat Window");

            _SettingsWindow.Resources["ResetChatPosition"] = catalog.GetString("Reset Chat Position");

            _SettingsWindow.Resources["TranslationEngineError"] = catalog.GetString("Translation failed:");

            _SettingsWindow.Resources["FFStatusLable"] = catalog.GetString("FF Status:");

            var ffNotFound = catalog.GetString("Not found");
            if (string.Equals(ffNotFound, "Not found", StringComparison.Ordinal) &&
                requestedLanguage == Languages.Russian)
            {
                ffNotFound = "Не найден";
            }

            _SettingsWindow.Resources["FFStatusText"] = ffNotFound;

            _SettingsWindow.Resources["FFStatusTextFound"] = catalog.GetString("Process found:");

            _SettingsWindow.Resources["DearPatrons"] = catalog.GetString("Dear Patrons!");
            _SettingsWindow.Resources["PatronsMsg"] = catalog.GetString(
                "We express our great appreciation to the people who support our project and motivate us for new achievements.");
            _SettingsWindow.Resources["PatronsThankYou"] = catalog.GetString("Thank you");

            _SettingsWindow.Resources["CkSystem"] = catalog.GetString("System");
            _SettingsWindow.Resources["CkEcho"] = catalog.GetString("Echo");
            _SettingsWindow.Resources["CkError"] = catalog.GetString("Error");
            _SettingsWindow.Resources["CkNPCD"] = catalog.GetString("NPCD");
            _SettingsWindow.Resources["CkNPCA"] = catalog.GetString("NPCA");
            _SettingsWindow.Resources["CkNPCDRealtime"] = catalog.GetString("NPCD Realtime");
            _SettingsWindow.Resources["CkNPCARealtime"] = catalog.GetString("NPCA Realtime");
            _SettingsWindow.Resources["CkRecruitment"] = catalog.GetString("Recruitment");
            _SettingsWindow.Resources["CkSay"] = catalog.GetString("Say");
            _SettingsWindow.Resources["CkShout"] = catalog.GetString("Shout");
            _SettingsWindow.Resources["CkParty"] = catalog.GetString("Party");
            _SettingsWindow.Resources["CkTell"] = catalog.GetString("Tell");
            _SettingsWindow.Resources["CkFreeCompany"] = catalog.GetString("FreeCompany");
            _SettingsWindow.Resources["CkYell"] = catalog.GetString("Yell");
            _SettingsWindow.Resources["CkAlliance"] = catalog.GetString("Alliance");
            _SettingsWindow.Resources["CkLinkShell1"] = catalog.GetString("LinkShell1");
            _SettingsWindow.Resources["CkLinkShell2"] = catalog.GetString("LinkShell2");
            _SettingsWindow.Resources["CkLinkShell3"] = catalog.GetString("LinkShell3");
            _SettingsWindow.Resources["CkLinkShell4"] = catalog.GetString("LinkShell4");
            _SettingsWindow.Resources["CkLinkShell5"] = catalog.GetString("LinkShell5");
            _SettingsWindow.Resources["CkLinkShell6"] = catalog.GetString("LinkShell6");
            _SettingsWindow.Resources["CkLinkShell7"] = catalog.GetString("LinkShell7");
            _SettingsWindow.Resources["CkLinkShell8"] = catalog.GetString("LinkShell8");

            _SettingsWindow.Resources["CkNoviceNetwork"] = catalog.GetString("Novice Network");

            _SettingsWindow.Resources["CkServerInfo"] = catalog.GetString("Server Info");

            _SettingsWindow.Resources["CkCWLS1"] = catalog.GetString("CWLS1");
            _SettingsWindow.Resources["CkCWLS2"] = catalog.GetString("CWLS2");
            _SettingsWindow.Resources["CkCWLS3"] = catalog.GetString("CWLS3");
            _SettingsWindow.Resources["CkCWLS4"] = catalog.GetString("CWLS4");
            _SettingsWindow.Resources["CkCWLS5"] = catalog.GetString("CWLS5");
            _SettingsWindow.Resources["CkCWLS6"] = catalog.GetString("CWLS6");
            _SettingsWindow.Resources["CkCWLS7"] = catalog.GetString("CWLS7");
            _SettingsWindow.Resources["CkCWLS8"] = catalog.GetString("CWLS8");

            _SettingsWindow.Resources["CkEmotes"] = catalog.GetString("Emotes");
            _SettingsWindow.Resources["CkCustomEmotes"] = catalog.GetString("Custom Emotes");

            _SettingsWindow.Resources["DownloadingUpdate"] = catalog.GetString("Downloading new version:");
            _SettingsWindow.Resources["UpdateInstalled"] = catalog.GetString("Click here to update to new version.");

            _SettingsWindow.Resources["NotifyUpdateTitle"] = catalog.GetString("Tataru Update");
            _SettingsWindow.Resources["NotifyUpdateText"] =
                catalog.GetString("New Tataru helper version is available. Restart Application to update.");

            _SettingsWindow.Resources["CheckUpdatesText"] = catalog.GetString("Check updates");
            _SettingsWindow.Resources["LookingForUpdates"] = catalog.GetString("Looking for updates...");
            _SettingsWindow.Resources["NoUpdatesFound"] = catalog.GetString("No updates found.");

            // Tray / dock
            _SettingsWindow.Resources["SettingsHide"] = catalog.GetString("Hide");
            _SettingsWindow.Resources["SettingsExit"] = catalog.GetString("Exit");
            _SettingsWindow.Resources["PatreonWindowName"] = catalog.GetString("Patrons");
            _SettingsWindow.Resources["DockPatrons"] = catalog.GetString("Patrons");
            _SettingsWindow.Resources["DockDiscrod"] = catalog.GetString("Discord");
            _SettingsWindow.Resources["DockModernSettingsPreview"] = catalog.GetString("Modern settings (preview)");

            // Page descriptions
            _SettingsWindow.Resources["ChatWindowsPageDescription"] =
                catalog.GetString("Manage your in-game chat overlay windows. Each tab is an independent overlay.");
            _SettingsWindow.Resources["ChatWindowsTabHint"] =
                catalog.GetString(
                    "Switch tabs to edit a window's translation, appearance, hotkeys, and behavior in the other sections.");
            _SettingsWindow.Resources["TranslationPageDescription"] =
                catalog.GetString("Engine and language routing apply only to the selected chat window.");
            _SettingsWindow.Resources["ChatCodeFiltersHint"] =
                catalog.GetString("Pick which chat channels are translated and the colour each one is rendered in.");
            _SettingsWindow.Resources["EngineRoutingHint"] =
                catalog.GetString("Choose the translation provider and source/target languages.");
            _SettingsWindow.Resources["AppearancePageDescription"] =
                catalog.GetString("Customize how the overlay looks and how text is laid out.");
            _SettingsWindow.Resources["HotkeysPageDescription"] =
                catalog.GetString("Record system-wide shortcuts and control how the overlay reacts.");
            _SettingsWindow.Resources["GeneralPageDescription"] =
                catalog.GetString("Application-wide options that affect every chat window.");
            _SettingsWindow.Resources["AboutPageDescription"] = catalog.GetString("Project links and credits.");

            // Theme group
            _SettingsWindow.Resources["ThemeGroupTitle"] = catalog.GetString("Theme");
            _SettingsWindow.Resources["ThemeGroupHint"] =
                catalog.GetString("Choose how the settings window and overlay should be coloured.");
            _SettingsWindow.Resources["ThemeSystem"] = catalog.GetString("Use system theme");
            _SettingsWindow.Resources["ThemeLight"] = catalog.GetString("Light");
            _SettingsWindow.Resources["ThemeDark"] = catalog.GetString("Dark");

            // Hotkeys / general
            _SettingsWindow.Resources["HotkeyRecordHint"] = catalog.GetString("Press a key combination…");
            _SettingsWindow.Resources["HotkeyClear"] = catalog.GetString("Clear");
            _SettingsWindow.Resources["HideToTrayDescription"] =
                catalog.GetString("Hide the settings window to the system tray when minimized.");
            _SettingsWindow.Resources["LanguageGroupHint"] =
                catalog.GetString("Interface language for the settings window and overlays.");

            // Updates group
            _SettingsWindow.Resources["UpdatesGroupTitle"] = catalog.GetString("Updates");
            _SettingsWindow.Resources["UpdatesGroupHint"] =
                catalog.GetString("Check whether a newer release of Tataru Helper is available.");
            _SettingsWindow.Resources["UpdateAvailableChip"] = catalog.GetString("Update available");

            // Chat code (missed)
            _SettingsWindow.Resources["CkBossQuotes"] = catalog.GetString("Boss quotes");

            // Translator catalogue (General page)
            _SettingsWindow.Resources["TranslatorsLabel"] = catalog.GetString("Translators");
            _SettingsWindow.Resources["TranslatorsHint"] =
                catalog.GetString(
                    "Enable only translators you need. Disabled translators are hidden on the Translation page.");
            _SettingsWindow.Resources["FreeTranslatorsLabel"] = catalog.GetString("Free");
            _SettingsWindow.Resources["PaidTranslatorsLabel"] = catalog.GetString("Paid");
            _SettingsWindow.Resources["AITranslatorsLabel"] = catalog.GetString("AI");
            _SettingsWindow.Resources["AzureKeyLabel"] = catalog.GetString("Azure key");
            _SettingsWindow.Resources["AzureRegionLabel"] = catalog.GetString("Azure region");
            _SettingsWindow.Resources["GoogleCloudKeyLabel"] = catalog.GetString("Google Cloud key");
            _SettingsWindow.Resources["DeepLApiKeyLabel"] = catalog.GetString("DeepL API key");
            _SettingsWindow.Resources["YandexApiKeyLabel"] = catalog.GetString("Yandex API key");
            _SettingsWindow.Resources["YandexFolderIdLabel"] = catalog.GetString("Yandex folder ID");
            _SettingsWindow.Resources["OpenAIKeyLabel"] = catalog.GetString("OpenAI key");
            _SettingsWindow.Resources["OpenAIModelLabel"] = catalog.GetString("OpenAI model (optional)");
            _SettingsWindow.Resources["DeepSeekKeyLabel"] = catalog.GetString("DeepSeek key");
            _SettingsWindow.Resources["DeepSeekModelLabel"] = catalog.GetString("DeepSeek model (optional)");
            _SettingsWindow.Resources["YandexGptUsesYandexHint"] =
                catalog.GetString("Uses the Yandex Cloud API key and folder ID from the Yandex row above.");
            _SettingsWindow.Resources["YandexGptModelAliasLabel"] =
                catalog.GetString("Model alias (optional, e.g. yandexgpt/latest, yandexgpt-lite/latest)");

            // About / Appearance
            _SettingsWindow.Resources["AppTagline"] = catalog.GetString("Real-time FFXIV chat translator");
            _SettingsWindow.Resources["TypographyHint"] =
                catalog.GetString("Choose text style and readability defaults for this overlay window.");
            _SettingsWindow.Resources["OverlayBehaviorHint"] =
                catalog.GetString("Configure how the chat overlay behaves in game.");
            _SettingsWindow.Resources["AutoHideTimeoutLabel"] = catalog.GetString("Auto Hide Timeout");
            _SettingsWindow.Resources["AutoHideTimeoutHint"] = catalog.GetString("Timeout in seconds.");
        }
    }
}