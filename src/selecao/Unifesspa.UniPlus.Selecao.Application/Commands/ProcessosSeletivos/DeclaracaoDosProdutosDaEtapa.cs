namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Diagnostics;

using Domain.Entities;
using Domain.Enums;

using Kernel.Results;

/// <summary>
/// Declara os produtos de uma etapa, depois que a etapa existe: é ela quem os vincula, e a recusa de ato
/// repetido é dela.
/// </summary>
internal sealed class DeclaracaoDosProdutosDaEtapa(AtosConsultadosNaEdicao atos)
{
    private readonly ConferenciaDoProdutoDeclarado _conferencia = new(atos);

    public async Task<Result> DeclararAsync(EtapaProcesso etapa, EtapaProcessoInput input, CancellationToken cancellationToken)
    {
        List<ProdutoDaEtapa> produtos = [];
        for (int posicao = 0; posicao < input.Produtos.Count; posicao++)
        {
            Result<ProdutoDaEtapa> produto = await ProdutoAsync(etapa, input, posicao, cancellationToken).ConfigureAwait(false);
            if (produto.IsFailure)
            {
                return Result.Failure(produto.Error!);
            }

            produtos.Add(produto.Value!);
        }

        return etapa.DefinirProdutos(produtos);
    }

    private async Task<Result<ProdutoDaEtapa>> ProdutoAsync(
        EtapaProcesso etapa, EtapaProcessoInput input, int posicao, CancellationToken cancellationToken)
    {
        ProdutoDaEtapaInput declarado = input.Produtos[posicao];
        Result<ProdutoConferido> conferido = await _conferencia
            .ConferirAsync(
                declarado.AtoCodigo, declarado.Papel, posicao, RotuloDoDonoDoProduto.DaEtapa(input.Nome), JaDeclarados(etapa), cancellationToken)
            .ConfigureAwait(false);
        return conferido.IsFailure
            ? Result<ProdutoDaEtapa>.Failure(conferido.Error!)
            : DaEtapa(conferido.Value!);
    }

    private static Result<ProdutoDaEtapa> DaEtapa(ProdutoConferido conferido) =>
        conferido switch
        {
            ProdutoConferido.Aceito aceito => Result<ProdutoDaEtapa>.Success(ProdutoDaEtapa.Criar(aceito.AtoCodigo, aceito.Papel)),
            ProdutoConferido.Recusado recusado => Result<ProdutoDaEtapa>.Failure(recusado.Recusa.Error),
            _ => throw new UnreachableException(),
        };

    private static List<(string AtoCodigo, PapelProdutoFase? Papel)> JaDeclarados(EtapaProcesso etapa) =>
        [.. etapa.Produtos.Select(static p => (p.AtoCodigo, p.Papel))];
}
