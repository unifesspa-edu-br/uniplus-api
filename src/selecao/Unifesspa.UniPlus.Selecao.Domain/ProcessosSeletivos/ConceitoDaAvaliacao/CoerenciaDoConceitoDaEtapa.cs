namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Confere o conceito que a etapa declara contra o tipo da etapa e contra o resto da avaliação declarada,
/// acumulando as recusas (ADR-0125): o conceito é um dos que o tipo admite, o conceito categórico não tem
/// nota e a nota máxima cabe na escala do conceito numérico. Sem conceito não há o que conferir em
/// rascunho; a etapa de consolidação sem conceito é pendência de quem a cobra na publicação.
/// </summary>
/// <remarks>
/// A etapa de nota do ENEM não declara conceito: a nota vem do INEP. Quando recusa por isso, não repete as
/// demais recusas, que seriam consequência da mesma causa. A nota máxima menor ou igual a zero é recusada
/// pela escala do lançamento e não é conferida de novo contra a escala do conceito.
/// </remarks>
public static class CoerenciaDoConceitoDaEtapa
{
    /// <param name="conceito">A cópia do conceito que a etapa declara; nula é a etapa sem conceito.</param>
    /// <param name="conceitosAdmitidosPeloTipo">Os códigos de conceito que o tipo da etapa admite.</param>
    /// <param name="declaracao">A avaliação declarada pela etapa.</param>
    /// <param name="declaraItens">Se a etapa declara itens de avaliação.</param>
    /// <param name="etapaDeNotaDoEnem">Se a nota das etapas do tipo vem do ENEM.</param>
    public static IReadOnlyList<FieldError> Validar(
        ConceitoDaEtapa? conceito,
        IReadOnlyCollection<string> conceitosAdmitidosPeloTipo,
        DeclaracaoDaAvaliacaoDaEtapa declaracao,
        bool declaraItens,
        bool etapaDeNotaDoEnem)
    {
        ArgumentNullException.ThrowIfNull(conceitosAdmitidosPeloTipo);
        ArgumentNullException.ThrowIfNull(declaracao);
        if (conceito is null)
        {
            return [];
        }

        return etapaDeNotaDoEnem
            ? [Recusa("conceito", ConceitoDaEtapaErrorCodes.EtapaDoEnemNaoDeclaraConceito, "A etapa de nota do ENEM não declara conceito: a nota vem do INEP.")]
            : [.. RecusasDoConceito(conceito, conceitosAdmitidosPeloTipo, declaracao, declaraItens)];
    }

    private static IEnumerable<FieldError> RecusasDoConceito(
        ConceitoDaEtapa conceito, IReadOnlyCollection<string> admitidos, DeclaracaoDaAvaliacaoDaEtapa declaracao, bool declaraItens) =>
        RecusasDaAdmissao(conceito, admitidos)
            .Concat(RecusasDaNotaNoCategorico(conceito, declaracao, declaraItens))
            .Concat(RecusasDaNotaMaximaNoNumerico(conceito, declaracao));

    private static IEnumerable<FieldError> RecusasDaAdmissao(ConceitoDaEtapa conceito, IReadOnlyCollection<string> admitidos)
    {
        if (!admitidos.Contains(conceito.Codigo, StringComparer.Ordinal))
        {
            yield return Recusa(
                "conceito", ConceitoDaEtapaErrorCodes.NaoAdmitidoPeloTipo, "O conceito da etapa deve ser um dos que o tipo da etapa admite.");
        }
    }

    private static IEnumerable<FieldError> RecusasDaNotaNoCategorico(
        ConceitoDaEtapa conceito, DeclaracaoDaAvaliacaoDaEtapa declaracao, bool declaraItens) =>
        conceito.Natureza == NaturezaConceito.Categorico
            ? CamposDeNota(declaracao, declaraItens)
                .Where(static campo => campo.Declarado)
                .Select(static campo => Recusa(
                    campo.Nome,
                    ConceitoDaEtapaErrorCodes.NotaNoConceitoCategorico,
                    $"A etapa de conceito categórico não tem nota, então não declara {campo.Descricao}."))
            : [];

    private static IEnumerable<(string Nome, string Descricao, bool Declarado)> CamposDeNota(
        DeclaracaoDaAvaliacaoDaEtapa declaracao, bool declaraItens) =>
    [
        ("notaMaxima", "a nota máxima", declaracao.NotaMaxima.HasValue),
        ("notaMinima", "a nota mínima", declaracao.NotaMinima.HasValue),
        ("peso", "o peso", declaracao.Peso.HasValue),
        ("passoDoLancamento", "o passo do lançamento", declaracao.PassoDoLancamento.HasValue),
        ("itensDeAvaliacao", "itens de avaliação", declaraItens),
        ("escalaDeAtendimento", "escala de atendimento", declaracao.EscalaDeAtendimento is { Count: > 0 }),
    ];

    private static IEnumerable<FieldError> RecusasDaNotaMaximaNoNumerico(ConceitoDaEtapa conceito, DeclaracaoDaAvaliacaoDaEtapa declaracao)
    {
        if (conceito.Escala is { } escala && declaracao.NotaMaxima is > 0 and { } notaMaxima && notaMaxima > escala.Maxima)
        {
            yield return Recusa(
                "notaMaxima",
                ConceitoDaEtapaErrorCodes.NotaMaximaAcimaDaEscala,
                "A nota máxima da etapa não pode passar da escala máxima do conceito.");
        }
    }

    private static FieldError Recusa(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));
}
