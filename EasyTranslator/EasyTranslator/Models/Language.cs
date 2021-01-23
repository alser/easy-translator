using SQLite;

namespace EasyTranslator.Models
{
    [Table("Languages")]
    public sealed class Language
    {
        [PrimaryKey]
        public int Id { get; set; }

        public string Name { get; set; }
    }
}
