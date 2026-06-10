using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

using Translation.Credentials;
using Translation.Models;

namespace FFXIVTataruHelper.ViewModel
{
    public sealed class TranslationCredentialsViewModel : INotifyPropertyChanged
    {
        private static readonly TranslationEngineName[] EngineUiOrder =
        {
            TranslationEngineName.GoogleTranslate, TranslationEngineName.Papago,
            TranslationEngineName.AzureTranslator, TranslationEngineName.GoogleCloudTranslate,
            TranslationEngineName.DeepLApi, TranslationEngineName.OpenAI, TranslationEngineName.DeepSeek,
            TranslationEngineName.YandexGPT, TranslationEngineName.Yandex
        };

        private readonly ITranslationCredentialStore _store;
        private readonly ObservableCollection<TranslationEngineName> _availableEngines;

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler AvailableEnginesChanged;

        public TranslationCredentialsViewModel(ITranslationCredentialStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _availableEngines = new ObservableCollection<TranslationEngineName>();
            AvailableEngines = new ReadOnlyObservableCollection<TranslationEngineName>(_availableEngines);
            RefreshAvailableEngines();
        }

        public ReadOnlyObservableCollection<TranslationEngineName> AvailableEngines { get; }

        public bool IsGoogleTranslateEnabled
        {
            get => _store.IsEngineEnabled(TranslationEngineName.GoogleTranslate);
            set => SetEngineEnabled(TranslationEngineName.GoogleTranslate, value);
        }

        public bool IsPapagoEnabled
        {
            get => _store.IsEngineEnabled(TranslationEngineName.Papago);
            set => SetEngineEnabled(TranslationEngineName.Papago, value);
        }

        public bool IsAzureEnabled
        {
            get => _store.IsEngineEnabled(TranslationEngineName.AzureTranslator);
            set => SetEngineEnabled(TranslationEngineName.AzureTranslator, value);
        }

        public bool IsGoogleCloudEnabled
        {
            get => _store.IsEngineEnabled(TranslationEngineName.GoogleCloudTranslate);
            set => SetEngineEnabled(TranslationEngineName.GoogleCloudTranslate, value);
        }

        public bool IsDeepLApiEnabled
        {
            get => _store.IsEngineEnabled(TranslationEngineName.DeepLApi);
            set => SetEngineEnabled(TranslationEngineName.DeepLApi, value);
        }

        public bool IsOpenAIEnabled
        {
            get => _store.IsEngineEnabled(TranslationEngineName.OpenAI);
            set => SetEngineEnabled(TranslationEngineName.OpenAI, value);
        }

        public bool IsDeepSeekEnabled
        {
            get => _store.IsEngineEnabled(TranslationEngineName.DeepSeek);
            set => SetEngineEnabled(TranslationEngineName.DeepSeek, value);
        }

        public bool IsYandexEnabled
        {
            get => _store.IsEngineEnabled(TranslationEngineName.Yandex);
            set => SetEngineEnabled(TranslationEngineName.Yandex, value);
        }

        public bool IsYandexGptEnabled
        {
            get => _store.IsEngineEnabled(TranslationEngineName.YandexGPT);
            set => SetEngineEnabled(TranslationEngineName.YandexGPT, value);
        }

        public string YandexGptModel
        {
            get => _store.GetModel(TranslationEngineName.YandexGPT);
            set => SetModel(TranslationEngineName.YandexGPT, value, nameof(YandexGptModel));
        }

        public bool ShowAzureSettings => IsAzureEnabled;
        public bool ShowGoogleCloudSettings => IsGoogleCloudEnabled;
        public bool ShowDeepLApiSettings => IsDeepLApiEnabled;
        public bool ShowOpenAISettings => IsOpenAIEnabled;
        public bool ShowDeepSeekSettings => IsDeepSeekEnabled;
        public bool ShowYandexSettings => IsYandexEnabled;
        public bool ShowYandexGptSettings => IsYandexGptEnabled;

        public string AzureApiKey
        {
            get => _store.GetApiKey(TranslationEngineName.AzureTranslator);
            set => SetApiKey(TranslationEngineName.AzureTranslator, value, nameof(AzureApiKey),
                nameof(AzureApiKeyMasked));
        }

        public string AzureApiKeyMasked => MaskSecret(AzureApiKey);

        public string AzureRegion
        {
            get => _store.GetRegion(TranslationEngineName.AzureTranslator);
            set => SetRegion(TranslationEngineName.AzureTranslator, value, nameof(AzureRegion));
        }

        public string GoogleCloudApiKey
        {
            get => _store.GetApiKey(TranslationEngineName.GoogleCloudTranslate);
            set => SetApiKey(
                TranslationEngineName.GoogleCloudTranslate,
                value,
                nameof(GoogleCloudApiKey),
                nameof(GoogleCloudApiKeyMasked));
        }

        public string GoogleCloudApiKeyMasked => MaskSecret(GoogleCloudApiKey);

        public string DeepLApiKey
        {
            get => _store.GetApiKey(TranslationEngineName.DeepLApi);
            set => SetApiKey(TranslationEngineName.DeepLApi, value, nameof(DeepLApiKey), nameof(DeepLApiKeyMasked));
        }

        public string DeepLApiKeyMasked => MaskSecret(DeepLApiKey);

