using SQLite;
using System;

namespace OpenTyping
{
    [Table("VeneziaUsers")]
    public class VeneziaUser : IComparable<VeneziaUser>
    {
        public VeneziaUser() { }

        public VeneziaUser(string name, string country, string org, string character, int level, int score, string date)
        {
            this.Name = name;
            this.Country = country;
            this.Org = org;
            this.Character = character;
            this.Level = level;
            this.Score = score;
            this.Date = date;
        }

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; }
        public string Country { get; set; }
        public string Org { get; set; }
        public string Character { get; set; }
        public int Level { get; set; }
        public int Score { get; set; }
        public string Date { get; set; }

        public int CompareTo(VeneziaUser other)
        {
            // Sort by Score descending
            if (this.Score < other.Score) return 1;
            else if (this.Score > other.Score) return -1;
            else
            {
                // Sort by Level descending
                if (this.Level < other.Level) return 1;
                else if (this.Level > other.Level) return -1;
                else return 0;
            }
        }
    }
}
