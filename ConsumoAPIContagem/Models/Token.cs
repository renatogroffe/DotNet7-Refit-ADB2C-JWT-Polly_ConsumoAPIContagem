namespace ConsumoAPIContagem.Models;

public class Token
{
    public bool? Authenticated => !String.IsNullOrWhiteSpace(AccessToken);
    public string? AccessToken { get; set; }
}