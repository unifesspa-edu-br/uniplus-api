namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Services;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Confere o que todo pedido de publicação de ato tem em comum, nesta ordem: o instante do comando; a forma, sem ler
/// nada; o estado do processo e os documentos. O instante que não se resolve é defeito de instalação e é a única causa
/// da resposta, sem ler mais nada. O estado do processo recusado impede as consultas, mas não a forma, que já foi
/// conferida.
/// </summary>
public sealed class ConferenciaDoPedidoDoAto(
    ConferenciaDoProcessoDivulgado processos,
    LeituraDoInstanteDoComando instantes,
    ConferenciaDosDocumentosDoAto documentos)
{
    internal async Task<PedidoDoAtoConferido> ConferirAsync(
        IDadosDoAtoPublicado dados, IReadOnlyList<FieldError> formaPropria, CancellationToken cancellationToken)
    {
        Result<InstanteDoComando> instante = instantes.Ler();
        if (instante.IsFailure)
        {
            return PedidoDoAtoConferido.SemInstante(dados, instante.Error!);
        }

        PedidoDoAtoDoProcesso pedido = PedidoDoAtoDoProcesso.De(dados, formaPropria);

        Result<ProcessoDivulgadoConferido> processo = await processos.ConferirAsync(dados.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);

        return processo.IsFailure
            ? new(dados, pedido, null, processo.Errors, ResultadoDosDocumentos.NaoLido)
            : await ConferirDocumentosAsync(dados, pedido, new(processo.Value!, instante.Value), cancellationToken).ConfigureAwait(false);
    }

    private async Task<PedidoDoAtoConferido> ConferirDocumentosAsync(
        IDadosDoAtoPublicado dados, PedidoDoAtoDoProcesso pedido, EstadoDoPedidoConferido estado, CancellationToken cancellationToken)
    {
        ResultadoDosDocumentos lidos = await documentos
            .ConferirAsync(dados.ProcessoSeletivoId, pedido.Documentos, cancellationToken).ConfigureAwait(false);

        return new(dados, pedido, estado, [], lidos);
    }
}
