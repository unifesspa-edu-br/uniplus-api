namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Services;

using Unifesspa.UniPlus.Kernel.Results;

using ProdutoConferido = (Unifesspa.UniPlus.Selecao.Domain.ValueObjects.IdentidadeDoItemDoEdital Item, Unifesspa.UniPlus.Publicacoes.Contracts.TipoAtoPublicadoView Tipo, string? ContaId);

/// <summary>Prepara o item do edital sem escrever, interrompendo na primeira recusa.</summary>
public sealed class PreparacaoDaPublicacaoDoProdutoDoEdital(
    ConferenciaDoProcessoDivulgado processos,
    LeituraDoInstanteDaPublicacao instantes,
    ConferenciaDoProdutoDoEdital produtos,
    ConferenciaDosDocumentosDoAto documentos)
{
    internal async Task<Result<AtoAPublicar>> PrepararAsync(PublicarProdutoDoEditalCommand command, CancellationToken cancellationToken)
    {
        Result<ProcessoDivulgadoConferido> processo = await processos.ConferirAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        if (processo.IsFailure)
        {
            return Result<AtoAPublicar>.Failure(processo.Error!);
        }
        Result<InstanteDaPublicacao> instante = instantes.Ler();
        return instante.IsFailure
            ? Result<AtoAPublicar>.Failure(instante.Error!)
            : await ConferirProdutoAsync(command, processo.Value!, instante.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AtoAPublicar>> ConferirProdutoAsync(
        PublicarProdutoDoEditalCommand command, ProcessoDivulgadoConferido processo,
        InstanteDaPublicacao instante, CancellationToken cancellationToken)
    {
        Result<ProdutoConferido> produto = await produtos.ConferirAsync(command, instante, cancellationToken).ConfigureAwait(false);
        return produto.IsFailure
            ? Result<AtoAPublicar>.Failure(produto.Error!)
            : await ConferirDocumentosAsync(command, processo, instante.Instante, produto.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AtoAPublicar>> ConferirDocumentosAsync(
        PublicarProdutoDoEditalCommand command, ProcessoDivulgadoConferido processo,
        DateTimeOffset instante, ProdutoConferido produto, CancellationToken cancellationToken)
    {
        Result<DocumentosDoAtoConferidos> conferidos = await documentos
            .ConferirAsync(command.ProcessoSeletivoId, command.DocumentoPrincipalId, command.Anexos ?? [], cancellationToken).ConfigureAwait(false);
        return conferidos.IsFailure
            ? Result<AtoAPublicar>.Failure(conferidos.Error!)
            : Result<AtoAPublicar>.Success(new(command, processo, produto.Tipo, conferidos.Value!, instante, produto.Item, produto.ContaId));
    }
}