        public string OpenAIApiKey
        {
            get => _store.GetApiKey(TranslationEngineName.OpenAI);
            set => SetApiKey(TranslationEngineName.OpenAI, value, nameof(OpenAIApiKey), nameof(OpenAIApiKeyMasked));
        }

        public string OpenAIApiKeyMasked => MaskSecret(OpenAIApiKey);

        public string OpenAIModel
        {
            get => _store.GetModel(TranslationEngineName.OpenAI);
            set => SetModel(TranslationEngineName.OpenAI, value, nameof(OpenAIModel));
        }

        public string DeepSeekApiKey
        {
            get => _store.GetApiKey(TranslationEngineName.DeepSeek);
            set => SetApiKey(
                TranslationEngineName.DeepSeek,
                value,
                nameof(DeepSeekApiKey),
                nameof(DeepSeekApiKeyMasked));
        }

        public string DeepSeekApiKeyMasked => MaskSecret(DeepSeekApiKey);

        public string DeepSeekModel
        {
            get => _store.GetModel(TranslationEngineName.DeepSeek);
            set => SetModel(TranslationEngineName.DeepSeek, value, nameof(DeepSeekModel));
        }

        public string YandexApiKey
        {
            get => _store.GetApiKey(TranslationEngineName.Yandex);
            set => SetApiKey(TranslationEngineName.Yandex, value, nameof(YandexApiKey), nameof(YandexApiKeyMasked));
        }

        public string YandexApiKeyMasked => MaskSecret(YandexApiKey);

        public string YandexFolderId
        {
            get => _store.GetRegion(TranslationEngineName.Yandex);
            set => SetRegion(TranslationEngineName.Yandex, value, nameof(YandexFolderId));
        }

        public bool IsEngineEnabled(TranslationEngineName engine) => _availableEngines.Contains(engine);

        private void SetApiKey(TranslationEngineName engine, string value, params string[] propsToNotify)
        {
            _store.SetApiKey(engine, value);
            _store.Save();
            NotifyMany(propsToNotify);
        }

        private void SetRegion(TranslationEngineName engine, string value, params string[] propsToNotify)
        {
            _store.SetRegion(engine, value);
            _store.Save();
            NotifyMany(propsToNotify);
        }

        private void SetModel(TranslationEngineName engine, string value, params string[] propsToNotify)
        {
            _store.SetModel(engine, value);
            _store.Save();
            NotifyMany(propsToNotify);
        }

        private void SetEngineEnabled(TranslationEngineName engine, bool isEnabled)
        {
            if (_store.IsEngineEnabled(engine) == isEnabled) return;

            if (!isEnabled && EnabledEngineCount() <= 1) return;

            _store.SetEngineEnabled(engine, isEnabled);
            _store.Save();

            OnPropertyChanged(EngineTogglePropertyName(engine));
            OnPropertyChanged(EngineSettingsVisibilityPropertyName(engine));
            RefreshAvailableEngines();
        }

        private static string EngineTogglePropertyName(TranslationEngineName engine) => engine switch
        {
            TranslationEngineName.GoogleTranslate => nameof(IsGoogleTranslateEnabled),
            TranslationEngineName.Papago => nameof(IsPapagoEnabled),
            TranslationEngineName.AzureTranslator => nameof(IsAzureEnabled),
            TranslationEngineName.GoogleCloudTranslate => nameof(IsGoogleCloudEnabled),
            TranslationEngineName.DeepLApi => nameof(IsDeepLApiEnabled),
            TranslationEngineName.OpenAI => nameof(IsOpenAIEnabled),
            TranslationEngineName.DeepSeek => nameof(IsDeepSeekEnabled),
            TranslationEngineName.Yandex => nameof(IsYandexEnabled),
            TranslationEngineName.YandexGPT => nameof(IsYandexGptEnabled),
            _ => string.Empty
        };

        private static string EngineSettingsVisibilityPropertyName(TranslationEngineName engine) => engine switch
        {
            TranslationEngineName.AzureTranslator => nameof(ShowAzureSettings),
            TranslationEngineName.GoogleCloudTranslate => nameof(ShowGoogleCloudSettings),
            TranslationEngineName.DeepLApi => nameof(ShowDeepLApiSettings),
            TranslationEngineName.OpenAI => nameof(ShowOpenAISettings),
            TranslationEngineName.DeepSeek => nameof(ShowDeepSeekSettings),
            TranslationEngineName.Yandex => nameof(ShowYandexSettings),
            TranslationEngineName.YandexGPT => nameof(ShowYandexGptSettings),
            _ => string.Empty
        };

        private int EnabledEngineCount() => EngineUiOrder.Count(engine => _store.IsEngineEnabled(engine));

        private void RefreshAvailableEngines()
        {
            var desired = EngineUiOrder.Where(engine => _store.IsEngineEnabled(engine)).ToList();
            if (_availableEngines.SequenceEqual(desired)) return;

            _availableEngines.Clear();
            foreach (var engine in desired)
            {
                _availableEngines.Add(engine);
            }

            AvailableEnginesChanged?.Invoke(this, EventArgs.Empty);
        }

        private static string MaskSecret(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "(not set)";
            if (value.Length <= 4) return new string('*', value.Length);
            return new string('*', value.Length - 4) + value.Substring(value.Length - 4);
        }

        private void NotifyMany(params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames) OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            if (string.IsNullOrWhiteSpace(propertyName)) return;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}