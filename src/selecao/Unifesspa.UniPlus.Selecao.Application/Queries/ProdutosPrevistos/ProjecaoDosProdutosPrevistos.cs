namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProdutosPrevistos;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cruza o que a versão vigente do edital declara publicar com os atos já registrados no processo e
/// diz, de cada item, a situação e se ele pode sair agora.
/// </summary>
/// <remarks>
/// <para>
/// <b>Matéria.</b> O catálogo nomeia a matéria pelo tipo do ato, e o edital a declara uma vez como
/// preliminar e outra como definitivo. O definitivo só sai depois que o preliminar do mesmo tipo,
/// declarado onde <see cref="PreliminaresDaMateria"/> diz, foi publicado, isto é, confirmado pelo
/// registro central. Item sem preliminar declarado não espera nada.
/// </para>
/// <para>
/// A recusa de publicar o que o edital não declara, o processo fora da página pública e o tipo
/// único já usado são conferências do comando de publicação: esta projeção só reflete as que
/// dependem do cronograma e do catálogo vigente.
/// </para>
/// </remarks>
internal static class ProjecaoDosProdutosPrevistos
{
    /// <summary>Por que o item não tem uma segunda publicação original: a correção é outro ato.</summary>
    internal const string MensagemDoItemJaPublicado = "O item já foi publicado. Para corrigi-lo, publique uma errata ou retificação do ato.";

    /// <param name="Id">O <c>Id</c> do produto na versão lida; muda entre versões e só serve dentro dela.</param>
    /// <param name="AtoCodigo">O tipo de ato do item.</param>
    /// <param name="Papel">O papel do item no ciclo recursal.</param>
    /// <param name="Item">A identidade do item entre versões, a que o ato publicado guarda.</param>
    internal sealed record Declarado(Guid Id, string AtoCodigo, PapelDaPublicacaoPrevista? Papel, IdentidadeDoItemDoEdital Item);

    /// <summary>Os códigos dos tipos de ato que o cronograma declara publicar.</summary>
    public static IReadOnlyList<string> TiposDeAtoDeclarados(ConfiguracaoPublicada configuracaoPublicada)
    {
        ArgumentNullException.ThrowIfNull(configuracaoPublicada);

        return
        [
            .. configuracaoPublicada.Cronograma.CronogramaFases.SelectMany(static f => f.Produtos.Select(static p => p.AtoCodigo))
                .Concat(configuracaoPublicada.Avaliacao.Etapas.SelectMany(static e => e.Produtos.Select(static p => p.AtoCodigo)))
                .Distinct(StringComparer.Ordinal),
        ];
    }

    /// <summary>O produto que o cronograma declara com o identificador dado; nulo quando a versão não o declara.</summary>
    internal static Declarado? ProdutoDeclarado(ConfiguracaoPublicada configuracaoPublicada, Guid produtoId)
    {
        ArgumentNullException.ThrowIfNull(configuracaoPublicada);

        return configuracaoPublicada.Cronograma.CronogramaFases.SelectMany(static f => f.Produtos.Select(p => DeclaradoDaFase(f, p)))
            .Concat(configuracaoPublicada.Avaliacao.Etapas.SelectMany(static e => e.Produtos.Select(p => DeclaradoDaEtapa(e, p))))
            .FirstOrDefault(p => p.Id == produtoId);
    }

    /// <summary>As identidades dos itens que o cronograma declara publicar.</summary>
    public static IReadOnlyList<IdentidadeDoItemDoEdital> ItensDeclarados(ConfiguracaoPublicada configuracaoPublicada)
    {
        ArgumentNullException.ThrowIfNull(configuracaoPublicada);

        return [.. IdentidadeDoItemDoEdital.Dos(configuracaoPublicada.Cronograma.CronogramaFases, configuracaoPublicada.Avaliacao.Etapas).Values.Distinct()];
    }

    private static Declarado DeclaradoDaFase(FaseCronograma fase, ProdutoDaFase produto) =>
        new(produto.Id, produto.AtoCodigo, produto.Papel, IdentidadeDoItemDoEdital.DaFase(fase, produto));

    private static Declarado DeclaradoDaEtapa(EtapaProcesso etapa, ProdutoDaEtapa produto) =>
        new(produto.Id, produto.AtoCodigo, produto.Papel, IdentidadeDoItemDoEdital.DaEtapa(etapa, produto));

