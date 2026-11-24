namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O que a versão de uma regra de consolidação de conceito categórico declara na <c>declaracao_da_regra</c>: o critério
/// de apuração, os corretores distintos e o tratamento quando o critério não converge (ADR-0150, emenda 3). É dado
/// da versão, sem valor fixo no código: o edital só escolhe a versão que referencia.
/// </summary>
/// <remarks>
/// As regras de consolidação numéricas não declaram critério nem tratamento, então a leitura falha para elas.
/// </remarks>
/// <param name="Criterio">O que basta para as atribuições virarem o conceito da etapa.</param>
/// <param name="CorretoresDistintos">Os corretores distintos que a versão exige, no mínimo um.</param>
/// <param name="Tratamento">O que se faz quando o critério não converge.</param>
public sealed record DeclaracaoDaConsolidacaoDoConceito(
    CriterioDeApuracaoDoConceito Criterio,
    int CorretoresDistintos,
    TratamentoDaFaltaDeApuracao Tratamento)
{
    private const string ChaveDoCriterio = "criterio";

    /// <summary>A declaração da versão da regra.</summary>
    /// <exception cref="ArgumentException">
    /// A regra não é de consolidação, ou a versão não declara critério, corretores, formas e tratamento válidos: o catálogo
    /// é semeado por código, e declaração assim é defeito de quem semeia, e nada é presumido.
    /// </exception>
    public static DeclaracaoDaConsolidacaoDoConceito De(RegraCatalogo regra) =>
        Tentar(regra, out DeclaracaoDaConsolidacaoDoConceito? declaracao) ? declaracao : throw DeclaracaoInvalida(regra);

    /// <summary>
    /// A declaração da versão, ou falso quando ela não declara as quatro chaves (critério, corretores, formas e tratamento) de forma válida. Quem lê o catálogo
    /// para recusar a etapa em vez de lançar usa este, e não presume critério, número nem tratamento.
    /// </summary>
    public static bool Tentar(RegraCatalogo regra, [NotNullWhen(true)] out DeclaracaoDaConsolidacaoDoConceito? declaracao)
    {
        ArgumentNullException.ThrowIfNull(regra);

        declaracao = null;
        if (!CorretoresDaRegraDeConsolidacao.Tentar(regra, out int corretores)
            || !FormasDaConsolidacao.Tentar(regra, out _)
            || !CatalogoDe<CriterioDeApuracaoDoConceito>.TentarObter(LerTexto(regra, ChaveDoCriterio), out CriterioDeApuracaoDoConceito criterio)
            || !TratamentoDaRegraDeConsolidacao.Tentar(regra, out TratamentoDaFaltaDeApuracao? tratamento)
            || tratamento is not { } declarado)
        {
            return false;
        }

        declaracao = new(criterio, corretores, declarado);
        return true;
    }

    private static string? LerTexto(RegraCatalogo regra, string chave) =>
        regra.DeclaracaoDaRegra.TryGetProperty(chave, out JsonElement valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;

    private static ArgumentException DeclaracaoInvalida(RegraCatalogo regra) =>
        new("A regra de consolidação deve declarar critério, corretores, formas de conceito e tratamento válidos.", nameof(regra));
}
