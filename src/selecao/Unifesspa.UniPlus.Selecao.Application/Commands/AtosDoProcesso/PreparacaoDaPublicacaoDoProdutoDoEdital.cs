namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Services;

using Unifesspa.UniPlus.Kernel.Results;

using ProdutoConferido = (Unifesspa.UniPlus.Selecao.Domain.ValueObjects.IdentidadeDoItemDoEdital Item, Unifesspa.UniPlus.Publicacoes.Contracts.TipoAtoPublicadoView Tipo, string? ContaId);

/// <summary>Prepara o item do edital sem escrever, interrompendo na primeira recusa.</summary>
public sealed class PreparacaoDaPublicacaoDoProdutoDoEdital(
    ConferenciaDoProcessoDivulgado processos,
    LeituraDoInstanteDoComando instantes,
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
        Result<InstanteDoComando> instante = instantes.Ler();
        return instante.IsFailure
            ? Result<AtoAPublicar>.Failure(instante.Error!)
            : AusenciaDaDataDoAto.Concluir(
                command, await ConferirAsync(command, processo.Value!, instante.Value, cancellationToken).ConfigureAwait(false));
    }

    // A recusa do item não impede a conferência da ausência dos documentos, que não depende dela.
    private async Task<Result<AtoConferido>> ConferirAsync(
        PublicarProdutoDoEditalCommand command, ProcessoDivulgadoConferido processo,
        InstanteDoComando instante, CancellationToken cancellationToken)
    {
        Result<ProdutoConferido> produto = await produtos.ConferirAsync(command, instante, cancellationToken).ConfigureAwait(false);
        if (produto.IsFailure)
        {
            return Result<AtoConferido>.ValidationFailure(
                [.. produto.Errors, .. ConferenciaDosDocumentosDoAto.DocumentosAusentes(command.DocumentoPrincipalId, command.Anexos ?? [])]);
        }

        Result<DocumentosDoAtoConferidos> conferidos = await documentos
            .ConferirAsync(command.ProcessoSeletivoId, command.DocumentoPrincipalId, command.Anexos ?? [], cancellationToken).ConfigureAwait(false);
        return conferidos.IsFailure
            ? Result<AtoConferido>.ValidationFailure(conferidos.Errors)
            : Result<AtoConferido>.Success(new(processo, produto.Value.Tipo, conferidos.Value!, instante.Instante, produto.Value.Item, produto.Value.ContaId));
    }
}
