namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Enums;

using Kernel.Results;

using Services;

/// <summary>
/// Confere um produto declarado, por etapa ou por fase, contra o catálogo de tipos de ato. A conferência é a
/// mesma para os dois donos; muda o rótulo da recusa e o que cada dono faz com ela.
/// </summary>
/// <remarks>
/// O desfecho tem dois caminhos: a falha é a interrupção, porque a versão publicada do tipo não pôde ser lida;
/// a recusa do produto é um desfecho, e não uma falha.
/// </remarks>
internal sealed class ConferenciaDoProdutoDeclarado(AtosConsultadosNaEdicao atos)
{
    /// <summary>
    /// Os pares (ato, papel) que o dono já tinha não consultam o catálogo: aposentar no catálogo um ato já
    /// declarado bloquearia toda gravação posterior da coleção, porque o cliente devolve o produto que leu.
    /// O campo da recusa é relativo ao dono: <c>produtos[posicao].papel</c> ou <c>produtos[posicao].atoCodigo</c>.
    /// </summary>
    public async Task<Result<ProdutoConferido>> ConferirAsync(
        string atoCodigo,
        string? papelDeclarado,
        int posicao,
        RotuloDoDonoDoProduto dono,
        IReadOnlyCollection<(string AtoCodigo, PapelDaPublicacaoPrevista? Papel)> jaDeclarados,
        CancellationToken cancellationToken)
    {
        string campo = $"produtos[{posicao}]";
        if (!CodigoLidoDoPedido.TentarLerOpcional(papelDeclarado, out PapelDaPublicacaoPrevista? papel))
        {
            return Recusa($"{campo}.papel", ConferenciaDoProdutoDoAto.PapelDesconhecido(dono));
        }

        return jaDeclarados.Any(p => string.Equals(p.AtoCodigo, atoCodigo, StringComparison.Ordinal) && p.Papel == papel)
            ? Result<ProdutoConferido>.Success(new ProdutoConferido.Aceito(atoCodigo, papel))
            : await ConferirNoCatalogoAsync(atoCodigo, papel, campo, dono, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Confere todos os produtos declarados do dono, na ordem, e acumula as recusas: uma coleção mal declarada
    /// erra em vários itens ao mesmo tempo. Só o catálogo ilegível interrompe, e a falha leva junto as recusas já
    /// acumuladas. Os campos das recusas são relativos ao dono, sem o seu prefixo.
    /// </summary>
    public async Task<Result<ProdutosConferidos<TProduto>>> ConferirTodosAsync<TProduto>(
        IReadOnlyList<(string AtoCodigo, string? Papel)> declarados,
        RotuloDoDonoDoProduto dono,
        IReadOnlyCollection<(string AtoCodigo, PapelDaPublicacaoPrevista? Papel)> jaDeclarados,
        Func<string, PapelDaPublicacaoPrevista?, TProduto> criar,
        CancellationToken cancellationToken)
    {
        ProdutosConferidos<TProduto> conferidos = new([], []);
        for (int posicao = 0; posicao < declarados.Count; posicao++)
        {
            Result<ProdutoConferido> conferido = await ConferirAsync(declarados[posicao], posicao, dono, jaDeclarados, cancellationToken)
                .ConfigureAwait(false);
            if (conferido.IsFailure)
            {
                return Result<ProdutosConferidos<TProduto>>.ValidationFailure([.. conferidos.Recusas, .. conferido.Errors]);
            }

            Acumular(conferido.Value!, conferidos, criar);
        }

        return Result<ProdutosConferidos<TProduto>>.Success(conferidos);
    }

    private Task<Result<ProdutoConferido>> ConferirAsync(
        (string AtoCodigo, string? Papel) declarado,
        int posicao,
        RotuloDoDonoDoProduto dono,
        IReadOnlyCollection<(string AtoCodigo, PapelDaPublicacaoPrevista? Papel)> jaDeclarados,
        CancellationToken cancellationToken) =>
        ConferirAsync(declarado.AtoCodigo, declarado.Papel, posicao, dono, jaDeclarados, cancellationToken);

    private static void Acumular<TProduto>(
        ProdutoConferido conferido, ProdutosConferidos<TProduto> conferidos, Func<string, PapelDaPublicacaoPrevista?, TProduto> criar)
    {
        switch (conferido)
        {
            case ProdutoConferido.Aceito aceito:
                conferidos.Produtos.Add(criar(aceito.AtoCodigo, aceito.Papel));
                break;
            case ProdutoConferido.Recusado recusado:
                conferidos.Recusas.Add(recusado.Recusa);
                break;
        }
    }

    private async Task<Result<ProdutoConferido>> ConferirNoCatalogoAsync(
        string atoCodigo, PapelDaPublicacaoPrevista? papel, string campo, RotuloDoDonoDoProduto dono, CancellationToken cancellationToken)
    {
        Result<ClassificacaoDoAtoConsultado> classificada = await atos
            .ClassificacaoAsync(atoCodigo, cancellationToken).ConfigureAwait(false);
        if (classificada.IsFailure)
        {
            return InterrupcaoDaEdicao.Com<ProdutoConferido>($"{campo}.atoCodigo", classificada.Error!);
        }

        return Desfecho(atoCodigo, papel, campo, dono, classificada.Value!);
    }

    private static Result<ProdutoConferido> Desfecho(
        string atoCodigo, PapelDaPublicacaoPrevista? papel, string campo, RotuloDoDonoDoProduto dono, ClassificacaoDoAtoConsultado classificada)
    {
        if (classificada is not ClassificacaoDoAtoConsultado.Conhecida conhecida)
        {
            return Recusa($"{campo}.atoCodigo", ConferenciaDoProdutoDoAto.AtoNaoEncontrado(dono));
        }

        return ConferenciaDoProdutoDoAto.RecusaDoPapel(papel, conhecida, dono) is { } recusaDoPapel
            ? Recusa($"{campo}.papel", recusaDoPapel)
            : Result<ProdutoConferido>.Success(new ProdutoConferido.Aceito(atoCodigo, papel));
    }

    private static Result<ProdutoConferido> Recusa(string campo, DomainError erro) =>
        Result<ProdutoConferido>.Success(new ProdutoConferido.Recusado(new FieldError(campo, erro)));
}
