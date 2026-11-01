namespace Unifesspa.UniPlus.Selecao.Application.Queries.CampoDaInscricao;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Wolverine.Attributes;

/// <summary>
/// A revelação de UM campo identificador de uma inscrição enviada: o CPF, o número do RG, do passaporte e qualquer outro
/// que o processo congelou como identificador (ADR-0153). Revelar é consulta de dado protegido a pedido, um registro por
/// vez, com a mesma decisão e o mesmo registro de qualquer outro acesso.
/// </summary>
/// <remarks>
/// A decisão é tomada aqui, e não na borda, para que a inscrição e o campo já estejam conferidos quando o registro os
/// nomear: a trilha nunca cita inscrição ou campo que não existem. Decide a permissão de revelar identificadores e registra
/// os dois resultados, com o código do fato e a ocorrência. A inscrição que não existe, a de outro processo, a em
/// rascunho, o campo que não é identificador e a recusa por falta de permissão respondem o mesmo 404. O valor só é
/// decifrado depois da decisão permitida, e o log traz quem revelou e qual campo, nunca o valor.
/// </remarks>
public static partial class RevelarCampoDaInscricaoQueryHandler
{
    /// <summary>
    /// <c>[NonTransactional]</c>: o verificador de acesso lê as concessões do <c>AutorizacaoDbContext</c> e os repositórios
    /// leem o <c>SelecaoDbContext</c>; o detector de transação do Wolverine recusa dois contextos. A consulta só lê.
    /// </summary>
    [NonTransactional]
    public static async Task<Result<CampoReveladoDto>> Handle(
        RevelarCampoDaInscricaoQuery query,
        IVerificadorDeAcesso verificador,
        IProcessoSeletivoRepository processoRepository,
        IInscricaoRepository inscricaoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IUserContext userContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(processoRepository);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(logger);

        Guid? unidade = await processoRepository
            .ObterUnidadeAdministradoraAsync(query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        VinculoDaInscricao? vinculo = await inscricaoRepository
            .ObterVinculoAsync(query.InscricaoId, cancellationToken).ConfigureAwait(false);
        if (unidade is null
            || vinculo is null
            || vinculo.ProcessoSeletivoId != query.ProcessoSeletivoId
            || vinculo.Situacao == SituacaoInscricao.Rascunho)
        {
            return Result<CampoReveladoDto>.Failure(CampoNaoEncontrado);
        }

        Result<VersaoConfiguracao> versao = await VersaoFixadaDaInscricaoResolver
            .ResolverAsync(vinculo.VersaoFormularioId, vinculo.ProcessoSeletivoId, processoRepository, cancellationToken)
            .ConfigureAwait(false);
        Result<EnvelopeReidratado> envelope = versao.IsFailure
            ? Result<EnvelopeReidratado>.Failure(versao.Error!)
            : ContextoDaVersaoFixadaResolver.Reidratar(versao.Value!, registroCodecs);
        if (envelope.IsFailure)
        {
            return Result<CampoReveladoDto>.Failure(envelope.Error!);
        }

        CampoIdentificador? campo = null;
        RespostaIdentificadora? resposta = null;
        foreach (CampoIdentificador candidato in FatosDaVersao.De(envelope.Value!).Identificadores(query.FatoCodigo, emGrupo: query.Ocorrencia is not null))
        {
            resposta = await inscricaoRepository
                .LocalizarIdentificadorAsync(query.InscricaoId, candidato.FatoCodigo, candidato.GrupoCodigo, query.Ocorrencia, cancellationToken)
                .ConfigureAwait(false);
            if (resposta is not null)
            {
                campo = candidato;
                break;
            }
        }

        if (campo is null || resposta is null)
        {
            return Result<CampoReveladoDto>.Failure(CampoNaoEncontrado);
        }

        ResultadoDoAcesso acesso = await AcessoAoProcesso
            .VerificarNaUnidadeAsync(
                verificador, unidade, query.ProcessoSeletivoId, PermissaoSelecao.DadosIdentificadoresRevelar,
                ResourceTiposSelecao.Inscricao, sensibilidadeDoResource: null, cancellationToken, query.InscricaoId,
                campo.FatoCodigo, query.InscricaoId, campo.GrupoCodigo is null ? null : query.Ocorrencia)
            .ConfigureAwait(false);
        if (acesso is ResultadoDoAcesso.IdentidadeIncompleta or ResultadoDoAcesso.Impedido)
        {
            return Result<CampoReveladoDto>.Failure(AcessoAoProcesso.Recusa(acesso));
        }

        if (acesso != ResultadoDoAcesso.Permitido)
        {
            return Result<CampoReveladoDto>.Failure(CampoNaoEncontrado);
        }

        string? bruto = await inscricaoRepository.LerIdentificadorAsync(resposta.RespostaId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(bruto))
        {
            return Result<CampoReveladoDto>.Failure(CampoNaoEncontrado);
        }

        string valor = campo.EhCpf && Cpf.Criar(bruto) is { IsSuccess: true } cpf ? cpf.Value!.Formatado : bruto;

        LogCampoRevelado(logger, userContext.UserId ?? string.Empty, campo.FatoCodigo, query.ProcessoSeletivoId, query.InscricaoId);
        return Result<CampoReveladoDto>.Success(new CampoReveladoDto(campo.FatoCodigo, valor));
    }

    private static DomainError CampoNaoEncontrado { get; } = new(
        InscricaoErrorCodes.CampoNaoEncontrado,
        "Campo não encontrado nesta inscrição.");

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Campo {FatoCodigo} da inscrição {InscricaoId} do processo {ProcessoSeletivoId} revelado a {Usuario}")]
    private static partial void LogCampoRevelado(ILogger logger, string usuario, string fatoCodigo, Guid processoSeletivoId, Guid inscricaoId);
}
