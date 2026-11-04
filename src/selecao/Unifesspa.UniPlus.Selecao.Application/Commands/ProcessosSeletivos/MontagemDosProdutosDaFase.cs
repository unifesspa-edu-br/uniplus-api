namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Monta os produtos de uma fase: cada código é resolvido contra o catálogo vigente, ou contra a versão
/// publicada quando o tipo já foi declarado, e o papel só é aceito onde o ato É resultado (ADR-0056). A
/// classificação é intrínseca ao tipo e nunca varia por edital, e os atributos que decidem a âncora do
/// recurso são lidos adiante e descartados, porque evoluem com o cadastro e não são copiados para a fase.
/// </summary>
/// <remarks>
/// Uma coleção mal declarada erra em vários itens ao mesmo tempo, então as recusas de produto são
/// acumuladas. Só a versão publicada ilegível interrompe.
/// </remarks>
internal sealed class MontagemDosProdutosDaFase(AtosConsultadosNaEdicao atos)
{
    /// <summary>
    /// Os produtos íntegros da fase. As recusas dos demais entram em <paramref name="recusas"/>; sem
    /// produtos íntegros não há o que a fábrica da fase avaliar, e chamá-la reportaria violações
    /// inventadas pela falha anterior.
    /// </summary>
    public async Task<Result<ProdutosDaFaseMontados>> MontarAsync(
        FaseCronogramaInput fase, int indice, string faseCodigo, List<FieldError> recusas, CancellationToken cancellationToken)
    {
        RotuloDoDonoDoProduto dono = RotuloDoDonoDoProduto.DaFase(faseCodigo);
        List<ProdutoDaFase> produtos = [];
        bool integros = true;
        for (int posicao = 0; posicao < fase.Produtos.Count; posicao++)
        {
            string campo = $"fases[{indice}].produtos[{posicao}]";
            Result<ProdutoDaFase?> produto = await ProdutoAsync(fase.Produtos[posicao], campo, dono, recusas, cancellationToken)
                .ConfigureAwait(false);
            if (produto.IsFailure)
            {
                return Result<ProdutosDaFaseMontados>.ValidationFailure(produto.Errors);
            }

            if (produto.Value is null)
            {
                integros = false;
                continue;
            }

            produtos.Add(produto.Value);
        }

        return Result<ProdutosDaFaseMontados>.Success(new ProdutosDaFaseMontados(produtos, integros));
    }

    // Sucesso nulo é a recusa do produto, já acumulada; falha é a interrupção.
    private async Task<Result<ProdutoDaFase?>> ProdutoAsync(
        ProdutoDaFaseInput declarado, string campo, RotuloDoDonoDoProduto dono, List<FieldError> recusas, CancellationToken cancellationToken)
    {
        if (!PapelProdutoFaseCodigo.TentarConverter(declarado.Papel, out PapelProdutoFase? papel))
        {
            recusas.Add(new($"{campo}.papel", ConferenciaDoProdutoDoAto.PapelDesconhecido(declarado.Papel, dono)));
            return Result<ProdutoDaFase?>.Success(null);
        }

        Result<FieldError?> conferido = await ConferirAtoAsync(declarado, papel, campo, dono, cancellationToken).ConfigureAwait(false);
        if (conferido.IsFailure)
        {
            return Result<ProdutoDaFase?>.ValidationFailure(conferido.Errors);
        }

        if (conferido.Value is { } recusa)
        {
            recusas.Add(recusa);
            return Result<ProdutoDaFase?>.Success(null);
        }

        return Result<ProdutoDaFase?>.Success(ProdutoDaFase.Criar(declarado.AtoCodigo, papel));
    }

    // Sucesso nulo é o ato conferido; sucesso com valor é a recusa a acumular; falha é a interrupção.
    private async Task<Result<FieldError?>> ConferirAtoAsync(
        ProdutoDaFaseInput declarado, PapelProdutoFase? papel, string campo, RotuloDoDonoDoProduto dono, CancellationToken cancellationToken)
    {
        Result<ClassificacaoDoTipoDeAto?> classificada = await atos
            .ClassificacaoAsync(declarado.AtoCodigo, cancellationToken).ConfigureAwait(false);
        if (classificada.IsFailure)
        {
            return InterrupcaoDaFase.Com<FieldError?>($"{campo}.atoCodigo", classificada.Error!);
        }

        if (classificada.Value is not { } classificacao)
        {
            return Result<FieldError?>.Success(
                new FieldError($"{campo}.atoCodigo", ConferenciaDoProdutoDoAto.AtoNaoEncontrado(declarado.AtoCodigo, dono)));
        }

        DomainError? recusaDoPapel = ConferenciaDoProdutoDoAto.RecusaDoPapel(declarado.AtoCodigo, papel, classificacao, dono);
        return Result<FieldError?>.Success(recusaDoPapel is null ? null : new FieldError($"{campo}.papel", recusaDoPapel));
    }
}

/// <summary>Os produtos da fase que passaram na conferência e se algum produto foi recusado.</summary>
internal sealed record ProdutosDaFaseMontados(List<ProdutoDaFase> Produtos, bool Integros);
