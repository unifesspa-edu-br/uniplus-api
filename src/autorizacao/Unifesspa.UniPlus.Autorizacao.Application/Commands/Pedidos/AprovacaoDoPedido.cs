namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A aprovação da etapa que o pedido aguarda. A concessão nasce na última aprovação exigida, na mesma transação, e os
/// avisos da situação em que o pedido ficou saem como mensagens de saída, no outbox da mesma transação.
/// </summary>
/// <remarks>
/// O pedido do perfil de Representante do controlador toma, antes de qualquer outra trava, a trava única do papel: a
/// leitura de que não há outro Representante vigente, que dispensa a segunda etapa na designação do primeiro, e a
/// gravação da concessão acontecem sob ela, e duas designações simultâneas não aplicam ambas a dispensa (ADR-0162).
/// </remarks>
public sealed class AprovacaoDoPedido
{
    private readonly IPedidoDeConcessaoRepository _pedidos;
    private readonly IConcessaoRepository _concessoes;
    private readonly CatalogoDaAplicacao _catalogo;
    private readonly IPapelDoRepresentante _papel;
    private readonly GravacaoDaDecisaoDoPedido _gravacao;
    private readonly TimeProvider _clock;

    public AprovacaoDoPedido(
        IPedidoDeConcessaoRepository pedidos,
        IConcessaoRepository concessoes,
        CatalogoDaAplicacao catalogo,
        IPapelDoRepresentante papel,
        GravacaoDaDecisaoDoPedido gravacao,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(pedidos);
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(papel);
        ArgumentNullException.ThrowIfNull(gravacao);
        ArgumentNullException.ThrowIfNull(clock);

        _pedidos = pedidos;
        _concessoes = concessoes;
        _catalogo = catalogo;
        _papel = papel;
        _gravacao = gravacao;
        _clock = clock;
    }

    public async Task<(Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens)> AprovarAsync(
        AprovarPedidoDeConcessaoCommand command, IVerificadorDeAcesso verificador, Identidade? decisor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await TravarPapelSeForDoRepresentanteAsync(command.Id, cancellationToken).ConfigureAwait(false);
        Result<DecisaoDaEtapa> preparada = await DecisaoDaEtapa
            .PrepararAsync(command.Id, _pedidos, _concessoes, verificador, _catalogo, decisor, cancellationToken)
            .ConfigureAwait(false);

        return preparada.IsFailure
            ? GravacaoDaDecisaoDoPedido.Recusa(preparada.Error!)
            : await AprovarNaEtapaAsync(preparada.Value!, command.ValidaAte, cancellationToken).ConfigureAwait(false);
    }

    // O perfil do pedido não muda depois da criação: a leitura sem trava basta para decidir se a trava do papel vale.
    private async Task TravarPapelSeForDoRepresentanteAsync(Guid pedidoId, CancellationToken cancellationToken)
    {
        if (await _pedidos.ObterPerfilIdAsync(pedidoId, cancellationToken).ConfigureAwait(false) == PerfisDeSistema.RepresentanteDoControladorId)
        {
            await _papel.TravarAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<(Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens)> AprovarNaEtapaAsync(
        DecisaoDaEtapa decisao, DateTimeOffset? validaAte, CancellationToken cancellationToken)
    {
        DateTimeOffset agora = _clock.GetUtcNow();
        SituacaoDoPedido situacaoAnterior = decisao.Pedido.Situacao;
        ContextoDaAprovacao contexto = await ContextoAsync(decisao, agora, cancellationToken).ConfigureAwait(false);

        Result<DesfechoDaAprovacao> aprovacao = decisao.Pedido.Aprovar(decisao.Decisor, decisao.Avaliado, contexto, agora, validaAte);
        if (aprovacao.IsFailure)
        {
            return await _gravacao.RecusarAprovacaoAsync(decisao, aprovacao.Errors, cancellationToken).ConfigureAwait(false);
        }

        return await _gravacao
            .GravarAsync(decisao, situacaoAnterior, aprovacao.Value == DesfechoDaAprovacao.Concluido, agora, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ContextoDaAprovacao> ContextoAsync(DecisaoDaEtapa decisao, DateTimeOffset agora, CancellationToken cancellationToken) =>
        new(decisao.Avaliado.Governanca == TipoDeGovernanca.RepresentanteDoControlador
            && await _papel.HaOutroVigenteAsync(decisao.Pedido.Beneficiario, agora, cancellationToken).ConfigureAwait(false));
}
