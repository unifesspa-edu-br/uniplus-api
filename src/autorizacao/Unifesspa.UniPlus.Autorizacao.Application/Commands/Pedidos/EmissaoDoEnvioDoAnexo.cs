namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Emite o envio do anexo de um pedido: cria o anexo e entrega a URL de PUT em que o solicitante sobe o PDF. A URL sai antes
/// de gravar: se o armazenamento falhar, nada é gravado. O pedido de outra pessoa não se distingue do inexistente.
/// </summary>
public sealed class EmissaoDoEnvioDoAnexo
{
    private readonly IPedidoDeConcessaoRepository _pedidos;
    private readonly EnvioDoPdf _envio;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public EmissaoDoEnvioDoAnexo(IPedidoDeConcessaoRepository pedidos, EnvioDoPdf envio, IUnitOfWork unitOfWork, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(pedidos);
        ArgumentNullException.ThrowIfNull(envio);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        _pedidos = pedidos;
        _envio = envio;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    internal async Task<Result<AnexoParaEnvioDto>> EmitirAsync(
        CriarAnexoDoPedidoCommand command, Identidade solicitante, CancellationToken cancellationToken)
    {
        PedidoDeConcessao? pedido = await PedidoDoSolicitante
            .ObterParaAlterarAsync(_pedidos, command.PedidoId, solicitante, cancellationToken)
            .ConfigureAwait(false);

        return pedido is null
            ? Result<AnexoParaEnvioDto>.Failure(PedidoHandlerErros.NaoEncontrado())
            : await EmitirDoPedidoAsync(pedido, command.TamanhoDoPdf, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AnexoParaEnvioDto>> EmitirDoPedidoAsync(
        PedidoDeConcessao pedido, long tamanhoDoPdf, CancellationToken cancellationToken)
    {
        Result<AnexoDoPedido> criado = pedido.NovoAnexo(tamanhoDoPdf);
        if (criado.IsFailure)
        {
            _unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<AnexoParaEnvioDto>.ValidationFailure(criado.Errors);
        }

        UrlDeEnvioDoPdf url = await _envio
            .EmitirAsync(criado.Value!.ChaveDeEnvio, _clock.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        return await GravarAsync(pedido, criado.Value!, url, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AnexoParaEnvioDto>> GravarAsync(
        PedidoDeConcessao pedido, AnexoDoPedido anexo, UrlDeEnvioDoPdf url, CancellationToken cancellationToken)
    {
        // Marca a linha do pedido: criar o anexo e decidir o pedido não correm juntos.
        _pedidos.Atualizar(pedido);
        Result gravado = await GravacaoDoPedido.GravarAsync(_unitOfWork, cancellationToken).ConfigureAwait(false);

        return gravado.IsFailure
            ? Result<AnexoParaEnvioDto>.Failure(gravado.Error!)
            : Result<AnexoParaEnvioDto>.Success(new AnexoParaEnvioDto(
                anexo.Id, CodigoComRotulo<SituacaoDoAnexo>.De(SituacaoDoAnexo.AguardandoEnvio), url.Endereco, EnvioDoPdf.TipoDeMidia, url.ExpiraEm));
    }
}
