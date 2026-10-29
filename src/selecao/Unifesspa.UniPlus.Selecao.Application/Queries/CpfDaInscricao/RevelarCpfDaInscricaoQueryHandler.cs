namespace Unifesspa.UniPlus.Selecao.Application.Queries.CpfDaInscricao;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Wolverine.Attributes;

/// <summary>
/// A revelação do CPF completo de UMA inscrição enviada. A decisão é tomada aqui, e não na borda, para que a inscrição
/// já esteja conferida (existe, é do processo da rota e saiu de rascunho) quando o registro da decisão a nomear: a
/// trilha de quem revelou nunca cita uma inscrição que não existe.
/// </summary>
/// <remarks>
/// Consultar as inscrições não basta: a permissão exigida é a de revelar. A inscrição que não existe, a de outro
/// processo, a em rascunho e a recusa por falta de permissão respondem o mesmo 404, e nenhum revela o outro. O CPF só
/// é decifrado depois da decisão permitida, e o log traz quem revelou e qual inscrição, nunca o CPF.
/// </remarks>
public static partial class RevelarCpfDaInscricaoQueryHandler
{
    /// <summary>
    /// <c>[NonTransactional]</c>: o verificador de acesso lê as concessões do <c>AutorizacaoDbContext</c> e o repositório
    /// lê o <c>SelecaoDbContext</c>; o detector de transação do Wolverine recusa dois contextos. A consulta só lê.
    /// </summary>
    [NonTransactional]
    public static async Task<Result<CpfReveladoDto>> Handle(
        RevelarCpfDaInscricaoQuery query,
        IVerificadorDeAcesso verificador,
        IProcessoSeletivoRepository processoRepository,
        IInscricaoRepository inscricaoRepository,
        IUserContext userContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(processoRepository);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
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
            return Result<CpfReveladoDto>.Failure(CpfNaoEncontrado);
        }

        ResultadoDoAcesso acesso = await AcessoAoProcesso
            .VerificarNaUnidadeAsync(
                verificador, unidade, query.ProcessoSeletivoId, PermissaoSelecao.InscricoesRevelarCpf,
                AcessoAoProcesso.RecursoInscricao, sensibilidadeDoRecurso: null, cancellationToken, query.InscricaoId)
            .ConfigureAwait(false);
        if (acesso is ResultadoDoAcesso.IdentidadeIncompleta or ResultadoDoAcesso.Impedido)
        {
            return Result<CpfReveladoDto>.Failure(AcessoAoProcesso.Recusa(acesso));
        }

        if (acesso != ResultadoDoAcesso.Permitido)
        {
            return Result<CpfReveladoDto>.Failure(CpfNaoEncontrado);
        }

        Cpf? cpf = await inscricaoRepository.ObterCpfAsync(query.InscricaoId, cancellationToken).ConfigureAwait(false);
        if (cpf is null)
        {
            return Result<CpfReveladoDto>.Failure(CpfNaoEncontrado);
        }

        LogCpfRevelado(logger, userContext.UserId ?? string.Empty, query.ProcessoSeletivoId, query.InscricaoId);
        return Result<CpfReveladoDto>.Success(new CpfReveladoDto(cpf.Formatado));
    }

    private static DomainError CpfNaoEncontrado { get; } = new(
        InscricaoErrorCodes.NaoEncontrada,
        "Inscrição não encontrada neste processo seletivo.");

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "CPF da inscrição {InscricaoId} do processo {ProcessoSeletivoId} revelado a {Usuario}")]
    private static partial void LogCpfRevelado(ILogger logger, string usuario, Guid processoSeletivoId, Guid inscricaoId);
}
