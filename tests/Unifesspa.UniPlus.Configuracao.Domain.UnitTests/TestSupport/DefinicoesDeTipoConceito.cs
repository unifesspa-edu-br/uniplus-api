namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>Definições válidas de tipo de conceito, a serem alteradas com <c>with</c> em cada cenário.</summary>
internal static class DefinicoesDeTipoConceito
{
    public static DefinicaoInformada Numerica() =>
        new("Nota da entrevista", "Nota atribuída pela banca.", "NUMERICO", new EscalaInformada(0m, 10m, 2), null);

    public static DefinicaoInformada Categorica() =>
        new("Parecer da análise", null, "POR_OPCOES", null, [Valor("DEFERIDO", "Deferido", true), Valor("INDEFERIDO", "Indeferido", false)]);

    public static ValorInformado Valor(string? codigo, string? rotulo, bool contaComoAprovacao = false) =>
        new(codigo, rotulo, contaComoAprovacao);
}
