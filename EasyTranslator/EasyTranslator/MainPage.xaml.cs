using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void LoadFromFile_OnClicked(object sender, EventArgs e)
        {
            string[] lastProcessedWordClosure = { null };

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

                this.ResultsLabel.Text = "Пожалуйста, подождите";

                await this.ProcessContentsAsync(fileData.DataArray, lastProcessedWordClosure);

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


        private async void SearchBar_OnSearchButtonPressed(object sender, EventArgs e)
        {
            this.ResultsLabel.Text = string.Empty;

            string text = this.SearchBar.Text?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            TranslatorDatabase database = App.Database;

            List<Record> records = await database.SearchRecordsAsync(text);
            if (records.Count == 0)
            {
                this.ResultsLabel.Text = "не найдено";
                return;
            }

            List<Language> languages = await database.GetLanguagesAsync();
            (string language, string value)[] results = records
                .Select(x => (language: languages.First(y => y.Id == x.TargetLanguageId).Name, value: x.Value?.Trim()))
                .Where(x => !string.IsNullOrEmpty(x.value))
                .Distinct()
                .OrderBy(x => x.language)
                .ThenBy(x => x.value)
                .ToArray();

            var formattedText = new FormattedString();

            string prevLanguage = null;
            foreach ((string language, string value) in results)
            {
                if (formattedText.Spans.Count > 0)
                {
                    formattedText.Spans.Add(
                        new Span { Text = Environment.NewLine + Environment.NewLine, FontSize = 20.0 });
                }

                if (language != prevLanguage)
                {
                    formattedText.Spans.Add(
                        new Span
                        {
                            Text = language + Environment.NewLine,
                            FontAttributes = FontAttributes.Bold,
                            FontSize = 16.0
                        });
                }

                formattedText.Spans.Add(
                    new Span { Text = value, FontSize = 20.0 });

                prevLanguage = language;
            }

            this.ResultsLabel.FormattedText = formattedText;
        }


        private void SearchBar_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.NewTextValue))
            {
                this.ResultsLabel.Text = string.Empty;
            }
        }
    }
}