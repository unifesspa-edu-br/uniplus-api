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
            : AusenciaDaDataDoAto.Concluir(
                command, await ConferirAsync(command, processo.Value!, instante.Value, cancellationToken).ConfigureAwait(false));
    }

    // A recusa do tipo ou da correção não impede a conferência da ausência dos documentos, que não depende delas.
    private async Task<Result<AtoConferido>> ConferirAsync(
        PublicarAtoAvulsoDoProcessoCommand command, ProcessoDivulgadoConferido processo,
        InstanteDoComando instante, CancellationToken cancellationToken)
    {
        Result<TipoAtoPublicadoView> tipo = await ConferirTipoECorrecaoAsync(command, instante, cancellationToken).ConfigureAwait(false);
        if (tipo.IsFailure)
        {
            return Result<AtoConferido>.ValidationFailure(
                [.. tipo.Errors, .. ConferenciaDosDocumentosDoAto.DocumentosAusentes(command.DocumentoPrincipalId, command.Anexos ?? [])]);
        }

        Result<DocumentosDoAtoConferidos> conferidos = await documentos
            .ConferirAsync(command.ProcessoSeletivoId, command.DocumentoPrincipalId, command.Anexos ?? [], cancellationToken).ConfigureAwait(false);
        return conferidos.IsFailure
            ? Result<AtoConferido>.ValidationFailure(conferidos.Errors)
            : Result<AtoConferido>.Success(new(processo, tipo.Value!, conferidos.Value!, instante.Instante));
    }

    private async Task<Result<TipoAtoPublicadoView>> ConferirTipoECorrecaoAsync(
        PublicarAtoAvulsoDoProcessoCommand command, InstanteDoComando instante, CancellationToken cancellationToken)
    {
        Result<TipoAtoPublicadoView> tipo = await ConferirTipoAsync(command, instante, cancellationToken).ConfigureAwait(false);
        return tipo.IsFailure ? tipo : await ConferirCorrecaoAsync(command, tipo.Value!, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<TipoAtoPublicadoView>> ConferirTipoAsync(
        PublicarAtoAvulsoDoProcessoCommand command, InstanteDoComando instante, CancellationToken cancellationToken)
    {
        Result<TipoAtoPublicadoView> tipo = await tipos.ConferirAsync(command.TipoAtoCodigo, instante.Dia, cancellationToken).ConfigureAwait(false);
        if (tipo.IsFailure)
        {
            return tipo;
        }

        Result exigido = ConferenciaDoTipoDoAtoAvulso.ExigeAtoCorrigido(tipo.Value!, command.AtoCorrigidoId);
        return exigido.IsFailure ? Result<TipoAtoPublicadoView>.Failure(exigido.Error!) : tipo;
    }

    private async Task<Result<TipoAtoPublicadoView>> ConferirCorrecaoAsync(
        PublicarAtoAvulsoDoProcessoCommand command, TipoAtoPublicadoView tipo, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<Guid>> corrigido = await correcoes.ConferirAsync(command.ProcessoSeletivoId, command.AtoCorrigidoId, cancellationToken).ConfigureAwait(false);
        if (corrigido.IsFailure)
        {
            return Result<TipoAtoPublicadoView>.Failure(corrigido.Error!);
        }

        Result item = await correcoes.NaoEhItemDoEditalAsync(corrigido.Value!, cancellationToken).ConfigureAwait(false);
        return item.IsFailure ? Result<TipoAtoPublicadoView>.Failure(item.Error!) : Result<TipoAtoPublicadoView>.Success(tipo);
    }
}
