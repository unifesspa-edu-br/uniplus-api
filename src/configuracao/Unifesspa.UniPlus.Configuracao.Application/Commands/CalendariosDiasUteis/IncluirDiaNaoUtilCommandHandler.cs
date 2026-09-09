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
/// persiste. Diferente de <c>MarcarVigenteCalendarioDiasUteisCommandHandler</c> e
/// <c>RemoverCalendarioDiasUteisCommandHandler</c>, este handler não captura
/// <c>DbUpdateConcurrencyException</c>: inserir um <c>DiaNaoUtil</c> filho não muta o
/// pai, então o <c>INSERT</c> não compara o <c>xmin</c> do <c>CalendarioDiasUteis</c>
/// — a única defesa de concorrência aqui é o índice único
/// <c>ix_dia_nao_util_unicidade</c> (api#1458).
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

        // O ChangeTracker não descobre sozinho o filho novo numa coleção-campo de
        // um agregado que já estava rastreado antes da inclusão (comprovado
        // empiricamente — ver AdicionarDiaNaoUtil) — sem este Add explícito, o
        // SaveChangesAsync abaixo silenciosamente não persiste a data incluída.
        repository.AdicionarDiaNaoUtil(inclusaoResult.Value!);

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

        return Result<CalendarioDiasUteisDto>.Success(calendario.ToDto());
    }
}
