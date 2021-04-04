using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using EasyTranslator.Data;
using EasyTranslator.Models;
using Plugin.FilePicker;
using Plugin.FilePicker.Abstractions;
using Xamarin.Forms;

namespace EasyTranslator
{
    public partial class MainPage
    {
        #region Constructors

        public MainPage() => this.InitializeComponent();

        #endregion

        #region Fields

        private bool skipEmptyDatabaseCheck;

        #endregion

        #region Private Methods

        private async Task LoadFileAsync(byte[] content)
        {
            string[] lastProcessedWordClosure = { null };

            try
            {
                this.ResultsLabel.Text = "Пожалуйста, подождите";

                await this.ProcessContentsAsync(content, lastProcessedWordClosure);

                this.ResultsLabel.Text = "Файл успешно загружен";
            }
            catch (Exception ex)
            {
                string prefix = lastProcessedWordClosure[0] is not null
                    ? $"Ошибка при обработке слова \"{lastProcessedWordClosure[0]}\": "
                    : "Ошибка: ";

                this.ResultsLabel.Text = prefix + ex;
            }
        }


        private async Task ProcessContentsAsync(byte[] data, string[] lastProcessedWordClosure)
        {
            using var streamReader = new StreamReader(new MemoryStream(data), Encoding.UTF8, true);
            using var csvReader = new CsvReader(streamReader,
                new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = ";" });

            if (!await csvReader.ReadAsync())
            {
                this.ResultsLabel.Text = "Ошибка: файл пустой";
                return;
            }

            int count = csvReader.Parser.Count;
            if (count < 2)
            {
                this.ResultsLabel.Text = "Ошибка: должно быть минимум два языка";
                return;
            }

            var languages = new Language[count];
            for (int i = 0; i < languages.Length; i++)
            {
                languages[i] = new Language { Id = i, Name = csvReader.GetField(i) };
            }

            var records = new List<Record>();

            while (await csvReader.ReadAsync())
            {
                string sourceText = csvReader.GetField(0)?.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(sourceText))
                {
                    continue;
                }

                lastProcessedWordClosure[0] = sourceText;

                for (int i = 1; i < count; i++)
                {
                    records.Add(new Record
                    {
                        SourceText = sourceText,
                        TargetLanguageId = i,
                        Value = csvReader.GetField(i),
                    });
                }
            }

            TranslatorDatabase database = App.Database;

            await database.DeleteAllLanguagesAsync();
            await database.DeleteAllRecordsAsync();

