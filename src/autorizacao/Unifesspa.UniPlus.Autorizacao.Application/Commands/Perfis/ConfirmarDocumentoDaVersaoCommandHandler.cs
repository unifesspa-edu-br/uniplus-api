namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// Grava o documento como em verificação e devolve o pedido como mensagem de saída, que o Wolverine instala no outbox
/// na mesma transação: não há documento em verificação sem pedido, nem pedido sem documento (ADR-0146).
/// </summary>
public static class ConfirmarDocumentoDaVersaoCommandHandler
{
    public static async Task<(Result<DocumentoDaVersaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        ConfirmarDocumentoDaVersaoCommand command, DocumentosDasVersoes documentos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(documentos);

        Result<DocumentoDaVersao> confirmado = await documentos
            .ConfirmarAsync(command.PerfilId, command.VersaoId, command.ETag, cancellationToken)
            .ConfigureAwait(false);

        return confirmado.IsFailure
            ? (Result<DocumentoDaVersaoDto>.ValidationFailure(confirmado.Errors), [])
            : (Result<DocumentoDaVersaoDto>.Success(VersaoMapping.ToDto(confirmado.Value!)), [PedidoAoVerificador(confirmado.Value!)]);
    }

    private static PedidoDeVerificacaoDeArquivo PedidoAoVerificador(DocumentoDaVersao documento) => new(
        documento.Id,
        documento.ChaveDeEnvio,
        documento.ETag!,
        documento.ChaveDefinitiva,
        FormatoDoArquivo.Pdf,
        documento.TamanhoDeclarado,
        DestinoDoResultado.Autorizacao);
}
