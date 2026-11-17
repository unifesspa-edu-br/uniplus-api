namespace Unifesspa.UniPlus.Verificador.Sintetico.Intermediario;

/// <summary>Assembly intermediário que referencia, no manifesto, um alvo proibido carregado e outro ausente.</summary>
public static class Dependencias
{
    public static string AlvoCarregado => Npgsql.Sintetico.AlvoSintetico.Nome;

    public static string AlvoAusente => Microsoft.EntityFrameworkCore.Sintetico.AlvoSintetico.Nome;
}
