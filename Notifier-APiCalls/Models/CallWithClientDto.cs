namespace NotifierAPI.Models;

public class CallWithClientDto
{
    public long Id { get; set; }
    public DateTime DateAndTime { get; set; }
    public string PhoneNumber { get; set; } = "";
    public string NombreCompleto { get; set; } = "";
    public string NombrePila { get; set; } = "";
}
