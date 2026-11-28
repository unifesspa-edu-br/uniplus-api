namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Services;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>Prepara o ato avulso sem escrever, interrompendo na primeira recusa.</summary>
public sealed class PreparacaoDaPublicacaoDoAtoAvulso(
    ConferenciaDoProcessoDivulgado processos,
    LeituraDoInstanteDoComando instantes,
    ConferenciaDoTipoDoAtoAvulso tipos,
    ConferenciaDoAtoCorrigido correcoes,
    ConferenciaDosDocumentosDoAto documentos)
{
    internal async Task<Result<AtoAPublicar>> PrepararAsync(PublicarAtoAvulsoDoProcessoCommand command, CancellationToken cancellationToken)
    {
        Result<ProcessoDivulgadoConferido> processo = await processos.ConferirAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        if (processo.IsFailure)
        {
            return Result<AtoAPublicar>.Failure(processo.Error!);
        }
        Result<InstanteDoComando> instante = instantes.Ler();
        return instante.IsFailure
            ? Result<AtoAPublicar>.Failure(instante.Error!)
            : await ConferirTipoAsync(command, processo.Value!, instante.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AtoAPublicar>> ConferirTipoAsync(
        PublicarAtoAvulsoDoProcessoCommand command, ProcessoDivulgadoConferido processo,
        InstanteDoComando instante, CancellationToken cancellationToken)
    {
        Result<TipoAtoPublicadoView> tipo = await tipos.ConferirAsync(command.TipoAtoCodigo, instante.Dia, cancellationToken).ConfigureAwait(false);
        if (tipo.IsFailure)
        {
            return Result<AtoAPublicar>.Failure(tipo.Error!);
        }
        Result exigido = ConferenciaDoTipoDoAtoAvulso.ExigeAtoCorrigido(tipo.Value!, command.AtoCorrigidoId);
        return exigido.IsFailure
            ? Result<AtoAPublicar>.Failure(exigido.Error!)
            : await ConferirCorrecaoAsync(command, processo, tipo.Value!, instante.Instante, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AtoAPublicar>> ConferirCorrecaoAsync(
        PublicarAtoAvulsoDoProcessoCommand command, ProcessoDivulgadoConferido processo,
        TipoAtoPublicadoView tipo, DateTimeOffset instante, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<Guid>> corrigido = await correcoes.ConferirAsync(command.ProcessoSeletivoId, command.AtoCorrigidoId, cancellationToken).ConfigureAwait(false);
        if (corrigido.IsFailure)
        {
            return Result<AtoAPublicar>.Failure(corrigido.Error!);
        }
        Result item = await correcoes.NaoEhItemDoEditalAsync(corrigido.Value!, cancellationToken).ConfigureAwait(false);
        return item.IsFailure
            ? Result<AtoAPublicar>.Failure(item.Error!)
            : await ConferirDocumentosAsync(command, processo, tipo, instante, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AtoAPublicar>> ConferirDocumentosAsync(
        PublicarAtoAvulsoDoProcessoCommand command, ProcessoDivulgadoConferido processo,
        TipoAtoPublicadoView tipo, DateTimeOffset instante, CancellationToken cancellationToken)
    {
        Result<DocumentosDoAtoConferidos> conferidos = await documentos
            .ConferirAsync(command.ProcessoSeletivoId, command.DocumentoPrincipalId, command.Anexos ?? [], cancellationToken).ConfigureAwait(false);
        return conferidos.IsFailure
            ? Result<AtoAPublicar>.ValidationFailure(conferidos.Errors)
            : Result<AtoAPublicar>.Success(new(command, processo, tipo, conferidos.Value!, instante));
    }
}
