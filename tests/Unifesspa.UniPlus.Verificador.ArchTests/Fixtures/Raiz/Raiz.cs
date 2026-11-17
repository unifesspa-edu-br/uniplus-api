namespace Unifesspa.UniPlus.Verificador.Sintetico;

/// <summary>Assembly que faz o papel do verificador: só referencia o intermediário, nunca os alvos proibidos.</summary>
public static class Raiz
{
    public static string Alvos => $"{Intermediario.Dependencias.AlvoCarregado} {Intermediario.Dependencias.AlvoAusente}";
}
