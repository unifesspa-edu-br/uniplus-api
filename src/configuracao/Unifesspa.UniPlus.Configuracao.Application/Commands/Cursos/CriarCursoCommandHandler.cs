namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Cursos;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarCursoCommand"/> (convention-based Wolverine):
/// confere a unicidade do código entre cursos vivos, cria o agregado, persiste e
/// commita. Protege a corrida check-then-act traduzindo a violação do índice
/// único parcial em <c>CodigoJaExiste</c>.
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

        if (await repository.CodigoExisteEntreVivosAsync(command.Codigo, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoJaExisteErro(command.Codigo));
        }

        Result<Curso> cursoResult = Curso.Criar(
            command.Codigo,
            command.Nome,
            command.Grau,
            command.NivelEnsino,
            command.GrupoAreaEnem);

        if (cursoResult.IsFailure)
        {
            return Result<Guid>.Failure(cursoResult.Error!);
        }

        Curso curso = cursoResult.Value!;
        await repository.AdicionarAsync(curso, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsCodigoConflict(constraint))
        {
            // Corrida entre CodigoExisteEntreVivosAsync e o INSERT (check-then-act): o
            // índice único parcial dispara 23505 e viramos o mesmo CodigoJaExiste do
            // caminho não-race — 409 consistente, em vez de deixar o DbUpdateException
            // virar 500 no middleware global. O filtro do `when` garante que outras
            // exceções propagam intactas.
            return Result<Guid>.Failure(CodigoJaExisteErro(command.Codigo));
        }

        return Result<Guid>.Success(curso.Id);
    }

    private static DomainError CodigoJaExisteErro(string codigo) =>
        new(CursoErrorCodes.CodigoJaExiste,
            $"Já existe um curso vivo com o código '{codigo}'.");
}
