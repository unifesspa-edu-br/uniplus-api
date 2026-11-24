namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As formas de conceito a que cada versão de regra de consolidação se aplica, lidas do
/// <c>declaracao_da_regra</c> da própria versão, e a conferência da regra escolhida contra o conceito da etapa
/// (ADR-0150). A versão nova de uma regra pode ampliar as formas sem alcançar a versão antiga.
/// </summary>
public static class FormasDaConsolidacao
{
    private const string ChaveDasFormas = "formas_do_conceito";

    /// <summary>As formas que a versão da regra declara, sem repetição.</summary>
    /// <exception cref="ArgumentException">
    /// A versão não declara a chave, ou a declara vazia, fora de uma lista ou com token desconhecido: o
    /// catálogo é semeado por código, e esquema assim é defeito de quem semeia, não dado a recusar.
    /// </exception>
    public static IReadOnlyList<FormaDoConceito> De(RegraCatalogo regra) =>
        Tentar(regra, out IReadOnlyList<FormaDoConceito> formas) ? formas : throw EsquemaInvalido(regra);

    /// <summary>
    /// As formas que a versão declara, ou falso quando ela não as declara de forma válida: quem lê o catálogo para
    /// recusar a etapa em vez de lançar usa este, e não presume forma.
    /// </summary>
    public static bool Tentar(RegraCatalogo regra, out IReadOnlyList<FormaDoConceito> formas)
    {
        ArgumentNullException.ThrowIfNull(regra);

        formas = [];
        if (regra.DeclaracaoDaRegra.ValueKind != JsonValueKind.Object
            || !regra.DeclaracaoDaRegra.TryGetProperty(ChaveDasFormas, out JsonElement declaradas)
            || declaradas.ValueKind != JsonValueKind.Array
            || declaradas.GetArrayLength() == 0)
        {
            return false;
        }

        return TentarLerTodas(declaradas, out formas);
    }

    private static bool TentarLerTodas(JsonElement declaradas, out IReadOnlyList<FormaDoConceito> formas)
    {
        List<FormaDoConceito> lidas = [];
        bool todasConhecidas = declaradas.EnumerateArray().All(declarada => TentarLer(declarada, lidas));
        formas = todasConhecidas ? lidas : [];

        return todasConhecidas;
    }

    /// <summary>
    /// A recusa da regra de consolidação cujas formas não incluem a do conceito da etapa, ou
    /// <see langword="null"/> quando ela se aplica.
    /// </summary>
    public static FieldError? Recusa(ConceitoDaEtapa conceito, IReadOnlyCollection<FormaDoConceito> formasDaRegra)
    {
        ArgumentNullException.ThrowIfNull(conceito);
        ArgumentNullException.ThrowIfNull(formasDaRegra);

        return formasDaRegra.Contains(conceito.Natureza)
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

    private static bool TentarLer(JsonElement declarada, List<FormaDoConceito> lidas)
    {
        string? token = declarada.ValueKind == JsonValueKind.String ? declarada.GetString() : null;
        if (!CatalogoDe<FormaDoConceito>.TentarObter(token, out FormaDoConceito forma))
        {
            return false;
        }

        if (!lidas.Contains(forma))
        {
            lidas.Add(forma);
        }

        return true;
    }

    private static ArgumentException EsquemaInvalido(RegraCatalogo regra) =>
        new("A regra deve declarar ao menos uma natureza de conceito conhecida.", nameof(regra));
}
