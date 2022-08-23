using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace V2Architects.NumberSheets
{
    [TransactionAttribute(TransactionMode.Manual)]
    [RegenerationAttribute(RegenerationOption.Manual)]
    class Command : IExternalCommand
    {
        private readonly string _codeSymbol = "\u202A";
        private readonly string _tempCodeSymbol = "\u202B";


        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument.Document;
            
            try
            {
                var sheets = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .ToList();

                if (sheets.Count == 0)
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

                var definitions = GetBrowserOrganizationParametersForSheets(doc);
                var unicodesForGroupsInBrowser = GetUnicodesForBrowserOrganization(sheets, definitions);

                using (var t = new Transaction(doc, "Унификация номеров листов"))
                {
                    t.Start();

                    foreach (var sheet in sheets)
                    {
                        sheet.SheetNumber = sheet.SheetNumber + _tempCodeSymbol;
                    }

                    foreach (var sheet in sheets)
                    {
                        sheet.SheetNumber = sheet.SheetNumber.Replace(_tempCodeSymbol, "").Replace(_codeSymbol, "") 
                            + unicodesForGroupsInBrowser[GetGroupKey(sheet, definitions)];
                    }

                    UpdateReviUI();

                    t.Commit();
                }

                var reportWindow = new ReportWindow("Унификация выполнена.");
                reportWindow.ShowDialog();

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

        private void UpdateReviUI()
        {
            DockablePaneId dockablePaneId = DockablePanes.BuiltInDockablePanes.ProjectBrowser;
            var dockablePane = new DockablePane(dockablePaneId);
            dockablePane.Show();
            dockablePane.Hide();
        }
    }
}
