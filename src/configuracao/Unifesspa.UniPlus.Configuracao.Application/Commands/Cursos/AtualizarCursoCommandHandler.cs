namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Cursos;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarCursoCommand"/>. Como o código é editável,
/// confere a unicidade entre cursos vivos quando ele muda (ignorando o próprio
/// registro) e protege a corrida traduzindo a violação do índice único parcial
/// em <c>CodigoJaExiste</c>.
/// </summary>
public static class AtualizarCursoCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarCursoCommand command,
        ICursoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Curso? curso = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (curso is null)
        {
            return Result.Failure(new DomainError(
                CursoErrorCodes.NaoEncontrado,
                "Curso não encontrado."));
        }

        // Código é case-sensitive (Ordinal), normalizado por Trim no agregado — só
        // checa colisão quando o código efetivamente muda.
        if (!string.Equals(command.Codigo.Trim(), curso.Codigo, StringComparison.Ordinal)
            && await repository.CodigoExisteEntreVivosAsync(command.Codigo, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(CodigoJaExisteErro(command.Codigo));
        }

        Result atualizarResult = curso.Atualizar(
            command.Codigo,
            command.Nome,
            command.Grau,
            command.NivelEnsino,
            command.GrupoAreaEnem);

        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsCodigoConflict(constraint))
        {
            // Corrida entre a checagem de unicidade e o UPDATE: o índice único parcial
            // dispara 23505 e viramos o mesmo CodigoJaExiste do caminho não-race.
            return Result.Failure(CodigoJaExisteErro(command.Codigo));
        }

        return Result.Success();
    }

    private static DomainError CodigoJaExisteErro(string codigo) =>
        new(CursoErrorCodes.CodigoJaExiste,
            $"Já existe um curso vivo com o código '{codigo}'.");
}
