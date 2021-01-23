﻿using System.Collections.Generic;
using System.Threading.Tasks;
using EasyTranslator.Models;
using SQLite;

namespace EasyTranslator.Data
{
    public class TranslatorDatabase
    {
        private readonly SQLiteAsyncConnection database;

        public TranslatorDatabase(string dbPath)
        {
            this.database = new SQLiteAsyncConnection(dbPath);

            this.database.CreateTableAsync<Language>().GetAwaiter().GetResult();
            this.database.CreateTableAsync<Record>().GetAwaiter().GetResult();
        }

        public Task<List<Language>> GetLanguagesAsync() => this.database.Table<Language>().ToListAsync();

        public Task InsertLanguagesAsync(IEnumerable<Language> languages) => this.database.InsertAllAsync(languages, typeof(Language));

        public Task DeleteAllLanguagesAsync() => this.database.DeleteAllAsync<Language>();

        public Task<List<Record>> SearchRecordsAsync(string searchText) =>
            this.database.Table<Record>()
                .Where(x => x.SourceText == searchText)
                .ToListAsync();

        public Task InsertRecordsAsync(IEnumerable<Record> records) => this.database.InsertAllAsync(records, typeof(Record));

        public Task DeleteAllRecordsAsync() => this.database.DeleteAllAsync<Record>();
    }
}
