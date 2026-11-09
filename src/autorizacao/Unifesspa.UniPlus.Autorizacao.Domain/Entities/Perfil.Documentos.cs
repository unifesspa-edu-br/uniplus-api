namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Documento instituidor das versões do perfil e o que o veredicto da verificação grava nele.</summary>
public sealed partial class Perfil
{
    /// <summary>Novo documento para a versão pendente cujo documento atual foi recusado pela verificação.</summary>
    public Result<DocumentoDaVersao> NovoDocumento(Guid versaoId, long tamanhoDeclarado)
    {
        Result<VersaoDoPerfil> versao = VersaoPendenteComPerfilAtivo(versaoId);
        if (versao.IsFailure)
        {
            return Result<DocumentoDaVersao>.Failure(versao.Error!);
        }

        return versao.Value!.Documento is { Situacao: SituacaoDoAnexo.Recusado }
            ? versao.Value.NovoDocumento(tamanhoDeclarado)
            : Result<DocumentoDaVersao>.Failure(PerfilErros.DocumentoNaoRecusado());
    }

    /// <summary>O documento que aguarda o envio e cuja URL assinada se emite de novo.</summary>
    public Result<DocumentoDaVersao> DocumentoParaReemitirEnvio(Guid versaoId)
    {
        Result<VersaoDoPerfil> versao = VersaoPendenteComPerfilAtivo(versaoId);
        if (versao.IsFailure)
        {
            return Result<DocumentoDaVersao>.Failure(versao.Error!);
        }

        return versao.Value!.Documento is { Situacao: SituacaoDoAnexo.AguardandoEnvio } documento
            ? Result<DocumentoDaVersao>.Success(documento)
            : Result<DocumentoDaVersao>.Failure(PerfilErros.DocumentoNaoAguardaEnvio());
    }

    /// <summary>Põe o documento da versão pendente em verificação, com o ETag que o PUT devolveu.</summary>
    public Result<DocumentoDaVersao> ConfirmarDocumento(Guid versaoId, string? etag, DateTimeOffset instante)
    {
        Result<VersaoDoPerfil> versao = ObterVersao(versaoId);
        if (versao.IsFailure || !versao.Value!.Pendente || versao.Value.Documento is not { } documento)
        {
            return Result<DocumentoDaVersao>.Failure(versao.IsFailure ? versao.Error! : RecusaDaConfirmacao(versao.Value!));
        }

        Result confirmado = documento.ConfirmarEnvio(etag, instante);

        return confirmado.IsSuccess
            ? Result<DocumentoDaVersao>.Success(documento)
            : Result<DocumentoDaVersao>.ValidationFailure(confirmado.Errors);
    }

    /// <summary>O documento verificado da versão, o único que se lê.</summary>
    public Result<DocumentoDaVersao> DocumentoParaLeitura(Guid versaoId)
    {
        Result<VersaoDoPerfil> versao = ObterVersao(versaoId);
        if (versao.IsFailure)
        {
            return Result<DocumentoDaVersao>.Failure(versao.Error!);
        }

        return DocumentoDe(versao.Value!) is { Situacao: SituacaoDoAnexo.Verificado } documento
            ? Result<DocumentoDaVersao>.Success(documento)
            : Result<DocumentoDaVersao>.Failure(PerfilErros.DocumentoNaoVerificado());
    }

    /// <summary>
    /// O documento que institui a versão: o dela, na proposta, ou o da versão proposta a que a remoção aponta, de modo
    /// que a remoção em sequência continua lendo o mesmo documento.
    /// </summary>
    public DocumentoDaVersao? DocumentoDe(VersaoDoPerfil versao)
    {
        ArgumentNullException.ThrowIfNull(versao);

        return versao.VersaoDoDocumento is { } origem ? _versoes.Find(v => v.Id == origem)?.Documento : versao.Documento;
    }

    /// <summary>
    /// Registra a recusa, ou o pedido de reenvio, do verificador. Só vale com o documento em verificação; o resultado
    /// repetido ou fora de hora não muda nada e devolve falso.
    /// </summary>
    public bool RegistrarDocumentoRecusado(Guid documentoId, DateTimeOffset instante) =>
        DocumentoPorId(documentoId)?.RegistrarRecusado(instante) ?? false;

    /// <summary>
    /// Registra o veredicto "verificado" no documento, em qualquer situação da versão: a verificação só grava o hash, e a
    /// entrada em vigor é da aprovação. O resultado repetido ou fora de hora não muda nada e devolve falso.
    /// </summary>
    public bool RegistrarDocumentoVerificado(Guid documentoId, string sha256, DateTimeOffset instante) =>
        DocumentoPorId(documentoId)?.RegistrarVerificado(sha256, instante) ?? false;

    private static DomainError RecusaDaConfirmacao(VersaoDoPerfil versao) =>
        versao.Pendente ? PerfilErros.DocumentoNaoAguardaEnvio() : versao.RecusaPorNaoPendente();

    private DocumentoDaVersao? DocumentoPorId(Guid documentoId) =>
        _versoes.SelectMany(static v => v.Documentos).FirstOrDefault(d => d.Id == documentoId);

    private Result<VersaoDoPerfil> VersaoPendenteComPerfilAtivo(Guid versaoId)
    {
        Result<VersaoDoPerfil> versao = ObterVersao(versaoId);
        if (versao.IsFailure)
        {
            return versao;
        }

        if (!Ativo)
        {
            return Result<VersaoDoPerfil>.Failure(PerfilErros.Inativo());
        }

        return versao.Value!.Pendente ? versao : Result<VersaoDoPerfil>.Failure(versao.Value.RecusaPorNaoPendente());
    }
}
