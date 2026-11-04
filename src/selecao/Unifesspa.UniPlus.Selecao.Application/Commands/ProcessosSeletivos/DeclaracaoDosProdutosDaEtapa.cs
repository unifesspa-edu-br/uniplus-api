namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Declara os produtos de uma etapa, depois que a etapa existe: é ela quem os vincula, e a recusa de ato
/// repetido é dela.
/// </summary>
internal sealed class DeclaracaoDosProdutosDaEtapa(AtosConsultadosNaEdicao atos)
{
    public async Task<Result> DeclararAsync(EtapaProcesso etapa, EtapaProcessoInput input, CancellationToken cancellationToken)
    {
        List<ProdutoDaEtapa> produtos = [];
        foreach (ProdutoDaEtapaInput declarado in input.Produtos)
        {
            Result<ProdutoDaEtapa> produto = await ProdutoAsync(etapa, input, declarado, cancellationToken).ConfigureAwait(false);
            if (produto.IsFailure)
            {
                return Result.Failure(produto.Error!);
            }

            produtos.Add(produto.Value!);
        }

        return etapa.DefinirProdutos(produtos);
    }

    private async Task<Result<ProdutoDaEtapa>> ProdutoAsync(
        EtapaProcesso etapa, EtapaProcessoInput input, ProdutoDaEtapaInput declarado, CancellationToken cancellationToken)
    {
        RotuloDoDonoDoProduto dono = RotuloDoDonoDoProduto.DaEtapa(input.Nome);
        if (!PapelProdutoFaseCodigo.TentarConverter(declarado.Papel, out PapelProdutoFase? papel))
        {
            return Result<ProdutoDaEtapa>.Failure(ConferenciaDoProdutoDoAto.PapelDesconhecido(declarado.Papel, dono));
        }

        // O catálogo só confere o par (ato, papel) que a etapa passa a declarar. Aposentar no catálogo um
        // ato já declarado bloquearia todo PUT posterior da coleção, porque o endpoint substitui tudo e o
        // cliente devolve o produto que leu.
        DomainError? recusa = JaDeclarado(etapa, declarado, papel)
            ? null
            : await RecusaDoProdutoNovoAsync(declarado, papel, dono, cancellationToken).ConfigureAwait(false);
        return recusa is null
            ? Result<ProdutoDaEtapa>.Success(ProdutoDaEtapa.Criar(declarado.AtoCodigo, papel))
            : Result<ProdutoDaEtapa>.Failure(recusa);
    }

    private static bool JaDeclarado(EtapaProcesso etapa, ProdutoDaEtapaInput declarado, PapelProdutoFase? papel) =>
        etapa.Produtos.Any(p => string.Equals(p.AtoCodigo, declarado.AtoCodigo, StringComparison.Ordinal) && p.Papel == papel);

    private async Task<DomainError?> RecusaDoProdutoNovoAsync(
        ProdutoDaEtapaInput declarado, PapelProdutoFase? papel, RotuloDoDonoDoProduto dono, CancellationToken cancellationToken)
    {
        Result<ClassificacaoDoAtoConsultado> classificada = await atos
            .ClassificacaoAsync(declarado.AtoCodigo, cancellationToken).ConfigureAwait(false);
        if (classificada.IsFailure)
        {
            return classificada.Error;
        }

        return classificada.Value is ClassificacaoDoAtoConsultado.Conhecida conhecida
            ? ConferenciaDoProdutoDoAto.RecusaDoPapel(declarado.AtoCodigo, papel, conhecida.Classificacao, dono)
            : ConferenciaDoProdutoDoAto.AtoNaoEncontrado(declarado.AtoCodigo, dono);
    }
}
