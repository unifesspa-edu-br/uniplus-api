namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;

using Kernel.Results;

/// <summary>
/// Declara, em uma etapa que já existe, os produtos, a janela, os tipos de decisão, as bancas e as janelas
/// recursais, nessa ordem, e acumula as recusas de todos eles (ADR-0125): o recurso se ancora num produto, e é a etapa
/// quem recusa o ato repetido. A recusa sem campo sai com o do grupo que a produziu, para a resposta localizá-la.
/// </summary>
internal sealed class DeclaracaoDosFilhosDasEtapas(
    DeclaracaoDosProdutosDaEtapa produtos,
    DeclaracaoDasBancasDaEtapa bancas,
    DeclaracaoDosRecursosDaEtapa recursos)
{
    public async Task<IReadOnlyList<FieldError>> DeclararAsync(
        EtapaProcesso etapa, EtapaProcessoInput input, CancellationToken cancellationToken)
    {
        // As âncoras que a etapa já tinha antes desta gravação: o cliente devolve o que leu, e uma âncora
        // que já estava no agregado não é revista pelo que o catálogo diz hoje do ato.
        HashSet<string> atosJaAncorados = AtosJaAncorados(etapa);

        Result declaradosOsProdutos = await produtos.DeclararAsync(etapa, input, cancellationToken).ConfigureAwait(false);
        List<FieldError> recusas = [.. Recusas(declaradosOsProdutos, "produtos"), .. DeclararJanelaETiposDeDecisao(etapa, input)];
        recusas.AddRange(Recusas(await bancas.DeclararAsync(etapa, input, cancellationToken).ConfigureAwait(false), "bancas"));
        recusas.AddRange(await RecursosAsync(declaradosOsProdutos, etapa, input, atosJaAncorados, cancellationToken).ConfigureAwait(false));
        return recusas;
    }

    // O recurso só deixa de ser declarado quando o produto que o ancora foi recusado: a âncora lê os produtos da etapa.
    private async Task<IEnumerable<FieldError>> RecursosAsync(
        Result declaradosOsProdutos,
        EtapaProcesso etapa,
        EtapaProcessoInput input,
        IReadOnlySet<string> atosJaAncorados,
        CancellationToken cancellationToken) =>
        declaradosOsProdutos.IsFailure
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
            .Where(p => p.Id == recurso.ProdutoAncoraId && p.Papel == PapelProdutoFase.Preliminar)
            .Select(static p => p.AtoCodigo))];
}