    public static ProdutosPrevistosDoProcessoDto Projetar(
        ConfiguracaoVigente vigente,
        CadeiasDosProdutosPrevistos cadeias,
        IReadOnlySet<string> tiposVigentes,
        Func<AtoDoProcesso, AtoDoProcessoDto> projetarAto)
    {
        ArgumentNullException.ThrowIfNull(vigente);
        ArgumentNullException.ThrowIfNull(cadeias);
        ArgumentNullException.ThrowIfNull(tiposVigentes);
        ArgumentNullException.ThrowIfNull(projetarAto);

        return new ProdutosPrevistosDoProcessoDto(
            vigente.NumeroDaVersao,
            PoliticaDeDivulgacaoDaVersao(vigente.Configuracao),
            [.. vigente.Configuracao.Cronograma.CronogramaFases.OrderBy(static f => f.Ordem).Select(f => Fase(f, vigente, cadeias, tiposVigentes, projetarAto))],
            EtapasDaFase(null, vigente, cadeias, tiposVigentes, projetarAto));
    }

    private static List<EtapaComProdutosPrevistosDto> EtapasDaFase(string? faseCodigo, ConfiguracaoVigente vigente,
        CadeiasDosProdutosPrevistos cadeias, IReadOnlySet<string> tiposVigentes, Func<AtoDoProcesso, AtoDoProcessoDto> projetarAto) =>
        [.. vigente.Configuracao.Avaliacao.Etapas.Where(e => string.Equals(e.FaseCodigo, faseCodigo, StringComparison.Ordinal))
            .OrderBy(static e => e.Ordem).ThenBy(static e => e.Nome, StringComparer.Ordinal)
            .Select(e => Etapa(e, vigente, cadeias, tiposVigentes, projetarAto))];

    private static EtapaComProdutosPrevistosDto Etapa(EtapaProcesso etapa, ConfiguracaoVigente vigente,
        CadeiasDosProdutosPrevistos cadeias, IReadOnlySet<string> tiposVigentes,
        Func<AtoDoProcesso, AtoDoProcessoDto> projetarAto) => new(etapa.Id, etapa.Nome, etapa.Ordem,
        Produtos([.. etapa.Produtos.Select(p => DeclaradoDaEtapa(etapa, p))], vigente, cadeias, tiposVigentes, projetarAto));

    private static FaseComProdutosPrevistosDto Fase(FaseCronograma fase, ConfiguracaoVigente vigente,
        CadeiasDosProdutosPrevistos cadeias, IReadOnlySet<string> tiposVigentes,
        Func<AtoDoProcesso, AtoDoProcessoDto> projetarAto) => new(fase.Id, fase.Codigo, fase.Ordem,
        Produtos([.. fase.Produtos.Select(p => DeclaradoDaFase(fase, p))], vigente, cadeias, tiposVigentes, projetarAto),
        EtapasDaFase(fase.Codigo, vigente, cadeias, tiposVigentes, projetarAto));

    /// <summary>
    /// A política de divulgação do certame como a versão congelou, e não como o cadastro está hoje.
    /// Sem configuração explícita vale o piso minimizado: só o número de inscrição.
    /// </summary>
    internal static ConfiguracaoDivulgacaoDto PoliticaDeDivulgacaoDaVersao(ConfiguracaoPublicada configuracaoPublicada)
    {
        ArgumentNullException.ThrowIfNull(configuracaoPublicada);

        return configuracaoPublicada.DivulgacaoETaxa.ConfiguracaoDivulgacao is { } configuracao
            ? new ConfiguracaoDivulgacaoDto(configuracao.CamposPublicos, configuracao.Justificativa)
            : new ConfiguracaoDivulgacaoDto([ConfiguracaoDivulgacao.NumeroInscricao], null);
    }

    private static List<ProdutoPrevistoDto> Produtos(
        List<Declarado> declarados,
        ConfiguracaoVigente vigente,
        CadeiasDosProdutosPrevistos cadeias,
        IReadOnlySet<string> tiposVigentes,
        Func<AtoDoProcesso, AtoDoProcessoDto> projetarAto) =>
        [.. declarados.Select(d => Produto(d, vigente, cadeias, tiposVigentes, projetarAto))];

