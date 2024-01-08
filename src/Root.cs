public class Klasser
{
	public int skoleaarId { get; set; }
	public int id { get; set; }
	public int trin { get; set; }
	public bool harTrin { get; set; }
	public string navn { get; set; }
}

public class WeekLetter
{
	public object errorMessage { get; set; }
	public List<Ugebreve> ugebreve { get; set; }
	public List<Klasser> klasser { get; set; }
}

public class Ugebreve
{
	public string klasseNavn { get; set; }
	public int klasseId { get; set; }
	public int uge { get; set; }
	public string indhold { get; set; }
	public string id { get; set; }
}