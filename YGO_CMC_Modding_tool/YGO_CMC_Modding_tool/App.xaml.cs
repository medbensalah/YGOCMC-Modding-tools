using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace YGO_CMC_Modding_tool
{
    public sealed partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();

            // Enter construction logic here...

            var mainPage = new MainPage();
            Window.Current.Content = mainPage;
        }
    }
}
