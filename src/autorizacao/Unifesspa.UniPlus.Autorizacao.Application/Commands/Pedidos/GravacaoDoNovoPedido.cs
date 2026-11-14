namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A gravação do pedido conferido e os avisos dele, que saem como mensagens de saída e entram no outbox na mesma
/// transação da gravação. O pedido ativo igual, que o índice único recusa, responde como duplicado.
/// </summary>
public sealed class GravacaoDoNovoPedido
{
    private readonly IPedidoDeConcessaoRepository _pedidos;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AvisosDoPedido _avisos;

    public GravacaoDoNovoPedido(IPedidoDeConcessaoRepository pedidos, IUnitOfWork unitOfWork, AvisosDoPedido avisos)
    {
        ArgumentNullException.ThrowIfNull(pedidos);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(avisos);

        _pedidos = pedidos;
        _unitOfWork = unitOfWork;
        _avisos = avisos;
    }

    internal async Task<(Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens)> GravarAsync(
        NovoPedido novo, CancellationToken cancellationToken)
    {
        await _pedidos.AdicionarAsync(novo.Pedido, cancellationToken).ConfigureAwait(false);
        if (!await SalvouAsync(cancellationToken).ConfigureAwait(false))
        {
            return (Result<PedidoDeConcessaoDto>.Failure(PedidoHandlerErros.PedidoDuplicado()), []);
        }

        IReadOnlyList<EnviarEmail> mensagens = await _avisos
            .AvisarAsync(novo.Pedido, novo.Perfil.Nome, situacaoAnterior: null, cancellationToken)
            .ConfigureAwait(false);

        return (Result<PedidoDeConcessaoDto>.Success(PedidoMapping.ToDto(novo.Pedido, novo.Perfil)), mensagens);
    }

    private async Task<bool> SalvouAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (PedidoViolacoes.EhPedidoDuplicado(ex))
        {
            _unitOfWork.DescartarAlteracoesNaoSalvas();
            return false;
        }
    }
}
