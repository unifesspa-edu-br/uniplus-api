namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Entrega a leitura do anexo de um pedido que a pessoa vê: só o anexo verificado, ou seja, o objeto promovido, tem URL
/// de leitura, de validade curta, e a leitura fica registrada com quem a pediu antes de a URL sair.
/// </summary>
public sealed class EmissaoDaLeituraDoAnexo
{
    private const string NomeDoArquivo = "anexo-do-pedido.pdf";

    private readonly IPedidoDeConcessaoRepository _pedidos;
    private readonly IArmazenamentoDosDocumentosDaAutorizacao _armazenamento;
    private readonly IAutorizacaoUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public EmissaoDaLeituraDoAnexo(
        IPedidoDeConcessaoRepository pedidos,
        IArmazenamentoDosDocumentosDaAutorizacao armazenamento,
        IAutorizacaoUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        _pedidos = pedidos ?? throw new ArgumentNullException(nameof(pedidos));
        _armazenamento = armazenamento ?? throw new ArgumentNullException(nameof(armazenamento));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// A URL de leitura do anexo, ou a recusa: 404 para o anexo que o pedido não tem, e a falha própria para o anexo ainda
    /// não verificado. Quem chama já conferiu que a pessoa vê o pedido.
    /// </summary>
    internal async Task<Result<LeituraDoAnexoDto>> EmitirAsync(
        PedidoDeConcessao pedido, Guid anexoId, Identidade leitor, CancellationToken cancellationToken)
    {
        AnexoDoPedido? anexo = pedido.Anexos.FirstOrDefault(a => a.Id == anexoId);
        if (anexo is not { Situacao: SituacaoDoAnexo.Verificado })
        {
            return Result<LeituraDoAnexoDto>.Failure(anexo is null ? PedidoHandlerErros.NaoEncontrado() : PedidoHandlerErros.AnexoNaoVerificado());
        }

        DateTimeOffset agora = _clock.GetUtcNow();
        string url = await _armazenamento
            .GerarUrlDeLeituraAsync(anexo.ChaveDefinitiva, NomeDoArquivo, LerAnexoDoPedidoCommandHandler.PrazoDaLeitura, cancellationToken)
            .ConfigureAwait(false);
        await RegistrarLeituraAsync(LeituraDoAnexoDoPedido.Registrar(pedido.Id, anexo.Id, leitor, agora), cancellationToken).ConfigureAwait(false);

        return Result<LeituraDoAnexoDto>.Success(
            new LeituraDoAnexoDto(new Uri(url, UriKind.Absolute), agora + LerAnexoDoPedidoCommandHandler.PrazoDaLeitura));
    }

    // O acesso se registra antes de a URL sair: a URL só vai a quem ficou na trilha.
    private async Task RegistrarLeituraAsync(LeituraDoAnexoDoPedido leitura, CancellationToken cancellationToken)
    {
        await _pedidos.AdicionarLeituraAsync(leitura, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
    }
}
