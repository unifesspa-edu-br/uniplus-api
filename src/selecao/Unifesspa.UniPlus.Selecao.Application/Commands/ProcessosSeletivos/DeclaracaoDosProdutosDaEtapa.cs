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

    // O papel desconhecido é RECUSADO, e não tratado como ausente: ignorá-lo fazia um erro de digitação
    // virar produto sem papel, e a etapa deixava de publicar resultado sem que nada dissesse isso.
    private async Task<Result<ProdutoDaEtapa>> ProdutoAsync(
        EtapaProcesso etapa, EtapaProcessoInput input, ProdutoDaEtapaInput declarado, CancellationToken cancellationToken)
    {
        if (!PapelProdutoFaseCodigo.TentarConverter(declarado.Papel, out PapelProdutoFase? papel))
        {
            return Result<ProdutoDaEtapa>.Failure(new DomainError(
                "ProdutoDaEtapa.PapelDesconhecido",
                $"O papel '{declarado.Papel}', declarado pela etapa '{input.Nome}', não é declarável — use '{PapelProdutoFaseCodigo.Preliminar}', '{PapelProdutoFaseCodigo.Definitivo}' ou nenhum."));
        }

        // O catálogo só confere o par (ato, papel) que a etapa passa a declarar. Aposentar no catálogo um
        // ato já declarado bloquearia todo PUT posterior da coleção, porque o endpoint substitui tudo e o
        // cliente devolve o produto que leu.
        DomainError? recusa = JaDeclarado(etapa, declarado, papel)
            ? null
            : await RecusaDoProdutoNovoAsync(input, declarado, papel, cancellationToken).ConfigureAwait(false);
        return recusa is null
            ? Result<ProdutoDaEtapa>.Success(ProdutoDaEtapa.Criar(declarado.AtoCodigo, papel))
            : Result<ProdutoDaEtapa>.Failure(recusa);
    }

    private static bool JaDeclarado(EtapaProcesso etapa, ProdutoDaEtapaInput declarado, PapelProdutoFase? papel) =>
        etapa.Produtos.Any(p => string.Equals(p.AtoCodigo, declarado.AtoCodigo, StringComparison.Ordinal) && p.Papel == papel);

    private async Task<DomainError?> RecusaDoProdutoNovoAsync(
        EtapaProcessoInput input, ProdutoDaEtapaInput declarado, PapelProdutoFase? papel, CancellationToken cancellationToken)
    {
        Result<ClassificacaoDoTipoDeAto?> classificada = await atos
            .ClassificacaoAsync(declarado.AtoCodigo, cancellationToken).ConfigureAwait(false);
        if (classificada.IsFailure)
        {
            return classificada.Error;
        }

        if (classificada.Value is not { } classificacao)
        {
            return new DomainError(
                "ProdutoDaEtapa.AtoNaoEncontradoNoCatalogo",
                $"O tipo de ato '{declarado.AtoCodigo}', declarado pela etapa '{input.Nome}', não tem versão vigente no catálogo de Publicações na data de hoje.");
        }

        // Papel preliminar ou definitivo só faz sentido sobre resultado, que é o par a que a janela recursal
        // se ancora.
        if (papel is not null && !classificacao.EhResultado)
        {
            return new DomainError(
                "ProdutoDaEtapa.PapelEmAtoQueNaoEhResultado",
                $"O tipo de ato '{declarado.AtoCodigo}', declarado pela etapa '{input.Nome}', não é resultado no catálogo — só resultado recebe papel preliminar ou definitivo.");
        }

        return classificacao.ViolacaoDoProduto(declarado.AtoCodigo, papel, $"na etapa '{input.Nome}'");
    }
}
