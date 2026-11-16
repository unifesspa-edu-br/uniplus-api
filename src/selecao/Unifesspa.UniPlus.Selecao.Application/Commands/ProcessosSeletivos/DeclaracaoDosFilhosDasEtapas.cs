namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;

using Kernel.Results;

/// <summary>
/// Declara, em uma etapa que já existe, os produtos, a janela, os tipos de decisão, as bancas e as janelas
/// recursais, nessa ordem: o recurso se ancora num produto, e é a etapa quem recusa o ato repetido.
/// </summary>
internal sealed class DeclaracaoDosFilhosDasEtapas(
    DeclaracaoDosProdutosDaEtapa produtos,
    DeclaracaoDasBancasDaEtapa bancas,
    DeclaracaoDosRecursosDaEtapa recursos)
{
    public async Task<Result> DeclararAsync(EtapaProcesso etapa, EtapaProcessoInput input, CancellationToken cancellationToken)
    {
        // As âncoras que a etapa já tinha antes desta gravação: o cliente devolve o que leu, e uma âncora
        // que já estava no agregado não é revista pelo que o catálogo diz hoje do ato.
        HashSet<string> atosJaAncorados = AtosJaAncorados(etapa);

        Result declaradosOsProdutos = await produtos.DeclararAsync(etapa, input, cancellationToken).ConfigureAwait(false);
        if (declaradosOsProdutos.IsFailure)
        {
            return declaradosOsProdutos;
        }

        Result propria = DeclararJanelaETiposDeDecisao(etapa, input);
        if (propria.IsFailure)
        {
            return propria;
        }

        Result declaradasAsBancas = await bancas.DeclararAsync(etapa, input, cancellationToken).ConfigureAwait(false);
        return declaradasAsBancas.IsFailure
            ? declaradasAsBancas
            : await recursos.DeclararAsync(etapa, input, atosJaAncorados, cancellationToken).ConfigureAwait(false);
    }

    private static Result DeclararJanelaETiposDeDecisao(EtapaProcesso etapa, EtapaProcessoInput input)
    {
        Result janela = etapa.DefinirJanelaEParecer(input.Inicio, input.Fim, input.EmiteParecerIndividual);
        return janela.IsFailure ? janela : etapa.DefinirTiposDeDecisao(input.TiposDeDecisao);
    }

    private static HashSet<string> AtosJaAncorados(EtapaProcesso etapa) =>
        [.. etapa.Recursos.SelectMany(recurso => etapa.Produtos
            .Where(p => p.Id == recurso.ProdutoAncoraId && p.Papel == PapelProdutoFase.Preliminar)
            .Select(static p => p.AtoCodigo))];
}
