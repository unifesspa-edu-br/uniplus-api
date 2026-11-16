namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Documento instituidor das versões do perfil e o que o veredicto da verificação grava nele.</summary>
public static class DocumentosDoPerfil
{
    /// <summary>Novo documento para a versão pendente cujo documento atual foi recusado pela verificação.</summary>
    public static Result<DocumentoDaVersao> Novo(Perfil perfil, Guid versaoId, long tamanhoDeclarado)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        Result<VersaoDoPerfil> versao = VersaoPendenteComPerfilAtivo(perfil, versaoId);
        if (versao.IsFailure)
        {
            return Result<DocumentoDaVersao>.Failure(versao.Error!);
        }

        return versao.Value!.Documento is { Situacao: SituacaoDoAnexo.Recusado }
            ? versao.Value.NovoDocumento(tamanhoDeclarado)
            : Result<DocumentoDaVersao>.Failure(PerfilErros.DocumentoNaoRecusado());
    }

    /// <summary>O documento que aguarda o envio e cuja URL assinada se emite de novo.</summary>
    public static Result<DocumentoDaVersao> ParaReemitirEnvio(Perfil perfil, Guid versaoId)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        Result<VersaoDoPerfil> versao = VersaoPendenteComPerfilAtivo(perfil, versaoId);
        if (versao.IsFailure)
        {
            return Result<DocumentoDaVersao>.Failure(versao.Error!);
        }

        return versao.Value!.Documento is { Situacao: SituacaoDoAnexo.AguardandoEnvio } documento
            ? Result<DocumentoDaVersao>.Success(documento)
            : Result<DocumentoDaVersao>.Failure(PerfilErros.DocumentoNaoAguardaEnvio());
    }

    /// <summary>Põe o documento da versão pendente em verificação, com o ETag que o PUT devolveu.</summary>
    public static Result<DocumentoDaVersao> Confirmar(Perfil perfil, Guid versaoId, string? etag, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        Result<VersaoDoPerfil> versao = perfil.ObterVersao(versaoId);
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
    public static Result<DocumentoDaVersao> ParaLeitura(Perfil perfil, Guid versaoId)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        Result<VersaoDoPerfil> versao = perfil.ObterVersao(versaoId);
        if (versao.IsFailure)
        {
            return Result<DocumentoDaVersao>.Failure(versao.Error!);
        }

        return De(perfil, versao.Value!) is { Situacao: SituacaoDoAnexo.Verificado } documento
            ? Result<DocumentoDaVersao>.Success(documento)
            : Result<DocumentoDaVersao>.Failure(PerfilErros.DocumentoNaoVerificado());
    }

    /// <summary>
    /// O documento que institui a versão: o dela, na proposta, ou o da versão proposta a que a remoção aponta, de modo
    /// que a remoção em sequência continua lendo o mesmo documento.
    /// </summary>
    public static DocumentoDaVersao? De(Perfil perfil, VersaoDoPerfil versao)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(versao);

        return versao.VersaoDoDocumento is { } origem ? perfil.Versoes.FirstOrDefault(v => v.Id == origem)?.Documento : versao.Documento;
    }

    /// <summary>
    /// Registra a recusa, ou o pedido de reenvio, do verificador. Só vale com o documento em verificação; o resultado
    /// repetido ou fora de hora não muda nada e devolve falso.
    /// </summary>
    public static bool RegistrarRecusado(Perfil perfil, Guid documentoId, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        return DocumentoPorId(perfil, documentoId)?.RegistrarRecusado(instante) ?? false;
    }

    /// <summary>
    /// Registra o veredicto "verificado" no documento, em qualquer situação da versão: a verificação só grava o hash, e a
    /// entrada em vigor é da aprovação. O resultado repetido ou fora de hora não muda nada e devolve falso.
    /// </summary>
    public static bool RegistrarVerificado(Perfil perfil, Guid documentoId, string sha256, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        return DocumentoPorId(perfil, documentoId)?.RegistrarVerificado(sha256, instante) ?? false;
    }

    private static DomainError RecusaDaConfirmacao(VersaoDoPerfil versao) =>
        versao.Pendente ? PerfilErros.DocumentoNaoAguardaEnvio() : versao.RecusaPorNaoPendente();

    private static DocumentoDaVersao? DocumentoPorId(Perfil perfil, Guid documentoId) =>
        perfil.Versoes.SelectMany(static v => v.Documentos).FirstOrDefault(d => d.Id == documentoId);

    /// <summary>
    /// A versão expirada responde pelo prazo mesmo em perfil desativado; nas demais, o perfil desativado recusa antes
    /// da situação da versão.
    /// </summary>
    private static Result<VersaoDoPerfil> VersaoPendenteComPerfilAtivo(Perfil perfil, Guid versaoId)
    {
        Result<VersaoDoPerfil> versao = perfil.ObterVersao(versaoId);
        if (versao.IsFailure)
        {
            return versao;
        }

        VersaoDoPerfil alvo = versao.Value!;
        DomainError? recusa = alvo.Expirada ? PerfilErros.VersaoExpirada()
            : !perfil.Ativo ? PerfilErros.Inativo()
            : alvo.Pendente ? null
            : alvo.RecusaPorNaoPendente();

        return recusa is null ? versao : Result<VersaoDoPerfil>.Failure(recusa);
    }
}
