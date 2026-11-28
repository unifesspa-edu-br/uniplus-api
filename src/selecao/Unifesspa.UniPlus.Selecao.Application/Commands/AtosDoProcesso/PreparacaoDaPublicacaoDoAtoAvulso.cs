namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Prepara o ato avulso sem escrever e junta todas as recusas que não dependem umas das outras: a forma do pedido, o
/// tipo, os documentos e o ato corrigido. Nenhuma consulta lê um valor já recusado, e nenhuma corre quando o processo ou
/// o instante foi recusado.
/// </summary>
public sealed class PreparacaoDaPublicacaoDoAtoAvulso(
    ConferenciaDoPedidoDoAto pedidos,
    ConferenciaDoTipoDoAtoAvulso tipos,
    ConferenciaDoAtoCorrigido correcoes)
{
    internal async Task<Result<AtoAPublicar>> PrepararAsync(PublicarAtoAvulsoDoProcessoCommand command, CancellationToken cancellationToken)
    {
        IReadOnlyList<FieldError> formaDoTipo = ConferenciaDoTipoDoAtoAvulso.RecusaDaForma(command.TipoAtoCodigo);
        PedidoDoAtoConferido pedido = await pedidos.ConferirAsync(command, formaDoTipo, cancellationToken).ConfigureAwait(false);
        Result<TipoAtoPublicadoView>? tipo = await ConferirTipoAsync(command, pedido, formaDoTipo.Count == 0, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<FieldError> doAtoCorrigido = await ConferirCorrecaoAsync(command, pedido, tipo, cancellationToken).ConfigureAwait(false);

        return pedido.Reunir(tipo?.Errors ?? [], doAtoCorrigido, () => pedido.Ato(tipo!.Value!));
    }

    /// <returns>O tipo conferido, ou nulo quando ele não pôde ser consultado: ausente no pedido ou sem processo e instante.</returns>
    private async Task<Result<TipoAtoPublicadoView>?> ConferirTipoAsync(
        PublicarAtoAvulsoDoProcessoCommand command, PedidoDoAtoConferido pedido, bool tipoInformado, CancellationToken cancellationToken)
    {
        if (!tipoInformado || pedido.Estado is not { } estado)
        {
            return null;
        }

        return await tipos.ConferirAsync(command.TipoAtoCodigo!, estado.Instante.Dia, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<FieldError>> ConferirCorrecaoAsync(
        PublicarAtoAvulsoDoProcessoCommand command, PedidoDoAtoConferido pedido, Result<TipoAtoPublicadoView>? tipo, CancellationToken cancellationToken)
    {
        if (pedido.Estado is null)
        {
            return [];
        }

        return pedido.Pedido.Correcao switch
        {
            { Situacao: SituacaoDaCorrecaoPedida.CorrigeUmAto, AtoCorrigidoId: { } corrigido } =>
                await correcoes.ConferirParaDocumentoAvulsoAsync(command.ProcessoSeletivoId, corrigido, cancellationToken).ConfigureAwait(false),
            { Situacao: SituacaoDaCorrecaoPedida.SemCorrecao } when tipo is { IsSuccess: true } =>
                ConferenciaDoTipoDoAtoAvulso.ExigeAtoCorrigido(tipo.Value!).Errors,
            _ => [],
        };
    }
}
