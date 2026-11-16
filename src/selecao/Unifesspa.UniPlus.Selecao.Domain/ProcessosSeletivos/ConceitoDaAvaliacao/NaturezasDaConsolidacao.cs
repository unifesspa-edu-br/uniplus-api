namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As naturezas de conceito a que cada versão de regra de consolidação se aplica, lidas do
/// <c>esquema_args</c> da própria versão, e a conferência da regra escolhida contra o conceito da etapa
/// (ADR-0150). A versão nova de uma regra pode ampliar as naturezas sem alcançar a versão antiga.
/// </summary>
public static class NaturezasDaConsolidacao
{
    private const string ChaveDasNaturezas = "naturezas_do_conceito";

    /// <summary>As naturezas que a versão da regra declara, sem repetição.</summary>
    /// <exception cref="ArgumentException">
    /// A versão não declara a chave, ou a declara vazia, fora de uma lista ou com token desconhecido: o
    /// catálogo é semeado por código, e esquema assim é defeito de quem semeia, não dado a recusar.
    /// </exception>
    public static IReadOnlyList<NaturezaConceito> De(RegraCatalogo regra)
    {
        ArgumentNullException.ThrowIfNull(regra);
        if (regra.EsquemaArgs.ValueKind != JsonValueKind.Object
            || !regra.EsquemaArgs.TryGetProperty(ChaveDasNaturezas, out JsonElement declaradas)
            || declaradas.ValueKind != JsonValueKind.Array
            || declaradas.GetArrayLength() == 0)
        {
            throw EsquemaInvalido(regra);
        }

        return [.. declaradas.EnumerateArray().Select(natureza => Ler(natureza, regra)).Distinct()];
    }

    /// <summary>
    /// A recusa da regra de consolidação cujas naturezas não incluem a do conceito da etapa, ou
    /// <see langword="null"/> quando ela se aplica.
    /// </summary>
    public static FieldError? Recusa(ConceitoDaEtapa conceito, IReadOnlyCollection<NaturezaConceito> naturezasDaRegra)
    {
        ArgumentNullException.ThrowIfNull(conceito);
        ArgumentNullException.ThrowIfNull(naturezasDaRegra);

        return naturezasDaRegra.Contains(conceito.Natureza)
            ? null
            : new("regraConsolidacao", new DomainError(
                ConceitoDaEtapaErrorCodes.ConsolidacaoIncompativel,
                "A regra de consolidação escolhida não se aplica à natureza do conceito da etapa."));
    }

    /// <summary>
    /// A pendência da etapa que declara regra de consolidação e não declara o conceito, ou
    /// <see langword="null"/> quando não há pendência. É tolerada em rascunho; quem a cobra é a publicação
    /// e a retificação.
    /// </summary>
    public static FieldError? PendenciaSemConceito(ReferenciaRegra? regraConsolidacao, ConceitoDaEtapa? conceito) =>
        regraConsolidacao is not null && conceito is null
            ? new("conceito", new DomainError(
                ConceitoDaEtapaErrorCodes.ConsolidacaoSemConceito,
                "A etapa com regra de consolidação deve declarar o conceito com que é avaliada."))
            : null;

    private static NaturezaConceito Ler(JsonElement declarada, RegraCatalogo regra)
    {
        string? token = declarada.ValueKind == JsonValueKind.String ? declarada.GetString() : null;

        return NaturezaConceitoCodigo.TentarConverter(token, out NaturezaConceito natureza)
            ? natureza
            : throw EsquemaInvalido(regra);
    }

    private static ArgumentException EsquemaInvalido(RegraCatalogo regra) =>
        new($"A regra '{regra.Codigo}' versão '{regra.Versao}' deve declarar '{ChaveDasNaturezas}' no esquema_args como lista não vazia de naturezas de conceito conhecidas.", nameof(regra));
}
