namespace pdf_split.API;

public class TeamsResponse
{
    public IEnumerable<Team> Teams { get; set; } = [];

    public class Team
    {
        public int TeamNumber { get; set; }
    }
}