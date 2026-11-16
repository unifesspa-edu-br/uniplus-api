namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using DTOs;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Salva o rascunho (issue #1859) sobre a versão do formulário em que está o rascunho (a divulgada) — nunca a vigente
/// por relógio. A validação é estrutural: só recusa por campo a resposta que viola uma restrição do
/// formulário; nunca por obrigatoriedade, que é exigida somente na submissão.
/// </summary>
public static class SalvarInscricaoCommandHandler
{
    public static async Task<Result<InscricaoDto>> Handle(
        SalvarInscricaoCommand command,
        RascunhoDoCandidato rascunho,
        SalvamentoDoRascunho salvamento,
        RespostaDoRascunho resposta,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(rascunho);
        ArgumentNullException.ThrowIfNull(salvamento);
        ArgumentNullException.ThrowIfNull(resposta);

        Result<PedidoDoRascunho> pedido = await rascunho.PedidoAsync(command.ProcessoSeletivoId, command.Request, userContext, cancellationToken).ConfigureAwait(false);
        return pedido.IsFailure
            ? Result<InscricaoDto>.ValidationFailure(pedido.Errors)
            : await SalvarEResponderAsync(command, pedido.Value!, salvamento, resposta, userContext, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<InscricaoDto>> SalvarEResponderAsync(
        SalvarInscricaoCommand command, PedidoDoRascunho pedido, SalvamentoDoRascunho salvamento, RespostaDoRascunho resposta, IUserContext userContext, CancellationToken cancellationToken)
    {
        Result salvo = await salvamento.SalvarAsync(pedido, command.Request, command.Precondicao, userContext, cancellationToken).ConfigureAwait(false);
        return salvo.IsFailure
            ? Result<InscricaoDto>.ValidationFailure(salvo.Errors)
            : await resposta.ResponderAsync(pedido, cancellationToken).ConfigureAwait(false);
    }
}
