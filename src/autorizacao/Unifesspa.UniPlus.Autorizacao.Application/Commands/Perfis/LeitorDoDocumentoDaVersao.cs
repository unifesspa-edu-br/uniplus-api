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
/// A leitura do documento instituidor da versão: só o documento verificado tem URL de leitura, e só para quem mantém
/// perfis ou aprova pela proteção de dados. Cada acesso fica registrado com quem o pediu.
/// </summary>
public sealed class LeitorDoDocumentoDaVersao
{
    private const string NomeDoArquivo = "documento-da-versao.pdf";

    private readonly AutoriaDaOperacao _autoria;
    private readonly IPerfilRepository _perfis;
    private readonly IArmazenamentoDosDocumentosDaAutorizacao _armazenamento;
    private readonly IAutorizacaoUnitOfWork _unitOfWork;

    public LeitorDoDocumentoDaVersao(
        AutoriaDaOperacao autoria, IPerfilRepository perfis, IArmazenamentoDosDocumentosDaAutorizacao armazenamento, IAutorizacaoUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(autoria);
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(armazenamento);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _autoria = autoria;
        _perfis = perfis;
        _armazenamento = armazenamento;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<LeituraDoDocumentoDaVersaoDto>> LerAsync(
        Guid perfilId, Guid versaoId, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        DomainError? negado = await NegadoAsync(verificador, perfilId, cancellationToken).ConfigureAwait(false);

        return negado is null
            ? await LerSeVerificadoAsync(perfilId, versaoId, cancellationToken).ConfigureAwait(false)
            : Result<LeituraDoDocumentoDaVersaoDto>.Failure(negado);
    }

    private async Task<Result<LeituraDoDocumentoDaVersaoDto>> LerSeVerificadoAsync(Guid perfilId, Guid versaoId, CancellationToken cancellationToken)
    {
        Result<DocumentoDaVersao> documento = await DocumentoParaLeituraAsync(perfilId, versaoId, cancellationToken).ConfigureAwait(false);

        return documento.IsFailure
            ? Result<LeituraDoDocumentoDaVersaoDto>.ValidationFailure(documento.Errors)
            : await LerVerificadoAsync(documento.Value!, _autoria.Autor!, cancellationToken).ConfigureAwait(false);
    }

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

        AcessoAvaliado acesso = await AcessoDaVersao.AvaliarLeituraDoDocumentoAsync(verificador, perfilId, cancellationToken).ConfigureAwait(false);

        return acesso.Recusa(PerfilErros.SemPermissaoNaEtapa);
    }

    private async Task<Result<DocumentoDaVersao>> DocumentoParaLeituraAsync(Guid perfilId, Guid versaoId, CancellationToken cancellationToken)
    {
        Perfil? perfil = await _perfis.ObterPorIdAsync(perfilId, cancellationToken).ConfigureAwait(false);

        return perfil is null
            ? Result<DocumentoDaVersao>.Failure(PerfilHandlerErros.NaoEncontrado(perfilId))
            : perfil.DocumentoParaLeitura(versaoId);
    }
}
