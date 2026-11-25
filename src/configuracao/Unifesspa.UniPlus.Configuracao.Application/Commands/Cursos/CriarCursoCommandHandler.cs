namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Cursos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarCursoCommand"/> (convention-based Wolverine):
/// valida o agregado por inteiro primeiro (sem I/O) — os cinco campos acumulam
/// no mesmo lote — só então confere a unicidade do código entre vivos, com o
/// código recebido. Protege a corrida check-then-act traduzindo a violação
/// do índice único parcial em <c>CodigoJaExiste</c>.
/// </summary>
public static class CriarCursoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarCursoCommand command,
        ICursoRepository repository,
        IUnitOfWork unitOfWork,
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

        return await SalvarAsync(criar.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<Guid>> SalvarAsync(
        Curso curso, ICursoRepository repository, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        if (await repository.CodigoExisteEntreVivosAsync(curso.Codigo, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoJaExisteErro());
        }
        await repository.AdicionarAsync(curso, cancellationToken).ConfigureAwait(false);
        return await ConfirmarAsync(curso, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<Guid>> ConfirmarAsync(Curso curso, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result<Guid>.Success(curso.Id);
        }
        catch (Exception excecao) when (UniqueConstraintViolation.GetViolatedConstraint(excecao) is { } restricao
            && UniqueConstraintViolation.IsCodigoConflict(restricao))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(CodigoJaExisteErro());
        }
    }

    private static DomainError CodigoJaExisteErro() =>
        new(CursoErrorCodes.CodigoJaExiste,
            "Já existe um curso vivo com o código informado.");
}
