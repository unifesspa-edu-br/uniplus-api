namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O documento instituidor das versões: outro documento depois da recusa, a URL de envio de novo, a confirmação do
/// envio e a leitura. Tudo que muda estado roda na ordem de travas de <see cref="OperacaoSobreOPerfil"/>.
/// </summary>
public sealed class DocumentosDasVersoes
{
    private const string NomeDoArquivo = "documento-da-versao.pdf";

    private readonly OperacaoSobreOPerfil _operacao;
    private readonly EnvioDoDocumento _envio;
    private readonly AutoriaDaOperacao _autoria;
    private readonly IPerfilRepository _perfis;
    private readonly IArmazenamentoDosDocumentosDaAutorizacao _armazenamento;
    private readonly IAutorizacaoUnitOfWork _unitOfWork;

    public DocumentosDasVersoes(
        OperacaoSobreOPerfil operacao,
        EnvioDoDocumento envio,
        AutoriaDaOperacao autoria,
        IPerfilRepository perfis,
        IArmazenamentoDosDocumentosDaAutorizacao armazenamento,
        IAutorizacaoUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(operacao);
        ArgumentNullException.ThrowIfNull(envio);
        ArgumentNullException.ThrowIfNull(autoria);
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(armazenamento);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _operacao = operacao;
        _envio = envio;
        _autoria = autoria;
        _perfis = perfis;
        _armazenamento = armazenamento;
        _unitOfWork = unitOfWork;
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

    /// <summary>
    /// Só o documento verificado tem URL de leitura, e só para quem mantém perfis ou aprova pela proteção de dados. Cada
    /// acesso fica registrado com quem o pediu.
    /// </summary>
    public async Task<Result<LeituraDoDocumentoDaVersaoDto>> LerAsync(
        Guid perfilId, Guid versaoId, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        DomainError? negado = await NegadoAsync(verificador, perfilId, cancellationToken).ConfigureAwait(false);
        if (negado is not null)
        {
            return Result<LeituraDoDocumentoDaVersaoDto>.Failure(negado);
        }

        Result<DocumentoDaVersao> documento = await DocumentoParaLeituraAsync(perfilId, versaoId, cancellationToken).ConfigureAwait(false);

        return documento.IsFailure
            ? Result<LeituraDoDocumentoDaVersaoDto>.ValidationFailure(documento.Errors)
            : await LerVerificadoAsync(documento.Value!, _autoria.Autor!, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<EnvioDoDocumentoDto>> EmitirAsync(Result<DocumentoDaVersao> documento, CancellationToken cancellationToken) =>
        documento.IsFailure
            ? Result<EnvioDoDocumentoDto>.ValidationFailure(documento.Errors)
            : Result<EnvioDoDocumentoDto>.Success(
                await _envio.EmitirAsync(documento.Value!, cancellationToken).ConfigureAwait(false));

    // O acesso se registra antes de a URL sair: a URL só vai a quem ficou na trilha.
    private async Task<Result<LeituraDoDocumentoDaVersaoDto>> LerVerificadoAsync(
        DocumentoDaVersao documento, Identidade leitor, CancellationToken cancellationToken)
    {
        DateTimeOffset agora = _autoria.Agora;
        string url = await _armazenamento
            .GerarUrlDeLeituraAsync(documento.ChaveDefinitiva, NomeDoArquivo, LerAnexoDoPedidoCommandHandler.PrazoDaLeitura, cancellationToken)
            .ConfigureAwait(false);

        await _perfis.AdicionarLeituraAsync(LeituraDoDocumentoDaVersao.Registrar(documento.Id, leitor, agora), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<LeituraDoDocumentoDaVersaoDto>.Success(
            new LeituraDoDocumentoDaVersaoDto(new Uri(url, UriKind.Absolute), agora + LerAnexoDoPedidoCommandHandler.PrazoDaLeitura));
    }

    private async Task<DomainError?> NegadoAsync(IVerificadorDeAcesso verificador, Guid perfilId, CancellationToken cancellationToken)
    {
        if (_autoria.Autor is null)
        {
            return PerfilHandlerErros.AutorNaoIdentificado();
        }

        bool pode = await AcessoDaVersao.PodeLerODocumentoAsync(verificador, perfilId, cancellationToken).ConfigureAwait(false);

        return pode ? null : PerfilErros.SemPermissaoNaEtapa();
    }

    private async Task<Result<DocumentoDaVersao>> DocumentoParaLeituraAsync(Guid perfilId, Guid versaoId, CancellationToken cancellationToken)
    {
        Perfil? perfil = await _perfis.ObterPorIdAsync(perfilId, cancellationToken).ConfigureAwait(false);

        return perfil is null
            ? Result<DocumentoDaVersao>.Failure(PerfilHandlerErros.NaoEncontrado(perfilId))
            : perfil.DocumentoParaLeitura(versaoId);
    }
}
