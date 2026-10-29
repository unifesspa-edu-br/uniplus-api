namespace Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosDaInscricao;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Wolverine.Attributes;

/// <summary>
/// O acesso de quem opera o processo ao documento de uma inscrição. A sensibilidade do recurso é a classificação
/// congelada da exigência a que o arquivo responde (ADR-0136): a sensível exige a permissão de documentos sensíveis, e as
/// demais, a de documentos pessoais. Consultar as inscrições não abre documento algum.
/// </summary>
/// <remarks>
/// <para>
/// A ordem: o processo e a unidade dona; o arquivo, o vínculo da inscrição e a classificação congelada da exigência, por
/// leituras que não decifram CPF nem respostas; e então UMA decisão, com a permissão que a classificação exige e a
/// sensibilidade real do recurso. O que não existe, é de outra inscrição ou processo, ou é de inscrição em rascunho
/// recebe a recusa sem decisão gravada, pois não há recurso sobre o qual decidir; assim a trilha nunca registra uma
/// negativa de permissão que não foi a exigida.
/// </para>
/// <para>
/// A recusa cobre numa mesma resposta o que não existe e o que a pessoa não pode ler: nenhum revela o outro. A decisão
/// fica no registro de decisões de acesso; o log traz só quem abriu e qual arquivo, sem a URL nem a chave.
/// </para>
/// </remarks>
public static partial class ObterAcessoAoDocumentoDaInscricaoQueryHandler
{
    /// <summary>
    /// <c>[NonTransactional]</c>: o verificador de acesso lê as concessões do <c>AutorizacaoDbContext</c>, e o handler também
    /// depende do <c>SelecaoDbContext</c>; o detector de transação do Wolverine recusa dois contextos. A consulta só lê.
    /// </summary>
    [NonTransactional]
    public static async Task<Result<AcessoArquivoDoCandidatoDto>> Handle(
        ObterAcessoAoDocumentoDaInscricaoQuery query,
        IVerificadorDeAcesso verificador,
        IProcessoSeletivoRepository processoRepository,
        IInscricaoRepository inscricaoRepository,
        IDocumentoDaInscricaoRepository documentoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IArquivoDoCandidatoStorage storage,
        IUserContext userContext,
        TimeProvider clock,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(processoRepository);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(documentoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(logger);

        Guid? unidade = await processoRepository
            .ObterUnidadeAdministradoraAsync(query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        ArquivoDaApresentacao? arquivo = await documentoRepository.ObterArquivoAsync(query.ArquivoId, cancellationToken).ConfigureAwait(false);
        VinculoDaInscricao? vinculo = arquivo is null || arquivo.InscricaoId != query.InscricaoId
            ? null
            : await inscricaoRepository.ObterVinculoAsync(arquivo.InscricaoId, cancellationToken).ConfigureAwait(false);
        if (unidade is null
            || arquivo is null
            || vinculo is null
            || vinculo.ProcessoSeletivoId != query.ProcessoSeletivoId
            || vinculo.Situacao == SituacaoInscricao.Rascunho)
        {
            return Result<AcessoArquivoDoCandidatoDto>.Failure(AcessoAoDocumentoDaInscricao.ArquivoNaoEncontrado);
        }

        ClassificacaoDeProtecao classificacao = await ClassificacaoDaExigenciaAsync(
                arquivo, vinculo, processoRepository, documentoRepository, registroCodecs, cancellationToken)
            .ConfigureAwait(false);

        PermissaoSelecao permissao = classificacao == ClassificacaoDeProtecao.Sensivel
            ? PermissaoSelecao.DocumentosSensiveisConsultar
            : PermissaoSelecao.DocumentosPessoaisConsultar;

        ResultadoDoAcesso acesso = await AcessoAoProcesso
            .VerificarNaUnidadeAsync(
                verificador, unidade, query.ProcessoSeletivoId, permissao, AcessoAoProcesso.RecursoDocumento,
                classificacao, cancellationToken, query.ArquivoId)
            .ConfigureAwait(false);
        if (acesso is ResultadoDoAcesso.IdentidadeIncompleta or ResultadoDoAcesso.Impedido)
        {
            return Result<AcessoArquivoDoCandidatoDto>.Failure(AcessoAoProcesso.Recusa(acesso));
        }

        if (acesso != ResultadoDoAcesso.Permitido)
        {
            return Result<AcessoArquivoDoCandidatoDto>.Failure(AcessoAoDocumentoDaInscricao.ArquivoNaoEncontrado);
        }

        Result<AcessoArquivoDoCandidatoDto> emitido = await EmissaoDoAcessoAoArquivo
            .EmitirAsync(arquivo, documentoRepository, storage, clock, cancellationToken)
            .ConfigureAwait(false);
        if (emitido.IsSuccess)
        {
            LogAcessoEmitido(
                logger, userContext.UserId ?? string.Empty, arquivo.Id, arquivo.InscricaoId,
                emitido.Value!.ExpiraEm - PrazosDoArquivoEnviado.Leitura);
        }

        return emitido;
    }

    /// <summary>
    /// A classificação congelada da exigência na versão da inscrição. A que não se resolve vale como sensível: na dúvida
    /// sobre o que o arquivo é, a permissão mais restrita é a que o abre.
    /// </summary>
    private static async Task<ClassificacaoDeProtecao> ClassificacaoDaExigenciaAsync(
        ArquivoDaApresentacao arquivo,
        VinculoDaInscricao inscricao,
        IProcessoSeletivoRepository processoRepository,
        IDocumentoDaInscricaoRepository documentoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        ApresentacaoDeDocumento? apresentacao = await documentoRepository
            .ObterApresentacaoAsync(arquivo.ApresentacaoId, cancellationToken).ConfigureAwait(false);
        if (apresentacao is null)
        {
            return ClassificacaoDeProtecao.Sensivel;
        }

        Result<ContextoDaVersaoFixada> contexto = await ContextoDaVersaoFixadaResolver.ResolverAsync(
            inscricao.VersaoFormularioId, inscricao.ProcessoSeletivoId, processoRepository, registroCodecs,
            inscricao.OfertaCursoId, inscricao.OfertaCursoSegundaOpcaoId, cancellationToken).ConfigureAwait(false);

        return contexto.IsSuccess
            && ExigenciaDaInscricao.Localizar(contexto.Value!.Configuracao.Arvore, apresentacao.ExigenciaId) is { } localizada
                ? localizada.Exigencia.ClassificacaoProtecao
                : ClassificacaoDeProtecao.Sensivel;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Acesso de leitura ao arquivo {ArquivoId} da inscrição {InscricaoId} emitido a {Usuario} em {EmitidoEm}")]
    private static partial void LogAcessoEmitido(ILogger logger, string usuario, Guid arquivoId, Guid inscricaoId, DateTimeOffset emitidoEm);
}
