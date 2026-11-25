namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Cursos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarCursoCommand"/>. Valida os cinco campos
/// editáveis primeiro (sem I/O) — validação sempre vence 404 — só então busca o
/// registro por Id; como o código é editável, confere a unicidade entre cursos
/// vivos quando ele muda (ignorando o próprio registro) e protege a corrida
/// traduzindo a violação do índice único parcial em <c>CodigoJaExiste</c>.
/// </summary>
public static class AtualizarCursoCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarCursoCommand command,
        ICursoRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<(string Codigo, string Nome, string Grau, string NivelEnsino, GrupoCurso? GrupoAreaEnem)> campos =
            Curso.ValidarCamposEditaveis(command.Codigo, command.Nome, command.Grau, command.NivelEnsino, command.GrupoAreaEnem);
        if (campos.IsFailure)
        {
            return Result.ValidationFailure(campos.Errors);
        }

        return await AtualizarAsync(command, campos.Value.Codigo, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> AtualizarAsync(AtualizarCursoCommand comando, string codigo,
        ICursoRepository repository, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Curso? curso = await repository.ObterPorIdAsync(comando.Id, cancellationToken).ConfigureAwait(false);
        if (curso is null)
        {
            return Result.Failure(new DomainError(CursoErrorCodes.NaoEncontrado, "Curso não encontrado."));
        }
        if (!string.Equals(codigo, curso.Codigo, StringComparison.Ordinal)
            && await repository.CodigoExisteEntreVivosAsync(codigo, comando.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(CodigoJaExisteErro());
        }
        return await AplicarAsync(curso, comando, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> AplicarAsync(Curso curso, AtualizarCursoCommand comando,
        IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Result resultado = curso.Atualizar(comando.Codigo, comando.Nome, comando.Grau, comando.NivelEnsino, comando.GrupoAreaEnem);
        return resultado.IsFailure ? resultado : await ConfirmarAsync(unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> ConfirmarAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception excecao) when (UniqueConstraintViolation.GetViolatedConstraint(excecao) is { } restricao
            && UniqueConstraintViolation.IsCodigoConflict(restricao))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(CodigoJaExisteErro());
        }
    }

    private static DomainError CodigoJaExisteErro() =>
        new(CursoErrorCodes.CodigoJaExiste,
            "Já existe um curso vivo com o código informado.");
}