    private static ProdutoPrevistoDto Produto(
        Declarado produto,
        ConfiguracaoVigente vigente,
        CadeiasDosProdutosPrevistos cadeias,
        IReadOnlySet<string> tiposVigentes,
        Func<AtoDoProcesso, AtoDoProcessoDto> projetarAto)
    {
        AtoDoProcesso? primeira = cadeias.PrimeiraPublicacaoDe(produto.Item);
        SituacaoDoProdutoPrevisto situacao = SituacaoDe(primeira);
        string? nomeDoTipo = vigente.TiposDeAto.GetValueOrDefault(produto.AtoCodigo)?.Nome ?? primeira?.TipoNome;
        ImpedimentoDoProdutoDto? impedimento = ImpedimentoParaPublicar(produto, situacao, vigente, cadeias, tiposVigentes, nomeDoTipo);
        return new ProdutoPrevistoDto(
            produto.Id,
            produto.AtoCodigo,
            nomeDoTipo,
            ProjecaoDoPapelDaPublicacao.De(produto.Papel),
            CodigoComRotulo<SituacaoDoProdutoPrevisto>.De(situacao),
            primeira is null ? null : projetarAto(primeira),
            primeira is null ? [] : [.. cadeias.CorrecoesDe(primeira).Select(projetarAto)],
            PodePublicar: impedimento is null,
            impedimento);
    }

    /// <summary>A situação do item, dada a primeira publicação dele.</summary>
    internal static SituacaoDoProdutoPrevisto SituacaoDe(AtoDoProcesso? primeiraPublicacao) =>
        primeiraPublicacao is null ? SituacaoDoProdutoPrevisto.Aberto : SituacaoDoProdutoPrevisto.Publicado;

    /// <summary>
    /// O que impede o item de ser publicado agora, ou nulo quando pode sair: é a mesma pergunta que a
    /// consulta responde e que o comando de publicação confere antes de publicar o ato.
    /// </summary>
    internal static ImpedimentoDoProdutoDto? ImpedimentoDe(
        Declarado produto,
        SituacaoDoProdutoPrevisto situacao,
        ConfiguracaoPublicada configuracaoPublicada,
        CadeiasDosProdutosPrevistos cadeias)
    {
        if (situacao == SituacaoDoProdutoPrevisto.Publicado)
        {
            return new(ImpedimentoDoProduto.JaPublicado, MensagemDoItemJaPublicado);
        }

        if (produto.Papel == PapelDaPublicacaoPrevista.Definitivo && ImpedimentoDoPreliminar(produto, configuracaoPublicada, cadeias) is { } doPreliminar)
        {
            return doPreliminar;
        }

        return null;
    }

    private static ImpedimentoDoProdutoDto? ImpedimentoParaPublicar(
        Declarado produto,
        SituacaoDoProdutoPrevisto situacao,
        ConfiguracaoVigente vigente,
        CadeiasDosProdutosPrevistos cadeias,
        IReadOnlySet<string> tiposVigentes,
        string? nomeDoTipo) =>
        ImpedimentoDe(produto, situacao, vigente.Configuracao, cadeias)
            ?? ImpedimentoDaVigencia(tiposVigentes.Contains(produto.AtoCodigo), nomeDoTipo);

    private static ImpedimentoDoProdutoDto? ImpedimentoDaVigencia(bool tipoVigente, string? nomeDoTipo) =>
        tipoVigente
            ? null
            : new(ImpedimentoDoProduto.TipoDeAtoNaoVigente, MensagemDoTipoSemVersaoVigente(nomeDoTipo));

    private static string MensagemDoTipoSemVersaoVigente(string? nomeDoTipo) =>
        nomeDoTipo is null
            ? "O tipo de ato do item não tem versão vigente no catálogo de Publicações."
            : $"O tipo de ato '{nomeDoTipo}' não tem versão vigente no catálogo de Publicações.";

    private static ImpedimentoDoProdutoDto? ImpedimentoDoPreliminar(Declarado definitivo, ConfiguracaoPublicada configuracaoPublicada, CadeiasDosProdutosPrevistos cadeias)
    {
        Dictionary<Guid, IdentidadeDoItemDoEdital> itens = IdentidadeDoItemDoEdital.Dos(configuracaoPublicada.Cronograma.CronogramaFases, configuracaoPublicada.Avaliacao.Etapas);
        foreach (Guid preliminarId in PreliminaresDaMateria.Esperados(configuracaoPublicada, definitivo.Id))
        {
            if (cadeias.PrimeiraPublicacaoDe(itens[preliminarId]) is null)
            {
                return new(
                    ImpedimentoDoProduto.PreliminarNaoPublicado,
                    "O resultado definitivo só pode ser publicado depois do preliminar da mesma matéria, que ainda não foi publicado.");
            }
        }

        return null;
    }
}
