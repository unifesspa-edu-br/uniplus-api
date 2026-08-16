namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Cursos;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarCursoCommand"/> (convention-based Wolverine):
/// valida o agregado por inteiro primeiro (sem I/O) — os cinco campos acumulam
/// no mesmo lote — só então confere a unicidade do código entre vivos, com o
/// código já normalizado. Protege a corrida check-then-act traduzindo a violação
/// do índice único parcial em <c>CodigoJaExiste</c>.
/// </summary>
public static class CriarCursoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarCursoCommand command,
        ICursoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<Curso> criar = Curso.Criar(
            command.Codigo, command.Nome, command.Grau, command.NivelEnsino, command.GrupoAreaEnem);
        if (criar.IsFailure)
        {
            return Result<Guid>.ValidationFailure(criar.Errors);
        }

        Curso curso = criar.Value!;

        if (await repository.CodigoExisteEntreVivosAsync(curso.Codigo, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoJaExisteErro());
        }

        await repository.AdicionarAsync(curso, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsCodigoConflict(constraint))
        {
            // Sem descartar, a entidade Added continua rastreada e o SaveChangesAsync
            // automático do Wolverine (AutoApplyTransactions) tenta a mesma inserção de
            // novo FORA deste catch — a mesma violação estoura sem tradução, e o 409
            // pretendido vira 500.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(CodigoJaExisteErro());
        }

        return Result<Guid>.Success(curso.Id);
    }

    private static DomainError CodigoJaExisteErro() =>
        new(CursoErrorCodes.CodigoJaExiste,
            "Já existe um curso vivo com o código informado.");
}
