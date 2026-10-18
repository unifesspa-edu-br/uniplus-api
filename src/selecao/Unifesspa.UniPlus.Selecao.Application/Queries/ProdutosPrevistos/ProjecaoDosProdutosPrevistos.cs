namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProdutosPrevistos;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
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
    private sealed record Declarado(Guid Id, string AtoCodigo, PapelProdutoFase? Papel);

    /// <summary>Os códigos dos tipos de ato que o cronograma declara publicar.</summary>
    public static IReadOnlyList<string> TiposDeAtoDeclarados(GrafoConfiguracao grafo)
    {
        ArgumentNullException.ThrowIfNull(grafo);

        return
        [
            .. grafo.CronogramaFases.SelectMany(static f => f.Produtos.Select(static p => p.AtoCodigo))
                .Concat(grafo.Etapas.SelectMany(static e => e.Produtos.Select(static p => p.AtoCodigo)))
                .Distinct(StringComparer.Ordinal),
        ];
    }

    /// <summary>Os identificadores dos produtos que o cronograma declara publicar.</summary>
    public static IReadOnlyList<Guid> ProdutosDeclarados(GrafoConfiguracao grafo)
    {
        ArgumentNullException.ThrowIfNull(grafo);

        return
        [
            .. grafo.CronogramaFases.SelectMany(static f => f.Produtos.Select(static p => p.Id))
                .Concat(grafo.Etapas.SelectMany(static e => e.Produtos.Select(static p => p.Id)))
                .Distinct(),
        ];
    }

    public static ProdutosPrevistosDoProcessoDto Projetar(
        int numeroDaVersao,
        GrafoConfiguracao grafo,
        CadeiasDosProdutosPrevistos cadeias,
        IReadOnlyDictionary<string, string> tiposVigentes,
        Func<AtoDoProcesso, AtoDoProcessoDto> projetarAto)
    {
        ArgumentNullException.ThrowIfNull(grafo);
        ArgumentNullException.ThrowIfNull(cadeias);
        ArgumentNullException.ThrowIfNull(tiposVigentes);
        ArgumentNullException.ThrowIfNull(projetarAto);

        EtapaComProdutosPrevistosDto Etapa(EtapaProcesso etapa) => new(
            etapa.Id,
            etapa.Nome,
            etapa.Ordem,
            Produtos([.. etapa.Produtos.Select(static p => new Declarado(p.Id, p.AtoCodigo, p.Papel))], grafo, cadeias, tiposVigentes, projetarAto));

        FaseComProdutosPrevistosDto Fase(FaseCronograma fase) => new(
            fase.Id,
            fase.Codigo,
            fase.Ordem,
            Produtos([.. fase.Produtos.Select(static p => new Declarado(p.Id, p.AtoCodigo, p.Papel))], grafo, cadeias, tiposVigentes, projetarAto),
            [.. grafo.Etapas
                .Where(e => string.Equals(e.FaseCodigo, fase.Codigo, StringComparison.Ordinal))
                .OrderBy(static e => e.Ordem ?? int.MaxValue)
                .ThenBy(static e => e.Nome, StringComparer.Ordinal)
                .Select(Etapa)]);

        return new ProdutosPrevistosDoProcessoDto(
            numeroDaVersao,
            [.. grafo.CronogramaFases.OrderBy(static f => f.Ordem).Select(Fase)],
            [.. grafo.Etapas
                .Where(static e => e.FaseCodigo is null)
                .OrderBy(static e => e.Ordem ?? int.MaxValue)
                .ThenBy(static e => e.Nome, StringComparer.Ordinal)
                .Select(Etapa)]);
    }

    private static List<ProdutoPrevistoDto> Produtos(
        List<Declarado> declarados,
        GrafoConfiguracao grafo,
        CadeiasDosProdutosPrevistos cadeias,
        IReadOnlyDictionary<string, string> tiposVigentes,
        Func<AtoDoProcesso, AtoDoProcessoDto> projetarAto) =>
        [.. declarados.Select(d => Produto(d, grafo, cadeias, tiposVigentes, projetarAto))];

    private static ProdutoPrevistoDto Produto(
        Declarado produto,
        GrafoConfiguracao grafo,
        CadeiasDosProdutosPrevistos cadeias,
        IReadOnlyDictionary<string, string> tiposVigentes,
        Func<AtoDoProcesso, AtoDoProcessoDto> projetarAto)
    {
        AtoDoProcesso? primeira = cadeias.PrimeiraPublicacaoDe(produto.Id);
        SituacaoDoProdutoPrevisto situacao = primeira switch
        {
            null => SituacaoDoProdutoPrevisto.Aberto,
            { Situacao: SituacaoDoRegistroDoAto.Registrado } => SituacaoDoProdutoPrevisto.Publicado,
            _ => SituacaoDoProdutoPrevisto.Pendente,
        };
        bool tipoVigente = tiposVigentes.ContainsKey(produto.AtoCodigo);
        ImpedimentoDoProdutoDto? impedimento = ImpedimentoDe(produto, situacao, grafo, cadeias, tipoVigente);

        return new ProdutoPrevistoDto(
            produto.Id,
            produto.AtoCodigo,
            tiposVigentes.GetValueOrDefault(produto.AtoCodigo) ?? primeira?.TipoNome,
            produto.Papel.ToCodigo(),
            situacao,
            primeira is null ? null : projetarAto(primeira),
            primeira is null ? [] : [.. cadeias.CorrecoesDe(primeira).Select(projetarAto)],
            PodePublicar: impedimento is null,
            impedimento);
    }

    private static ImpedimentoDoProdutoDto? ImpedimentoDe(
        Declarado produto,
        SituacaoDoProdutoPrevisto situacao,
        GrafoConfiguracao grafo,
        CadeiasDosProdutosPrevistos cadeias,
        bool tipoVigente)
    {
        switch (situacao)
        {
            case SituacaoDoProdutoPrevisto.Publicado:
                return new(ImpedimentoDoProduto.JaPublicado, "O item já foi publicado. Para corrigi-lo, publique uma errata ou retificação do ato.");
            case SituacaoDoProdutoPrevisto.Pendente:
                return new(ImpedimentoDoProduto.AguardaConfirmacaoDoRegistro, "O item foi enviado ao Publicações e aguarda a confirmação do registro.");
        }

        if (produto.Papel == PapelProdutoFase.Definitivo && ImpedimentoDoPreliminar(produto, grafo, cadeias) is { } doPreliminar)
        {
            return doPreliminar;
        }

        return tipoVigente
            ? null
            : new(ImpedimentoDoProduto.TipoDeAtoNaoVigente, $"O tipo de ato '{produto.AtoCodigo}' não tem versão vigente no catálogo de Publicações.");
    }

    private static ImpedimentoDoProdutoDto? ImpedimentoDoPreliminar(Declarado definitivo, GrafoConfiguracao grafo, CadeiasDosProdutosPrevistos cadeias)
    {
        foreach (Guid preliminarId in PreliminaresDaMateria.Esperados(grafo, definitivo.Id))
        {
            AtoDoProcesso? ato = cadeias.PrimeiraPublicacaoDe(preliminarId);
            if (ato is null)
            {
                return new(
                    ImpedimentoDoProduto.PreliminarNaoPublicado,
                    "O resultado definitivo só pode ser publicado depois do preliminar da mesma matéria, que ainda não foi publicado.");
            }

            if (ato.Situacao != SituacaoDoRegistroDoAto.Registrado)
            {
                return new(
                    ImpedimentoDoProduto.PreliminarAguardaConfirmacao,
                    "O resultado preliminar da mesma matéria foi enviado ao Publicações e aguarda a confirmação do registro.");
            }
        }

        return null;
    }
}
