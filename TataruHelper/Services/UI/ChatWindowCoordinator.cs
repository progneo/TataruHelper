using FFXIVTataruHelper.Factories;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.TataruComponentModel;
using FFXIVTataruHelper.UIModel;
using FFXIVTataruHelper.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Translation;

namespace FFXIVTataruHelper.Services.UI
{
    public sealed class ChatWindowCoordinator : IChatWindowCoordinator
    {
        private readonly List<PropertyBinder> _propertyBinders = new List<PropertyBinder>();
        private readonly List<ChatWindow> _chatWindows = new List<ChatWindow>();
        private readonly IUiDispatcher _uiDispatcher;
        private readonly IAppLogger _logger;
        private readonly IChatWindowFactory _chatWindowFactory;

        public ChatWindowCoordinator(IUiDispatcher uiDispatcher, IAppLogger logger, IChatWindowFactory chatWindowFactory)
        {
            _uiDispatcher = uiDispatcher;
            _logger = logger;
            _chatWindowFactory = chatWindowFactory;
        }

        public void AddFromSettings(ChatWindowViewModelSettings settings, TataruViewModel viewModel)
        {
            var viewModelWindow = viewModel.ChatWindows.FirstOrDefault(x => x.WinId == settings.WinId);
            if (viewModelWindow != null)
            {
                return;
            }

            viewModel.AddNewChatWindow(settings);
            var newWindow = viewModel.ChatWindows[viewModel.ChatWindows.Count - 1];
            var binder = new PropertyBinder(settings, newWindow);
            CreateBinderCouples(binder);
            _propertyBinders.Add(binder);
        }

        public void RemoveFromSettings(ChatWindowViewModelSettings settings, TataruViewModel viewModel)
        {
            var viewModelWindow = viewModel.ChatWindows.FirstOrDefault(x => x.WinId == settings.WinId);
            if (viewModelWindow == null)
            {
                return;
            }

            viewModel.DeleteChatWindow(viewModel.ChatWindows.IndexOf(viewModelWindow));
            RemoveChatWindow(viewModelWindow.WinId);
            StopAndRemoveBinder(viewModelWindow, settings);
        }

        public void AddFromViewModel(ChatWindowViewModel viewModelWindow, TataruUIModel uiModel)
        {
            var settings = uiModel.ChatWindows.FirstOrDefault(x => x.WinId == viewModelWindow.WinId);
            if (settings != null)
            {
                return;
            }

            var createdSettings = viewModelWindow.GetSettings();
            uiModel.ChatWindows.Add(createdSettings);
            var newSettings = uiModel.ChatWindows[uiModel.ChatWindows.Count - 1];
            var binder = new PropertyBinder(newSettings, viewModelWindow);
            CreateBinderCouples(binder);
            _propertyBinders.Add(binder);
        }

        public void RemoveFromViewModel(ChatWindowViewModel viewModelWindow, TataruUIModel uiModel)
        {
            var settings = uiModel.ChatWindows.FirstOrDefault(x => x.WinId == viewModelWindow.WinId);
            if (settings == null)
            {
                return;
            }

            uiModel.ChatWindows.Remove(settings);
            RemoveChatWindow(settings.WinId);
            StopAndRemoveBinder(settings, viewModelWindow);
        }

        public void ShowChatWindow(TataruModel tataruModel, ChatWindowViewModel viewModelWindow, MainWindow mainWindow)
        {
            _chatWindows.Add(_chatWindowFactory.Create(tataruModel, viewModelWindow, mainWindow));
            _chatWindows[_chatWindows.Count - 1].Show();
        }

        public void CloseAll()
        {
            foreach (var binder in _propertyBinders.ToList())
            {
                binder.Stop();
            }

            _propertyBinders.Clear();

            foreach (var chatWindow in _chatWindows.ToList())
            {
                chatWindow.Close();
            }

            _chatWindows.Clear();
        }

