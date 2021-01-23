using SQLite;

namespace EasyTranslator.Models
{
    [Table("Records")]
    public class Record
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// Текст на первом языке, для которого выполняется перевод.
        /// </summary>
        [Indexed(Name = "ndx_Records_SourceText")]
        public string SourceText { get; set; }

        /// <summary>
        /// Язык, на который выполняется перевод.
        /// </summary>
        public int TargetLanguageId { get; set; }

        /// <summary>
        /// Результат перевода.
        /// </summary>
        public string Value { get; set; }
    }
}
