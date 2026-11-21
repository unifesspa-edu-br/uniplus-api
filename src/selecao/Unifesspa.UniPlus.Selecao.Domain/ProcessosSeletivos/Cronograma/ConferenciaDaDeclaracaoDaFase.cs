namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
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
    private const string CodigoDoAtoDuplicado = "FaseCronograma.AtoDuplicadoNaFase";

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
    // violações da mesma fase. A lista com parte recusada na leitura do documento deixa sem julgar a regra que a quantifica.
    internal static List<FieldError> Violacoes(
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase cronograma,
        PublicacoesDaFase publicacoes,
        IReadOnlyList<BancaRequerida> bancasRequeridas,
        IReadOnlySet<string> colecoesComParteRecusada) =>
    [
        .. JanelaDaFase.Violacoes(origem.OrigemData, cronograma),
        .. ViolacoesDasPublicacoes(origem.Codigo, publicacoes, colecoesComParteRecusada.Contains(FaseDeclarada.ListaDosProdutos)),
        .. TiposDeDecisaoDeclarados.Validar(publicacoes.TiposDeDecisao, TiposDeDecisaoDeclarados.Campo),
        .. ResultadoDaRegraDeHomologacao(origem.Codigo, publicacoes, colecoesComParteRecusada.Contains(FaseDeclarada.ListaDosProdutos)),
        .. RecorteDeCompetenciaDasBancas.Violacoes(origem.Codigo, bancasRequeridas),
        .. RecortesEmBancaDeRecursos(origem.Codigo, bancasRequeridas),
    ];

    // A falta do resultado sai sempre; o resultado que não é produto da fase só é julgado contra a lista de produtos íntegra.
    private static IEnumerable<FieldError> ResultadoDaRegraDeHomologacao(
        string codigoDaFase, PublicacoesDaFase publicacoes, bool produtosComParteRecusada)
    {
        FieldError? recusa = ConferenciaDaRegraDeHomologacao.ResultadoAusente(codigoDaFase, publicacoes.RegraDeHomologacao)
            ?? (produtosComParteRecusada ? null : ConferenciaDaRegraDeHomologacao.ResultadoQueNaoEProduto(codigoDaFase, publicacoes));
        return recusa is null ? [] : [recusa];
    }

    private static IEnumerable<FieldError> RecortesEmBancaDeRecursos(string codigoDaFase, IReadOnlyList<BancaRequerida> bancasRequeridas)
    {
        for (int indice = 0; indice < bancasRequeridas.Count; indice++)
        {
            BancaRequerida banca = bancasRequeridas[indice];
            if (ExigenciaDeBancaDeRecurso.RecorteNaDeclaracao(codigoDaFase, banca.Codigo, banca.RecorteDeCompetencia.Count) is { } recusa)
            {
                yield return new FieldError(RecorteDeCompetenciaDasBancas.CampoDoRecorte(indice), recusa);
            }
        }
    }

    private static void ExigirCodigoEFilhas(
        string codigo, IReadOnlyList<ProdutoDaFase> produtos, IReadOnlyList<BancaRequerida> bancasRequeridas)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentNullException.ThrowIfNull(produtos);
        ArgumentNullException.ThrowIfNull(bancasRequeridas);
    }

    // O ato repetido entre os produtos que ficaram continua repetido; o parecer sem resultado e a âncora do recurso leem a
    // lista inteira, e uma parte recusada pode ser o resultado ou o preliminar que falta.
    private static List<FieldError> ViolacoesDasPublicacoes(string codigo, PublicacoesDaFase publicacoes, bool produtosComParteRecusada)
    {
        FieldError?[] daLista =
        [
            produtosComParteRecusada ? null : ParecerIndividualSemResultado(codigo, publicacoes),
            produtosComParteRecusada ? null : AncoraDoRecursoDaFase.Violacao(codigo, publicacoes.Produtos, publicacoes.RegraRecurso),
        ];

        return [.. AtosDuplicados(codigo, publicacoes.Produtos), .. daLista.OfType<FieldError>()];
    }

    // A chave natural do produto é o par ato e papel, e o índice único da tabela a espelha. Não é o ato
    // sozinho porque o catálogo nomeia a matéria: o mesmo código é publicado como preliminar, que abre o
    // ciclo recursal, e como definitivo, que o encerra.
    private static IEnumerable<FieldError> AtosDuplicados(string codigo, IReadOnlyList<ProdutoDaFase> produtos) =>
        ProdutosPublicados.PosicoesRepetidas(produtos, static ato => ato)
            .Select(posicao => new FieldError($"produtos[{posicao}]", AtoDuplicado(codigo, produtos[posicao].AtoCodigo)));

    /// <summary>A recusa do par ato e papel repetido, que a leitura do documento reconhece para recusar a lista inteira.</summary>
    internal static bool EhAtoDuplicado(FieldError recusa) => recusa.Error.Code == CodigoDoAtoDuplicado;

    private static DomainError AtoDuplicado(string codigo, string atoCodigo) =>
        new(
            CodigoDoAtoDuplicado,
            $"A fase '{codigo}' declara o ato '{atoCodigo}' mais de uma vez no mesmo papel — o par ato e papel é declarado uma única vez por fase.");

    // O parecer individual é o fundamento do recurso: sem publicação de resultado não há decisão a
    // contestar, e a promessa ficaria sem objeto.
    private static FieldError? ParecerIndividualSemResultado(string codigo, PublicacoesDaFase publicacoes) =>
        publicacoes.EmiteParecerIndividual && !publicacoes.Produtos.Any(static p => p.Papel is not null)
            ? new FieldError("emiteParecerIndividual", new DomainError(
                "FaseCronograma.ParecerIndividualSemResultado",
                $"A fase '{codigo}' promete parecer individual e não publica nenhum resultado — não haveria decisão a fundamentar."))
            : null;
}
