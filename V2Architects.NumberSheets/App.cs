using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace V2Architects.NumberSheets
{
    public class App : IExternalApplication
    {
        private string tabName = "V2 Tools";
        private string panelName = "Листы";
        private string buttonName = "Унификация\nномеров";
        private string buttonTooltip = "Унификация номеров листов.\n" +
                                      $"v{typeof(App).Assembly.GetName().Version}";

        public string AssemblyPath { get => typeof(App).Assembly.Location; }

        public Result OnStartup(UIControlledApplication revit)
        {
            if (RunningWrongRevitVersion(revit.ControlledApplication.VersionNumber))
            {
                return Result.Cancelled;
            }

            CreateRibbonTab(revit);
            CreateButton(CreateRibbonPanel(revit));

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }


        private static bool RunningWrongRevitVersion(string currentRevitVersion)
        {
            var requiredRevitVersions = new List<string> { "2019", "2020", "2021", "2022" };
            return !requiredRevitVersions.Contains(currentRevitVersion);
        }

        private void CreateRibbonTab(UIControlledApplication revit)
        {
            try
            {
                revit.CreateRibbonTab(tabName);
            }
            catch { }
        }

        private RibbonPanel CreateRibbonPanel(UIControlledApplication revit)
        {
            foreach (RibbonPanel panel in revit.GetRibbonPanels(tabName))
            {
                if (panel.Name == panelName)
                {
                    return panel;
                }
            }

            return revit.CreateRibbonPanel(tabName, panelName);
        }

        private void CreateButton(RibbonPanel panel)
        {
            var buttonData = new PushButtonData(
                nameof(NumberSheets),
                buttonName,
                typeof(Command).Assembly.Location,
                typeof(Command).FullName
            );

            var pushButton = panel.AddItem(buttonData) as PushButton;
            pushButton.LargeImage = GetImageSourceByBitMapFromResource(Properties.Resources.LargeImage);
            pushButton.Image = GetImageSourceByBitMapFromResource(Properties.Resources.Image);
            pushButton.ToolTip = buttonTooltip;
        }

        private ImageSource GetImageSourceByBitMapFromResource(Bitmap source)
        {
            return Imaging.CreateBitmapSourceFromHBitmap(
                source.GetHbitmap(),
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions()
            );
        }
    }
}
