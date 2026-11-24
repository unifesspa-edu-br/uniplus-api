namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;

using Kernel.Results;

/// <summary>
/// Declara, em uma etapa que já existe, os produtos, a janela, os tipos de decisão, as bancas e as janelas
/// recursais, nessa ordem, e acumula as recusas de todos eles (ADR-0125): o recurso se ancora num produto, e é a etapa
/// quem recusa o ato repetido. A recusa sem campo sai com o do grupo que a produziu, para a resposta localizá-la. O
/// catálogo ilegível interrompe a declaração, e nada mais da etapa é julgado.
/// </summary>
internal sealed class DeclaracaoDosFilhosDasEtapas(
    DeclaracaoDosProdutosDaEtapa produtos,
    DeclaracaoDasBancasDaEtapa bancas,
    DeclaracaoDosRecursosDaEtapa recursos)
{
    /// <summary>
    /// A falha é a interrupção do catálogo ilegível nos produtos, com as recusas já acumuladas. O sucesso traz as
    /// recusas da etapa, e a lista vazia é etapa declarada.
    /// </summary>
    public async Task<Result<IReadOnlyList<FieldError>>> DeclararAsync(
        EtapaProcesso etapa, EtapaProcessoInput input, CancellationToken cancellationToken)
    {
        // As âncoras que a etapa já tinha antes desta gravação: o cliente devolve o que leu, e uma âncora
        // que já estava no agregado não é revista pelo que o catálogo diz hoje do ato.
        HashSet<string> atosJaAncorados = AtosJaAncorados(etapa);

        Result<IReadOnlyList<FieldError>> doProduto = await produtos.DeclararAsync(etapa, input, cancellationToken).ConfigureAwait(false);
        return doProduto.IsFailure
            ? doProduto
            : Result<IReadOnlyList<FieldError>>.Success(
                await DeclararOsDemaisAsync(etapa, input, doProduto.Value!, atosJaAncorados, cancellationToken).ConfigureAwait(false));
    }

    private async Task<IReadOnlyList<FieldError>> DeclararOsDemaisAsync(
        EtapaProcesso etapa,
        EtapaProcessoInput input,
        IReadOnlyList<FieldError> recusasDosProdutos,
        IReadOnlySet<string> atosJaAncorados,
        CancellationToken cancellationToken)
    {
        List<FieldError> recusas = [.. recusasDosProdutos, .. DeclararJanelaETiposDeDecisao(etapa, input)];
        recusas.AddRange(Recusas(await bancas.DeclararAsync(etapa, input, cancellationToken).ConfigureAwait(false), "bancas"));
        recusas.AddRange(await RecursosAsync(recusasDosProdutos, etapa, input, atosJaAncorados, cancellationToken).ConfigureAwait(false));
        return recusas;
    }

    // O recurso só deixa de ser declarado quando o produto que o ancora foi recusado: a âncora lê os produtos da etapa.
    private async Task<IEnumerable<FieldError>> RecursosAsync(
        IReadOnlyList<FieldError> recusasDosProdutos,
        EtapaProcesso etapa,
        EtapaProcessoInput input,
        IReadOnlySet<string> atosJaAncorados,
        CancellationToken cancellationToken) =>
        recusasDosProdutos.Count > 0
            ? []
            : Recusas(await recursos.DeclararAsync(etapa, input, atosJaAncorados, cancellationToken).ConfigureAwait(false), "recursos");

    private static IEnumerable<FieldError> DeclararJanelaETiposDeDecisao(EtapaProcesso etapa, EtapaProcessoInput input) =>
    [
        .. Recusas(etapa.DefinirJanelaEParecer(input.Inicio, input.Fim, input.EmiteParecerIndividual), "fim"),
        .. etapa.DefinirTiposDeDecisao(input.TiposDeDecisao).Errors,
    ];

    private static IEnumerable<FieldError> Recusas(Result declarada, string campo) =>
        declarada.Errors.Select(erro => erro.Field is null ? erro with { Field = campo } : erro);

    private static HashSet<string> AtosJaAncorados(EtapaProcesso etapa) =>
        [.. etapa.Recursos.SelectMany(recurso => etapa.Produtos
            .Where(p => p.Id == recurso.ProdutoAncoraId && p.Papel == PapelDaPublicacaoPrevista.Preliminar)
            .Select(static p => p.AtoCodigo))];
}
