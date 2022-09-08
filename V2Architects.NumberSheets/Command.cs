using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace V2Architects.NumberSheets
{
    [TransactionAttribute(TransactionMode.Manual)]
    [RegenerationAttribute(RegenerationOption.Manual)]
    class Command : IExternalCommand
    {
        private Document _doc;
        private MainWindow _mainWindow;
        private List<ViewSheet> _sheets = new List<ViewSheet>();
        private int _count = 0;

        private readonly string _codeSymbol = "\u202A";
        private readonly string _tempCodeSymbol = "\u202B";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            _doc = commandData.Application.ActiveUIDocument.Document;

            _mainWindow = new MainWindow();
            _mainWindow.DataContext = this;

            _ComboB = new ObservableCollection<string>();

            try
            {
                GetDocSheets(); // получение списка листов проекта

                if (_sheets.Count == 0)
                {
                    var taskDialog = new TaskDialog("Ошибка")
                    {
                        TitleAutoPrefix = false,
                        MainIcon = TaskDialogIcon.TaskDialogIconError,
                        MainInstruction = "В проекте нет листов!"
                    };
                    taskDialog.Show();
                    return Result.Failed;
                }

                var definitions = GetBrowserOrganizationParametersForSheets(_doc);
                var unicodesForGroupsInBrowser = GetUnicodesForBrowserOrganization(_sheets, definitions);

                using (var t = new Transaction(_doc, "Унификациыя номеров листов"))
                {
                    t.Start();

                    var subgroups1 = _sheets.GroupBy(s => GetGroupKey(s, definitions[0]));
                    var subgroups11 = subgroups1.ToList();

                    foreach (var sheet1 in subgroups1)
                    {
                        var subgroups2 = sheet1.GroupBy(s => GetGroupKey(s, definitions[1]));
                        var subgroups21 = subgroups2.ToList();
                        var yyy = subgroups2.Select(x => x.Key).ToList();
                        yyy.Sort();
                        yyy.ForEach(x => _ComboB.Add(x));
                    }

                    _TextSelectItem = _ComboB[0];

                    _mainWindow.ShowDialog();

                    UpdateReviUI();

                    t.Commit();
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                TaskDialog.Show("Отмена", "Операция отменена пользователем.");
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", message);
                return Result.Failed;
            }
        }

        private void GetDocSheets()
        {
            _sheets = new FilteredElementCollector(_doc)
            .WhereElementIsNotElementType()
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .ToList();
        }

        private void RenameSheets()
        {
            var definition = GetBrowserOrganizationParametersForSheets(_doc)[1];
            _sheets = _sheets.Where(x => GetGroupKey(x, definition) == TextSelectItem).ToList();
            _sheets.Sort(new ViewSheetComparer());

            int i = 1;
            int.TryParse(Text, out i); // начало нумерации
            _sheets.ForEach(x => x.SheetNumber = (i++).ToString());
            _count = _sheets.Count;

            // унификация листов

            GetDocSheets();

            var definitions = GetBrowserOrganizationParametersForSheets(_doc);
            var unicodesForGroupsInBrowser = GetUnicodesForBrowserOrganization(_sheets, definitions);

            foreach (var sheet in _sheets)
            {
                sheet.SheetNumber = sheet.SheetNumber + _tempCodeSymbol;
                var _code = _tempCodeSymbol.ToString();
            }

            foreach (var sheet in _sheets)
            {
                sheet.SheetNumber = sheet.SheetNumber.Replace(_tempCodeSymbol, "").Replace(_codeSymbol, "")
                    + unicodesForGroupsInBrowser[GetGroupKey(sheet, definitions)];
            }

            UpdateReviUI();

             ShowReport();
        }

        private void ShowReport()
        {
            var reportWindow = new ReportWindow($"Обновлено {_count}");
            reportWindow.ShowDialog();
        }

        private List<Definition> GetBrowserOrganizationParametersForSheets(Document doc)
        {
            var sheet = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Sheets).WhereElementIsNotElementType().First();
            var org = BrowserOrganization.GetCurrentBrowserOrganizationForSheets(doc);
            List<FolderItemInfo> folderfields = org.GetFolderItems(sheet.Id).ToList();

            var definitions = new List<Definition>();
            foreach (FolderItemInfo info in folderfields)
            {
                string groupheader = info.Name;
                var parameterElement = doc.GetElement(info.ElementId) as ParameterElement;
                definitions.Add(parameterElement.GetDefinition());
            }

            return definitions;
        }

        private Dictionary<string, string> GetUnicodesForBrowserOrganization(List<ViewSheet> sheets, List<Definition> definitions)
        {
            var unicodeForSubgroups = new Dictionary<string, string>();
            var startCode = string.Empty;

            var subgroups = sheets.GroupBy(s => GetGroupKey(s, definitions));
            foreach (var group in subgroups)
            {
                startCode = startCode + _codeSymbol;
                unicodeForSubgroups[group.Key] = startCode;
            }

            return unicodeForSubgroups;
        }

        private string GetGroupKey(ViewSheet sheet, List<Definition> definitions)
        {
            var key = string.Empty;
            foreach (var definition in definitions)
            {
                key += sheet.get_Parameter(definition).AsString();
            }

            return key;
        }

        private string GetGroupKey(ViewSheet sheet, Definition definition)
        {
            var key = string.Empty;
            key += sheet.get_Parameter(definition).AsString();
            return key;
        }

        private void UpdateReviUI()
        {
            DockablePaneId dockablePaneId = DockablePanes.BuiltInDockablePanes.ProjectBrowser;
            var dockablePane = new DockablePane(dockablePaneId);
            dockablePane.Show();
            dockablePane.Hide();
        }

        private string _Text { get; set; }

        /// <summary>
        /// заполнение техстбокса
        /// </summary>
        public string Text
        {
            get => _Text;
            set
            {
                _Text = value;
                //OnPropertyChanged();
            }
        }

        /// <summary>
        /// массив для комбобокса 1
        /// </summary>
        private ObservableCollection<string> _ComboB { get; set; }

        /// <summary>
        /// массив для комбобокса
        /// </summary>
        public ObservableCollection<string> ComboB
        {
            get => _ComboB;
            set
            {
                _ComboB = value;
                //OnPropertyChanged();
            }
        }

        private string _TextSelectItem { get; set; }

        public string TextSelectItem
        {
            get => _TextSelectItem;
            set
            {
                _TextSelectItem = value;
                //Text = _TextSelectItem;
                //OnPropertyChanged();
            }
        }

        private RelayCommand _Btn;

        /// <summary>
        /// Команда запуска определения помещения 
        /// </summary>
        public RelayCommand Btn
        {
            get
            {
                return _Btn ??
                    (_Btn = new RelayCommand(obj => { _mainWindow.Close(); RenameSheets(); }));
            }
        }
    }

    /// <summary>
    /// Компаратор для листов.
    /// </summary>
    /// <remarks>Используется умное сравнение <see cref="LogicalStringComparer"/>. Сравнивает по свойству <see cref="ViewSheet.SheetNumber"/>.</remarks>
    public class ViewSheetComparer : IComparer<ViewSheet>
    {
        private readonly LogicalStringComparer _logicalStringComparer = new LogicalStringComparer();

        /// <inheritdoc/>
        public int Compare(ViewSheet x, ViewSheet y)
        {
            return _logicalStringComparer.Compare(x?.SheetNumber, y?.SheetNumber);
        }
    }

    /// <summary>
    /// Умное сравнение строк.
    /// </summary>
    /// <remarks>Для сравнения используется метод WinApi <see cref="StrCmpLogicalW"/></remarks>
    public class LogicalStringComparer : IComparer<string>
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int StrCmpLogicalW(string x, string y);

        /// <inheritdoc/>
        public int Compare(string x, string y)
        {
            return StrCmpLogicalW(x, y);
        }
    }

    public class RelayCommand : ICommand
    {
        private Action<object> execute;
        private Func<object, bool> canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return this.canExecute == null || this.canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            this.execute(parameter);
        }
    }
}
