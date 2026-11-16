namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// Grava o PDF como em verificação e devolve o pedido como mensagem de saída, que o Wolverine instala no outbox na
/// mesma transação: não há documento em verificação sem pedido, nem pedido sem documento (ADR-0146).
/// </summary>
public static class ConfirmarDocumentoDaInstalacaoCommandHandler
{
    public static async Task<(Result<InstalacaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        ConfirmarDocumentoDaInstalacaoCommand command,
        ConfirmacaoDoDocumentoDeDesignacao confirmacao,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(confirmacao);
        if (IdentidadeDoToken.De(userContext) is not { } executor)
        {
            return Recusa(InstalacaoHandlerErros.AutorNaoIdentificado());
        }

        Result<Instalacao> confirmada = await confirmacao.ConfirmarAsync(command.Id, executor, command.ETag, cancellationToken).ConfigureAwait(false);

        return confirmada.IsFailure
            ? (Result<InstalacaoDto>.ValidationFailure(confirmada.Errors), [])
            : (Result<InstalacaoDto>.Success(InstalacaoMapping.ToDto(confirmada.Value!)), [PedidoAoVerificador(confirmada.Value!)]);
    }

    private static PedidoDeVerificacaoDeArquivo PedidoAoVerificador(Instalacao instalacao) => new(
        instalacao.Id,
        instalacao.ChaveDeEnvio,
        instalacao.DocumentoETag!,
        instalacao.ChaveDefinitiva,
        FormatoDoArquivo.Pdf,
        instalacao.TamanhoDoPdf,
        DestinoDoResultado.Autorizacao);

    private static (Result<InstalacaoDto> Resposta, IEnumerable<object> Mensagens) Recusa(DomainError erro) =>
        (Result<InstalacaoDto>.Failure(erro), []);
}
