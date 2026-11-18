namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// De que versão do edital a escolha de um motivo vale: a versão em que ela entrou e, quando a escolha deixa de
/// valer, a versão em que saiu. A escolha vale nas versões a partir da entrada e anteriores à saída; sem saída,
/// vale em todas as seguintes. A igualdade é pelo valor.
/// </summary>
public sealed record VigenciaNoEdital
{
    private VigenciaNoEdital(int versaoDoEditalDeEntrada, int? versaoDoEditalDeSaida)
    {
        VersaoDoEditalDeEntrada = versaoDoEditalDeEntrada;
        VersaoDoEditalDeSaida = versaoDoEditalDeSaida;
    }

    /// <summary>O número da versão do edital em que a escolha entrou; a primeira versão é a 1.</summary>
    public int VersaoDoEditalDeEntrada { get; }

    /// <summary>O número da versão do edital em que a escolha saiu; nulo enquanto ela vale.</summary>
    public int? VersaoDoEditalDeSaida { get; }

    internal static VigenciaNoEdital APartirDa(int versaoDoEditalDeEntrada)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(versaoDoEditalDeEntrada, 1);

        return new(versaoDoEditalDeEntrada, null);
    }
}
