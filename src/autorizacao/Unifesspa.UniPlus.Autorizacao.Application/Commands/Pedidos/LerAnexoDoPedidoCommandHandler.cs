namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da leitura do anexo: confere que a pessoa vê o pedido, com a leitura do pedido registrada, e entrega a URL do
/// anexo verificado, com a leitura do anexo registrada com quem a pediu (<see cref="EmissaoDaLeituraDoAnexo"/>).
/// </summary>
public static class LerAnexoDoPedidoCommandHandler
{
    /// <summary>Validade da URL de leitura; o navegador a segue logo depois de recebê-la.</summary>
    public static readonly TimeSpan PrazoDaLeitura = TimeSpan.FromMinutes(2);

    public static async Task<Result<LeituraDoAnexoDto>> Handle(
        LerAnexoDoPedidoCommand command,
        IPedidoDeConcessaoRepository repository,
        IVerificadorDeAcesso verificador,
        EmissaoDaLeituraDoAnexo emissao,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(emissao);
        ArgumentNullException.ThrowIfNull(userContext);

        if (IdentidadeDoToken.PessoaDe(userContext) is not { } pessoa)
        {
            return Result<LeituraDoAnexoDto>.Failure(PedidoHandlerErros.IdentidadeIncompleta());
        }

        Result<PedidoDeConcessao> pedido = await VisibilidadeDoPedido.ObterAsync(command.PedidoId, pessoa, repository, verificador, cancellationToken).ConfigureAwait(false);

        return pedido.IsFailure
            ? Result<LeituraDoAnexoDto>.Failure(pedido.Error!)
            : await emissao.EmitirAsync(pedido.Value!, command.AnexoId, pessoa.Identidade, cancellationToken).ConfigureAwait(false);
    }
}
