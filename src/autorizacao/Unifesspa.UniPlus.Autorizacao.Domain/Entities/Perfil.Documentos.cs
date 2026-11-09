namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Documento instituidor das versões do perfil e a entrada em vigor que a verificação dele dispara.</summary>
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

        return versao.Value!.Documento is { Situacao: SituacaoDoAnexo.Verificado } documento
            ? Result<DocumentoDaVersao>.Success(documento)
            : Result<DocumentoDaVersao>.Failure(PerfilErros.DocumentoNaoVerificado());
    }

    /// <summary>
    /// Registra a recusa, ou o pedido de reenvio, do verificador. Só vale com o documento em verificação; o resultado
    /// repetido ou fora de hora não muda nada e devolve falso.
    /// </summary>
    public bool RegistrarDocumentoRecusado(Guid documentoId, DateTimeOffset instante) =>
        DocumentoPorId(documentoId)?.RegistrarRecusado(instante) ?? false;

    /// <summary>
    /// Registra o veredicto "verificado". Numa versão pendente é a entrada em vigor: repete as conferências contra o que
    /// há agora (vigente, concessão e nome) e, se passam, troca a vigência; se alguma falha, a versão é recusada com o
    /// motivo e o documento guarda o veredicto. Numa versão já cancelada ou recusada só o documento é gravado. Quando a
    /// versão entra em vigor, o resultado traz a troca, que quem chama aplica às concessões e aos pedidos do perfil.
    /// </summary>
    public ResultadoDoDocumentoVerificado RegistrarDocumentoVerificado(
        Guid documentoId,
        string sha256,
        DateTimeOffset instante,
        ContextoDaVersao contexto,
        ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(catalogo);

        VersaoDoPerfil? versao = _versoes.Find(v => v.Documentos.Any(d => d.Id == documentoId));
        DocumentoDaVersao? documento = versao?.Documentos.First(d => d.Id == documentoId);
        if (versao is null || !documento!.RegistrarVerificado(sha256, instante))
        {
            return ResultadoDoDocumentoVerificado.Ignorado;
        }

        return versao.Pendente
            ? EntrarEmVigor(versao, instante, contexto, catalogo)
            : ResultadoDoDocumentoVerificado.Gravado;
    }

    private static DomainError RecusaDaConfirmacao(VersaoDoPerfil versao) =>
        versao.Pendente ? PerfilErros.DocumentoNaoAguardaEnvio() : PerfilErros.VersaoNaoPendente();

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

        return versao.Value!.Pendente ? versao : Result<VersaoDoPerfil>.Failure(PerfilErros.VersaoNaoPendente());
    }

    private ResultadoDoDocumentoVerificado EntrarEmVigor(
        VersaoDoPerfil versao, DateTimeOffset instante, ContextoDaVersao contexto, ICatalogoDePermissoesConsultavel catalogo)
    {
        MotivoDaRecusaDaVersao? motivo = ConferenciasDaVersao.DaEntradaEmVigor(versao, VersaoVigente, contexto, catalogo);
        if (motivo is { } recusa)
        {
            versao.Recusar(recusa, instante);
            return ResultadoDoDocumentoVerificado.VersaoRecusada;
        }

        return ResultadoDoDocumentoVerificado.EntrouEmVigor(TrocarVigencia(versao, instante));
    }

    // A vigência aberta se encerra antes de a nova abrir: o banco admite uma só aberta por perfil.
    private TrocaDeVigencia TrocarVigencia(VersaoDoPerfil nova, DateTimeOffset instante)
    {
        VersaoDoPerfil? anterior = VersaoVigente;
        _vigencias.Find(static v => v.Aberta)?.Encerrar(instante);
        anterior?.Encerrar();

        nova.EntrarEmVigor();
        _vigencias.Add(VigenciaDoPerfil.Abrir(Id, nova.Id, instante));
        Nome = nova.Nome;
        Descricao = nova.Descricao;

        return new TrocaDeVigencia(anterior, nova);
    }
}
