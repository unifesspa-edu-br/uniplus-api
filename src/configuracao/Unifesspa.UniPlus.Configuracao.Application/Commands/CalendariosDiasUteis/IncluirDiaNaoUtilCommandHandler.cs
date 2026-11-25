namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="IncluirDiaNaoUtilCommand"/>: carrega o calendário (vigente ou
/// rascunho), inclui a data via <see cref="CalendarioDiasUteis.IncluirDiaNaoUtil"/> e
/// persiste. Marca o pai como alterado (<see cref="ICalendarioDiasUteisRepository.RegistrarInclusaoDeDiaNaoUtil"/>)
/// para que o <c>UPDATE</c> dele participe do mesmo <c>SaveChangesAsync</c> do
/// <c>INSERT</c> do filho — sem isso, uma remoção (soft-delete) concorrente do
/// mesmo calendário não vira conflito, porque inserir só o filho nunca compara o
/// <c>xmin</c> do pai.
/// </summary>
public static class IncluirDiaNaoUtilCommandHandler
{
    public static async Task<Result<CalendarioDiasUteisDto>> Handle(
        IncluirDiaNaoUtilCommand command,
        ICalendarioDiasUteisRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        CalendarioDiasUteis? calendario = await repository.ObterPorIdAsync(command.CalendarioId, cancellationToken).ConfigureAwait(false);
        if (calendario is null)
        {
            return RecusaDeCalendarioNaoEncontrado();
        }
        return await IncluirNoCalendarioAsync(calendario, command, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<CalendarioDiasUteisDto>> IncluirNoCalendarioAsync(
        CalendarioDiasUteis calendario,
        IncluirDiaNaoUtilCommand command,
        ICalendarioDiasUteisRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result<DiaNaoUtil> inclusaoResult = calendario.IncluirDiaNaoUtil(CriacaoDoDiaNaoUtil(command));
        if (inclusaoResult.IsFailure)
        {
            return Result<CalendarioDiasUteisDto>.ValidationFailure(inclusaoResult.Errors);
        }
        return await GravarInclusaoAsync(calendario, inclusaoResult.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static Result<CalendarioDiasUteisDto> RecusaDeCalendarioNaoEncontrado()
        => Result<CalendarioDiasUteisDto>.Failure(new DomainError(
            CalendarioDiasUteisErrorCodes.NaoEncontrado,
            "Calendário de dias úteis não encontrado."));

    private static DiaNaoUtilCriacao? CriacaoDoDiaNaoUtil(IncluirDiaNaoUtilCommand command)
        => command.Item is null
            ? null
            : new DiaNaoUtilCriacao(
                command.Item.Abrangencia,
                command.Item.MunicipioIbge,
                command.Item.MunicipioNome,
                command.Item.MunicipioUf,
                command.Item.Data,
                command.Item.Descricao,
                command.Item.Uf);

    private static async Task<Result<CalendarioDiasUteisDto>> GravarInclusaoAsync(
        CalendarioDiasUteis calendario,
        DiaNaoUtil diaNaoUtil,
        ICalendarioDiasUteisRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        repository.RegistrarInclusaoDeDiaNaoUtil(calendario, diaNaoUtil);
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueIndexViolation.IsDiaNaoUtilDuplicado(ex))
        {
            return RecusaDeDataDuplicada(unitOfWork);
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            return RecusaDeConflitoDeConcorrencia(unitOfWork);
        }
        return Result<CalendarioDiasUteisDto>.Success(calendario.ToDto());
    }

    /// <summary>
    /// Em inclusões concorrentes da mesma data, abrangência, município e UF, ambas podem
    /// passar pela checagem em memória antes da confirmação da outra; a última recebe a
    /// violação do índice único. Descarta as alterações para que o SaveChangesAsync
    /// automático do outbox não tente gravar novamente o DiaNaoUtil rastreado, lançando
    /// a mesma exceção fora do catch e causando um 500 em vez de 422.
    /// </summary>
    private static Result<CalendarioDiasUteisDto> RecusaDeDataDuplicada(IUnitOfWork unitOfWork)
    {
        unitOfWork.DescartarAlteracoesNaoSalvas();
        return Result<CalendarioDiasUteisDto>.Failure(new DomainError(
            CalendarioDiasUteisErrorCodes.DataDuplicadaNoCalendario,
            "Data duplicada no calendário (mesma abrangência, município e UF)."));
    }

    /// <summary>
    /// Trata a corrida entre a inclusão e a remoção (soft-delete) ou ativação de vigência
    /// concorrente do mesmo calendário. O UPDATE do pai, forçado por
    /// RegistrarInclusaoDeDiaNaoUtil, compara o xmin lido com o atual; se a linha mudou,
    /// afeta zero linhas e o EF Core lança a exceção. Sem marcar o pai como Modified, o
    /// INSERT do filho não verifica o pai e uma remoção concorrente poderia deixar uma
    /// linha dias_nao_uteis inalcançável sob um calendário com is_deleted = true.
    /// </summary>
    private static Result<CalendarioDiasUteisDto> RecusaDeConflitoDeConcorrencia(IUnitOfWork unitOfWork)
    {
        unitOfWork.DescartarAlteracoesNaoSalvas();
        return Result<CalendarioDiasUteisDto>.Failure(new DomainError(
            CalendarioDiasUteisErrorCodes.ConflitoDeConcorrencia,
            "Este calendário foi modificado concorrentemente (possivelmente removido). Tente novamente."));
    }
}
