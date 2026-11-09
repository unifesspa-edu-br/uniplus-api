namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// Emite a URL de PUT assinada do documento de uma versão (ADR-0146). A URL sai antes de gravar: se o armazenamento
/// falhar, nada é gravado. A validade é a do envio do anexo do pedido, e a das chaves de idempotência dos endpoints
/// que a devolvem acompanha essa validade.
/// </summary>
public sealed class EnvioDoDocumento
{
    private readonly IArmazenamentoDosDocumentosDaAutorizacao _armazenamento;
    private readonly TimeProvider _timeProvider;

    public EnvioDoDocumento(IArmazenamentoDosDocumentosDaAutorizacao armazenamento, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(armazenamento);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _armazenamento = armazenamento;
        _timeProvider = timeProvider;
    }

    public async Task<EnvioDoDocumentoDto> EmitirAsync(DocumentoDaVersao documento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documento);

        string url = await _armazenamento
            .GerarUrlDeEnvioAsync(
                documento.ChaveDeEnvio,
                CriarAnexoDoPedidoCommandHandler.TipoDeMidiaDoPdf,
                CriarAnexoDoPedidoCommandHandler.PrazoDoEnvio,
                cancellationToken)
            .ConfigureAwait(false);

        return Descrever(documento, new Uri(url, UriKind.Absolute));
    }

    private EnvioDoDocumentoDto Descrever(DocumentoDaVersao documento, Uri url) =>
        new(
            documento.PerfilId,
            documento.VersaoId,
            documento.Id,
            documento.Situacao.ToCodigo(),
            url,
            CriarAnexoDoPedidoCommandHandler.TipoDeMidiaDoPdf,
            _timeProvider.GetUtcNow() + CriarAnexoDoPedidoCommandHandler.PrazoDoEnvio);
}
