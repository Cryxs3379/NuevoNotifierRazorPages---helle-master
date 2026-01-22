namespace NotifierDesktop.ViewModels;

public class MissedCallVm
{
    public long Id { get; set; }
    public DateTime DateAndTime { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? NombrePila { get; set; }
    public string? NombreCompleto { get; set; }
    public long? AnswerCall { get; set; }

    public string Cliente
    {
        get
        {
            var nombre = $"{NombrePila ?? ""} {NombreCompleto ?? ""}".Trim();
            return string.IsNullOrWhiteSpace(nombre) ? "—" : nombre;
        }
    }

    public string Devuelta => AnswerCall.HasValue ? "Sí" : "No";
}
