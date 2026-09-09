namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="IncluirDiaNaoUtilCommand"/>: carrega o dataset (vigente ou
/// rascunho), inclui a data via <see cref="CalendarioDiasUteis.IncluirDiaNaoUtil"/> e
/// persiste. Marca o pai como alterado (<see cref="ICalendarioDiasUteisRepository.RegistrarInclusaoDeDiaNaoUtil"/>)
/// para que o <c>UPDATE</c> dele participe do mesmo <c>SaveChangesAsync</c> do
/// <c>INSERT</c> do filho — sem isso, uma remoção (soft-delete) concorrente do
/// mesmo dataset não vira conflito, porque inserir só o filho nunca compara o
/// <c>xmin</c> do pai (achado de revisão, api#1458 PR #1460).
/// </summary>
public static class IncluirDiaNaoUtilCommandHandler
{
    public static async Task<Result<CalendarioDiasUteisDto>> Handle(
        IncluirDiaNaoUtilCommand command,
        ICalendarioDiasUteisRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        CalendarioDiasUteis? calendario = await repository
            .ObterPorIdAsync(command.CalendarioId, cancellationToken)
            .ConfigureAwait(false);
        if (calendario is null)
        {
            return Result<CalendarioDiasUteisDto>.Failure(new DomainError(
                CalendarioDiasUteisErrorCodes.NaoEncontrado,
                "Calendário de dias úteis não encontrado."));
        }

        DiaNaoUtilCriacao? item = command.Item is null
            ? null
            : new DiaNaoUtilCriacao(
                command.Item.Abrangencia,
                command.Item.MunicipioIbge,
                command.Item.MunicipioNome,
                command.Item.MunicipioUf,
                command.Item.Data,
                command.Item.Descricao,
                command.Item.Uf);

        Result<DiaNaoUtil> inclusaoResult = calendario.IncluirDiaNaoUtil(item);
        if (inclusaoResult.IsFailure)
        {
            return Result<CalendarioDiasUteisDto>.ValidationFailure(inclusaoResult.Errors);
        }

        repository.RegistrarInclusaoDeDiaNaoUtil(calendario, inclusaoResult.Value!);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueIndexViolation.IsDiaNaoUtilDuplicado(ex))
        {
            // Corrida entre duas inclusões concorrentes da mesma combinação
            // (mesma data/abrangência/município/UF): ambas passaram pela checagem
            // em memória contra o agregado carregado antes de a outra commitar —
            // quem confirma por último recebe a violação do índice único. Mesmo
            // raciocínio de descarte do ChangeTracker de
            // MarcarVigenteCalendarioDiasUteisCommandHandler: sem isso, o
            // SaveChangesAsync automático do outbox (ADR-0004) tenta gravar o
            // DiaNaoUtil ainda rastreado de novo, e a mesma exceção estoura fora
            // deste catch (500 em vez de 422).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<CalendarioDiasUteisDto>.Failure(new DomainError(
                CalendarioDiasUteisErrorCodes.DataDuplicadaNoDataset,
                "Data duplicada no dataset (mesma abrangência, município e UF)."));
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            // Corrida entre esta inclusão e uma remoção (soft-delete) ou
            // ativação de vigência concorrente do MESMO dataset (xmin): o
            // UPDATE do pai, forçado por RegistrarInclusaoDeDiaNaoUtil, compara
            // o xmin lido na leitura contra o valor atual — se outra operação já
            // mudou a linha, este UPDATE afeta 0 linhas e o EF Core lança aqui.
            // Sem o Modified explícito no pai, esta corrida nunca apareceria:
            // o INSERT do filho sozinho não checa nada do pai, e uma remoção
            // concorrente deixaria uma linha dia_nao_util inalcançável sob um
            // calendário já escondido (is_deleted = true).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<CalendarioDiasUteisDto>.Failure(new DomainError(
                CalendarioDiasUteisErrorCodes.ConflitoDeConcorrencia,
                "Este dataset foi modificado concorrentemente (possivelmente removido). Tente novamente."));
        }

        return Result<CalendarioDiasUteisDto>.Success(calendario.ToDto());
    }
}
