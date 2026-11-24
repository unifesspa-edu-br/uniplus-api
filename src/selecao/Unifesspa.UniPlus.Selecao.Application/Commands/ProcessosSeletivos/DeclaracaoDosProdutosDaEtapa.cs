namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;

using Kernel.Results;

/// <summary>
/// Declara os produtos de uma etapa, depois que a etapa existe: é ela quem os vincula, e a recusa de ato
/// repetido é dela. As recusas de todos os produtos se acumulam, cada uma em <c>produtos[posicao]</c>, e a etapa
/// só confere a repetição quando todos os produtos passaram.
/// </summary>
/// <remarks>
/// A falha é a interrupção: a versão publicada do tipo de ato não pôde ser lida, e a falha leva junto as
/// recusas já acumuladas. O sucesso traz as recusas dos produtos, e a lista vazia é produtos declarados.
/// </remarks>
internal sealed class DeclaracaoDosProdutosDaEtapa(AtosConsultadosNaEdicao atos)
{
    private readonly ConferenciaDoProdutoDeclarado _conferencia = new(atos);

    public async Task<Result<IReadOnlyList<FieldError>>> DeclararAsync(
        EtapaProcesso etapa, EtapaProcessoInput input, CancellationToken cancellationToken)
    {
        Result<ProdutosConferidos<ProdutoDaEtapa>> conferidos = await _conferencia
            .ConferirTodosAsync(
                [.. input.Produtos.Select(static p => (p.AtoCodigo, p.Papel))],
                RotuloDoDonoDoProduto.DaEtapa(input.Nome),
                JaDeclarados(etapa),
                ProdutoDaEtapa.Criar,
                cancellationToken)
            .ConfigureAwait(false);
        return conferidos.IsFailure
            ? Result<IReadOnlyList<FieldError>>.ValidationFailure(conferidos.Errors)
            : Result<IReadOnlyList<FieldError>>.Success(Recusas(etapa, conferidos.Value!));
    }

    // A repetição do par ato e papel só é julgada com todos os produtos íntegros: a etapa é quem a recusa.
    private static IReadOnlyList<FieldError> Recusas(EtapaProcesso etapa, ProdutosConferidos<ProdutoDaEtapa> conferidos) =>
        conferidos.Integros ? etapa.DefinirProdutos(conferidos.Produtos).Errors : conferidos.Recusas;

    private static List<(string AtoCodigo, PapelDaPublicacaoPrevista? Papel)> JaDeclarados(EtapaProcesso etapa) =>
        [.. etapa.Produtos.Select(static p => (p.AtoCodigo, p.Papel))];
}
