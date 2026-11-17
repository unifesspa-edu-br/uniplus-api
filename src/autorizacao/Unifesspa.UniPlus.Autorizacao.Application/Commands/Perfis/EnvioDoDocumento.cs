namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// Emite a URL de PUT assinada do documento de uma versão (ADR-0146). A URL sai antes de gravar: se o armazenamento
/// falhar, nada é gravado. A validade é a do envio do PDF, comum à Autorização.
/// </summary>
public sealed class EnvioDoDocumento
{
    private readonly EnvioDoPdf _envio;
    private readonly TimeProvider _timeProvider;

    public EnvioDoDocumento(EnvioDoPdf envio, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(envio);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _envio = envio;
        _timeProvider = timeProvider;
    }

    public async Task<EnvioDoDocumentoDto> EmitirAsync(DocumentoDaVersao documento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documento);

        UrlDeEnvioDoPdf envio = await _envio
            .EmitirAsync(documento.ChaveDeEnvio, _timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        return Descrever(documento, envio);
    }

    private static EnvioDoDocumentoDto Descrever(DocumentoDaVersao documento, UrlDeEnvioDoPdf envio) =>
        new(
            documento.PerfilId,
            documento.VersaoId,
            documento.Id,
            documento.Situacao.ToCodigo(),
            envio.Endereco,
            EnvioDoPdf.TipoDeMidia,
            envio.ExpiraEm);
}
