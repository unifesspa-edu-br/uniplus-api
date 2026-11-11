namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O documento instituidor das versões: outro documento depois da recusa, a URL de envio de novo, a confirmação do
/// envio e a leitura. Tudo que muda estado roda na ordem de travas de <see cref="OperacaoSobreOPerfil"/>.
/// </summary>
public sealed class DocumentosDasVersoes
{
    private readonly OperacaoSobreOPerfil _operacao;
    private readonly EnvioDoDocumento _envio;
    private readonly LeitorDoDocumentoDaVersao _leitor;

    public DocumentosDasVersoes(OperacaoSobreOPerfil operacao, EnvioDoDocumento envio, LeitorDoDocumentoDaVersao leitor)
    {
        ArgumentNullException.ThrowIfNull(operacao);
        ArgumentNullException.ThrowIfNull(envio);
        ArgumentNullException.ThrowIfNull(leitor);

        _operacao = operacao;
        _envio = envio;
        _leitor = leitor;
    }

    /// <summary>Outro documento para a versão pendente cujo documento foi recusado pela verificação.</summary>
    public Task<Result<EnvioDoDocumentoDto>> NovoAsync(
        Guid perfilId, Guid versaoId, long tamanhoDoDocumento, CancellationToken cancellationToken) =>
        _operacao.ExecutarAsync(
            perfilId,
            (perfil, _) => EmitirAsync(perfil.NovoDocumento(versaoId, tamanhoDoDocumento), cancellationToken),
            cancellationToken);

    /// <summary>URL de envio nova para o documento que ainda aguarda o PDF: a mesma chave no armazenamento.</summary>
    public Task<Result<EnvioDoDocumentoDto>> ReemitirEnvioAsync(Guid perfilId, Guid versaoId, CancellationToken cancellationToken) =>
        _operacao.ExecutarAsync(
            perfilId,
            (perfil, _) => EmitirAsync(perfil.DocumentoParaReemitirEnvio(versaoId), cancellationToken),
            cancellationToken);

    /// <summary>Põe o documento em verificação. Devolve o documento para quem publica o pedido ao verificador.</summary>
    public Task<Result<DocumentoDaVersao>> ConfirmarAsync(
        Guid perfilId, Guid versaoId, string? etag, CancellationToken cancellationToken) =>
        _operacao.ExecutarSemChaveAsync(
            perfilId,
            (perfil, contexto) => Task.FromResult(perfil.ConfirmarDocumento(versaoId, etag, contexto.Agora)),
            cancellationToken);

    /// <summary>Só o documento verificado tem URL de leitura; a leitura fica registrada com quem a pediu.</summary>
    public Task<Result<LeituraDoDocumentoDaVersaoDto>> LerAsync(
        Guid perfilId, Guid versaoId, IVerificadorDeAcesso verificador, CancellationToken cancellationToken) =>
        _leitor.LerAsync(perfilId, versaoId, verificador, cancellationToken);

    private async Task<Result<EnvioDoDocumentoDto>> EmitirAsync(Result<DocumentoDaVersao> documento, CancellationToken cancellationToken) =>
        documento.IsFailure
            ? Result<EnvioDoDocumentoDto>.ValidationFailure(documento.Errors)
            : Result<EnvioDoDocumentoDto>.Success(
                await _envio.EmitirAsync(documento.Value!, cancellationToken).ConfigureAwait(false));
}
