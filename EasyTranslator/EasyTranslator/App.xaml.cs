using System;
using System.IO;
using EasyTranslator.Data;
using Xamarin.Forms;

namespace EasyTranslator
{
    public partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();

            this.MainPage = new NavigationPage(new MainPage());
        }

        protected override void OnStart()
        {
        }

        protected override void OnSleep()
        {
        }

        protected override void OnResume()
        {
        }

        #region Database Static Property

        private static TranslatorDatabase database;

        private static readonly object syncObject = new object();

        public static TranslatorDatabase Database
        {
            get
            {
                if (database is null)
                {
                    lock (syncObject)
                    {
                        database ??=
                            new TranslatorDatabase(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "translations.db3"));
                    }
                }

                return database;
            }
        }

        #endregion
    }
}