            await database.InsertLanguagesAsync(languages);
            await database.InsertRecordsAsync(records);
        }


        private async Task ResetDatabaseAsync()
        {
            try
            {
                byte[] bytes;
                {
                    await using Stream stream = Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream(typeof(MainPage), "data.csv");

                    bytes = new byte[stream.Length];

                    await using Stream memoryStream = new MemoryStream(bytes);
                    await stream.CopyToAsync(memoryStream);
                }

                await this.LoadFileAsync(bytes);
            }
            catch (Exception ex)
            {
                this.ResultsLabel.Text = "Ошибка: " + ex;
            }
        }


        private void AddDeveloperToolbar()
        {
            var loadFromFile = new ToolbarItem { Text = "Загрузить из файла", Priority = 0, Order = ToolbarItemOrder.Secondary };
            loadFromFile.Clicked += this.LoadFromFile_OnClicked;
            if (this.ToolbarItems.All(x => x.Text != loadFromFile.Text))
            {
                this.ToolbarItems.Add(loadFromFile);
            }

            var defaultDatabase = new ToolbarItem { Text = "База по умолчанию", Priority = 1, Order = ToolbarItemOrder.Secondary };
            loadFromFile.Clicked += this.ResetDatabase_OnClicked;
            if (this.ToolbarItems.All(x => x.Text != defaultDatabase.Text))
            {
                this.ToolbarItems.Add(defaultDatabase);
            }
        }


        private void RemoveDeveloperToolbar()
        {
            this.ToolbarItems.Clear();
        }

        #endregion

        #region Event Handlers

        private async void LoadFromFile_OnClicked(object sender, EventArgs e)
        {
            try
            {
                FileData fileData = await CrossFilePicker.Current.PickFile();
                if (fileData is null)
                {
                    return;
                }

                string fileName = fileData.FileName;
                if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    this.ResultsLabel.Text = "Ошибка: выберите файл с расширением .csv";
                    return;
                }

                await this.LoadFileAsync(fileData.DataArray);
            }
            catch (Exception ex)
            {
                this.ResultsLabel.Text = "Ошибка: " + ex;
            }
        }

        private async void ResetDatabase_OnClicked(object sender, EventArgs e) => await this.ResetDatabaseAsync();


        private async void SearchBar_OnSearchButtonPressed(object sender, EventArgs e)
        {
            if (!this.skipEmptyDatabaseCheck)
            {
                if (await App.Database.IsEmptyAsync())
                {
                    await this.ResetDatabaseAsync();
                }

                this.skipEmptyDatabaseCheck = true;
            }

            this.ResultsLabel.Text = string.Empty;

            string text = this.SearchBar.Text?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            switch (text)
            {
                case "show developer tools":
                    this.AddDeveloperToolbar();
                    this.ResultsLabel.Text = "Тулбар добавлен." + Environment.NewLine + Environment.NewLine + "Введите \"hide developer tools\", чтобы скрыть.";
                    return;

                case "hide developer tools":
                    this.RemoveDeveloperToolbar();
                    this.ResultsLabel.Text = "Тулбар скрыт." + Environment.NewLine + Environment.NewLine + "Введите \"show developer tools\", чтобы показать.";
                    return;
            }

            TranslatorDatabase database = App.Database;

            List<Language> languages = await database.GetLanguagesAsync();
            List<Record> exactRecords = await database.SearchRecordsAsync(text);
            List<Record> startsWithRecords = text.Length > 1 ? await database.SearchRecordsStartsWithAndNotExactAsync(text) : new List<Record>();
            List<Record> approximateRecords = text.Length > 2 ? await database.SearchRecordsContainsAndNotExactAsync(text) : new List<Record>();

            var formattedText = new FormattedString();
            if (exactRecords.Count == 0 && startsWithRecords.Count == 0 && approximateRecords.Count == 0)
            {
                this.ResultsLabel.Text = "не найдено";
                return;
            }

            if (exactRecords.Count > 0)
            {
                formattedText.Spans.Add(
                    new Span
                    {
                        Text = text,
                        FontSize = 22.0
                    });

                AddWordTranslations(formattedText, exactRecords, languages);
            }

            AddGroupTranslations(formattedText, startsWithRecords, languages);
            AddGroupTranslations(formattedText, approximateRecords, languages);

            this.ResultsLabel.FormattedText = formattedText;
        }


        private static void AddGroupTranslations(FormattedString formattedText, IEnumerable<Record> records, IReadOnlyCollection<Language> languages)
        {
            foreach (IGrouping<string, Record> grouping in records.OrderBy(x => x.SourceText).GroupBy(x => x.SourceText))
            {
                if (formattedText.Spans.Count > 0)
                {
                    formattedText.Spans.Add(
                        new Span { Text = Environment.NewLine + Environment.NewLine + Environment.NewLine, FontSize = 22.0 });
                }

                formattedText.Spans.Add(
                    new Span
                    {
                        Text = grouping.Key,
                        FontSize = 22.0
                    });

                AddWordTranslations(formattedText, grouping, languages);
            }
        }


        private static void AddWordTranslations(FormattedString formattedText, IEnumerable<Record> records, IReadOnlyCollection<Language> languages)
        {
            (string language, string value)[] results = records
                .Select(x => (language: languages.First(y => y.Id == x.TargetLanguageId).Name, value: x.Value?.Trim()))
                .Where(x => !string.IsNullOrEmpty(x.value))
                .Distinct()
                .OrderBy(x => x.language)
                .ThenBy(x => x.value)
                .ToArray();

            string prevLanguage = null;
            foreach ((string language, string value) in results)
            {
                if (formattedText.Spans.Count > 0)
                {
                    formattedText.Spans.Add(
                        new Span { Text = Environment.NewLine, FontSize = 18.0 });
                }

                if (language != prevLanguage)
                {
                    //formattedText.Spans.Add(
                    //    new Span
                    //    {
                    //        Text = language + Environment.NewLine,
                    //        FontAttributes = FontAttributes.Bold,
                    //        FontSize = 16.0
                    //    });
                }

                formattedText.Spans.Add(
                    new Span { Text = value, FontSize = 18.0 });

                prevLanguage = language;
            }
        }


        private void SearchBar_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.NewTextValue))
            {
                this.ResultsLabel.Text = string.Empty;
            }
        }

        #endregion
    }
}