namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que uma declaração precisa cumprir para virar fase: as guardas contra erro de programação, que só
/// lançam, e as recusas de negócio, independentes entre si e por isso acumuladas (ADR-0125). Não há
/// passada pré-I/O para o handler chamar antes: a origem da data é cópia do cadastro e os produtos só
/// existem depois de o catálogo de tipos de ato ser consultado.
/// </summary>
internal static class ConferenciaDaDeclaracaoDaFase
{
    internal static void ExigirDeclaracaoPresente(
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase cronograma,
        PublicacoesDaFase publicacoes,
        IReadOnlyList<BancaRequerida> bancasRequeridas)
    {
        ArgumentNullException.ThrowIfNull(origem);
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(publicacoes);
        ExigirCodigoEFilhas(origem.Codigo, publicacoes.Produtos, bancasRequeridas);
    }

    internal static void ExigirValoresDeCriacao(int ordem, Guid faseCanonicaOrigemId, OrigemDataFase origemData)
    {
        if (ordem <= 0)
        {
            throw new ArgumentException("A ordem da fase deve ser maior que zero.", nameof(ordem));
        }

        if (faseCanonicaOrigemId == Guid.Empty)
        {
            throw new ArgumentException("O id de origem da fase canônica é obrigatório.", nameof(faseCanonicaOrigemId));
        }

        if (origemData == OrigemDataFase.Nenhuma)
        {
            throw new ArgumentException("A origem da data da fase é obrigatória.", nameof(origemData));
        }
    }

    internal static void ExigirIdCongelado(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A fase reidratada deve declarar o Id congelado no envelope.", nameof(id));
        }
    }

    // A âncora congelada é reposta como estava, pelo mesmo predicado de Criar: uma âncora reposta sobre
    // produto definitivo é tão incoerente quanto uma escrita assim pela primeira vez.
    internal static void ExigirAncoraCoerente(
        string codigo, IReadOnlyList<ProdutoDaFase> produtos, RegraRecursoFase? regraRecurso)
    {
        if (AncoraDoRecursoDaFase.Violacao(codigo, produtos, regraRecurso) is { } violacaoDaAncora)
        {
            throw new ArgumentException(violacaoDaAncora.Error.Message, nameof(regraRecurso));
        }
    }

    // Pelo mesmo predicado de Criar: um recorte reposto que não distingue duas bancas do mesmo tipo é tão
    // incoerente quanto um escrito assim pela primeira vez.
    internal static void ExigirRecorteCoerente(string codigo, IReadOnlyList<BancaRequerida> bancasRequeridas)
    {
        if (RecorteDeCompetenciaDasBancas.Violacoes(codigo, bancasRequeridas) is [{ } violacaoDoRecorte, ..])
        {
            throw new ArgumentException(violacaoDoRecorte.Error.Message, nameof(bancasRequeridas));
        }
    }

    // A janela invertida acumula aqui, e não no validator, para a resposta trazê-la junto das outras
    // violações da mesma fase.
    internal static List<FieldError> Violacoes(
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase cronograma,
        PublicacoesDaFase publicacoes,
        IReadOnlyList<BancaRequerida> bancasRequeridas) =>
    [
        .. JanelaDaFase.Violacoes(origem.Codigo, origem.OrigemData, cronograma),
        .. ViolacoesDasPublicacoes(origem.Codigo, publicacoes),
        .. RecorteDeCompetenciaDasBancas.Violacoes(origem.Codigo, bancasRequeridas),
    ];

    private static void ExigirCodigoEFilhas(
        string codigo, IReadOnlyList<ProdutoDaFase> produtos, IReadOnlyList<BancaRequerida> bancasRequeridas)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentNullException.ThrowIfNull(produtos);
        ArgumentNullException.ThrowIfNull(bancasRequeridas);
    }

    private static List<FieldError> ViolacoesDasPublicacoes(string codigo, PublicacoesDaFase publicacoes)
    {
        FieldError?[] violacoes =
        [
            AtoDuplicado(codigo, publicacoes.Produtos),
            ParecerIndividualSemResultado(codigo, publicacoes),
            AncoraDoRecursoDaFase.Violacao(codigo, publicacoes.Produtos, publicacoes.RegraRecurso),
        ];

        return [.. violacoes.OfType<FieldError>()];
    }

    // A chave natural do produto é o par ato e papel, e o índice único da tabela a espelha. Não é o ato
    // sozinho porque o catálogo nomeia a matéria: o mesmo código é publicado como preliminar, que abre o
    // ciclo recursal, e como definitivo, que o encerra.
    private static FieldError? AtoDuplicado(string codigo, IReadOnlyList<ProdutoDaFase> produtos) =>
        ProdutosPublicados.PrimeiroParRepetido(produtos, static ato => ato) is { } duplicado
            ? new FieldError("produtos", new DomainError(
                "FaseCronograma.AtoDuplicadoNaFase",
                $"A fase '{codigo}' declara o ato '{duplicado.AtoCodigo}' mais de uma vez no mesmo papel — o par ato e papel é declarado uma única vez por fase."))
            : null;

    // O parecer individual é o fundamento do recurso: sem publicação de resultado não há decisão a
    // contestar, e a promessa ficaria sem objeto.
    private static FieldError? ParecerIndividualSemResultado(string codigo, PublicacoesDaFase publicacoes) =>
        publicacoes.EmiteParecerIndividual && !publicacoes.Produtos.Any(static p => p.Papel is not null)
            ? new FieldError("emiteParecerIndividual", new DomainError(
                "FaseCronograma.ParecerIndividualSemResultado",
                $"A fase '{codigo}' promete parecer individual e não publica nenhum resultado — não haveria decisão a fundamentar."))
            : null;
}