        private void StopAndRemoveBinder(object first, object second)
        {
            var binder = _propertyBinders.FirstOrDefault(x => x.Object1 == first && x.Object2 == second);
            if (binder == null)
            {
                binder = _propertyBinders.FirstOrDefault(x => x.Object2 == first && x.Object1 == second);
            }

            if (binder == null)
            {
                return;
            }

            binder.Stop();
            _propertyBinders.Remove(binder);
        }

        private void RemoveChatWindow(long winId)
        {
            var win = _chatWindows.FirstOrDefault(x => x.WinId == winId);
            if (win == null)
            {
                return;
            }

            _chatWindows.Remove(win);
            win.Close();
        }

        private void CreateBinderCouples(PropertyBinder binder)
        {
            try
            {
                binder.AddPropertyCouple(new PropertyCouple<string, string>("Name", "Name"));
                binder.AddPropertyCouple(new PropertyCouple<double, double>("ChatFontSize", "ChatFontSize"));
                binder.AddPropertyCouple(new PropertyCouple<double, double>("LineBreakHeight", "LineBreakHeight"));
                binder.AddPropertyCouple(new PropertyCouple<int, int>("SpacingCount", "SpacingCount"));
                binder.AddPropertyCouple(
                    new PropertyCouple<System.Windows.Media.FontFamily, System.Windows.Media.FontFamily>("ChatFont",
                        "ChatFont"));
                binder.AddPropertyCouple(new PropertyCouple<bool, bool>("IsAlwaysOnTop", "IsAlwaysOnTop"));
                binder.AddPropertyCouple(new PropertyCouple<bool, bool>("IsClickThrough", "IsClickThrough"));
                binder.AddPropertyCouple(new PropertyCouple<bool, bool>("IsAutoHide", "IsAutoHide"));
                binder.AddPropertyCouple(new PropertyCouple<TimeSpan, TimeSpan>("AutoHideTimeout", "AutoHideTimeout"));
                binder.AddPropertyCouple(
                    new PropertyCouple<System.Windows.Media.Color, System.Windows.Media.Color>("BackGroundColor",
                        "BackGroundColor"));
                binder.AddPropertyCouple(new PropertyCouple<bool, bool>("ShowTimestamps", "ShowTimestamps"));
                binder.AddPropertyCouple(
                    new PropertyCouple<System.Drawing.RectangleD, System.Drawing.RectangleD>("ChatWindowRectangle",
                        "ChatWindowRectangle"));
                binder.AddPropertyCouple(new PropertyCouple<TranslationEngineName, System.Windows.Data.CollectionView>(
                    "TranslationEngineName", "TranslationEngines",
                    (ref TranslationEngineName x, ref System.Windows.Data.CollectionView y) =>
                    {
                        var engine = x;
                        var collection = y;

                        _uiDispatcher.Invoke(() =>
                        {
                            TranslationEngine result = null;

                            foreach (TranslationEngine elem in collection.SourceCollection)
                            {
                                if (elem.EngineName == engine)
                                {
                                    result = elem;
                                    break;
                                }
                            }

                            if (result != null && !collection.CurrentItem.Equals(result))
                            {
                                collection.MoveCurrentTo(result);
                            }
                        });
                    },
                    (ref System.Windows.Data.CollectionView y, ref TranslationEngineName x) =>
                    {
                        var collection = y;
                        TranslationEngineName selectedEngine = TranslationEngineName.GoogleTranslate;

                        _uiDispatcher.Invoke(() => { selectedEngine = ((TranslationEngine)collection.CurrentItem).EngineName; });
                        x = selectedEngine;
                    }));
                binder.AddPropertyCouple(new PropertyCouple<TranslatorLanguague, System.Windows.Data.CollectionView>(
                    "FromLanguague", "TranslateFromLanguagues",
                    (ref TranslatorLanguague x, ref System.Windows.Data.CollectionView y) =>
                    {
                        var languague = x;
                        var collection = y;

                        _uiDispatcher.Invoke(() =>
                        {
                            TranslatorLanguague result = null;

                            foreach (TranslatorLanguague elem in collection.SourceCollection)
                            {
                                if (elem.SystemName == languague.SystemName)
                                {
                                    result = elem;
                                    break;
                                }
                            }

                            if (result != null && !collection.CurrentItem.Equals(result))
                            {
                                collection.MoveCurrentTo(result);
                            }
                        });
                    },
                    (ref System.Windows.Data.CollectionView y, ref TranslatorLanguague x) =>
                    {
                        var collection = y;
                        TranslatorLanguague lang = new TranslatorLanguague();

                        _uiDispatcher.Invoke(() => { lang = new TranslatorLanguague((TranslatorLanguague)collection.CurrentItem); });
                        x = lang;
                    }));
                binder.AddPropertyCouple(new PropertyCouple<TranslatorLanguague, System.Windows.Data.CollectionView>(
                    "ToLanguague", "TranslateToLanguagues",
                    (ref TranslatorLanguague x, ref System.Windows.Data.CollectionView y) =>
                    {
                        var languague = x;
                        var collection = y;

                        _uiDispatcher.Invoke(() =>
                        {
                            TranslatorLanguague result = null;

                            foreach (TranslatorLanguague elem in collection.SourceCollection)
                            {
                                if (elem.SystemName == languague.SystemName)
                                {
                                    result = elem;
                                    break;
                                }
                            }

                            if (result != null && !collection.CurrentItem.Equals(result))
                            {
                                collection.MoveCurrentTo(result);
                            }
                        });
                    },
                    (ref System.Windows.Data.CollectionView y, ref TranslatorLanguague x) =>
                    {
                        var collection = y;
                        TranslatorLanguague lang = new TranslatorLanguague();

                        _uiDispatcher.Invoke(() => { lang = new TranslatorLanguague((TranslatorLanguague)collection.CurrentItem); });
                        x = lang;
                    }));
                binder.AddPropertyCouple(
                    new PropertyCouple<WinUtils.HotKeyCombination, WinUtils.HotKeyCombination>("ShowHideChatKeys",
                        "ShowHideChatKeys"));
                binder.AddPropertyCouple(
                    new PropertyCouple<WinUtils.HotKeyCombination, WinUtils.HotKeyCombination>("ClickThoughtChatKeys",
                        "ClickThoughtChatKeys"));
                binder.AddPropertyCouple(
                    new PropertyCouple<WinUtils.HotKeyCombination, WinUtils.HotKeyCombination>("ClearChatKeys",
                        "ClearChatKeys"));
                binder.AddPropertyCouple(new PropertyCouple<List<ChatCodeViewModel>, BindingList<ChatCodeViewModel>>(
                    "ChatCodes", "ChatCodes",
                    (ref List<ChatCodeViewModel> x, ref BindingList<ChatCodeViewModel> y) =>
                    {
                        var list = x;
                        var bindinglist = y;

                        _uiDispatcher.Invoke(() =>
                        {
                            foreach (var code in list)
                            {
                                var existingCode = bindinglist.FirstOrDefault(p => p.Equals(code));
                                if (existingCode != null)
                                {
                                    existingCode.Color = code.Color;
                                    existingCode.IsChecked = code.IsChecked;
                                }
                            }
                        });
                    },
                    (ref BindingList<ChatCodeViewModel> y, ref List<ChatCodeViewModel> x) =>
                    {
                        var list = x;
                        var bindinglist = y;

                        _uiDispatcher.Invoke(() =>
                        {
                            foreach (var code in bindinglist)
                            {
                                var existingCode = list.FirstOrDefault(p => p.Equals(code));
                                if (existingCode != null)
                                {
                                    existingCode.Color = code.Color;
                                    existingCode.IsChecked = code.IsChecked;
                                }
                            }
                        });
                    }));
            }
            catch (Exception ex)
            {
                _logger.WriteLog("Failed to configure chat window property binder.");
                _logger.WriteLog(ex);
                throw;
            }
        }
    }
}
