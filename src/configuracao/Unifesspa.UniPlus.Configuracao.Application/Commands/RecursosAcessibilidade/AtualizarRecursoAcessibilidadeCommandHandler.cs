namespace Unifesspa.UniPlus.Configuracao.Application.Commands.RecursosAcessibilidade;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarRecursoAcessibilidadeCommand"/>. Como o nome é
/// editável, confere a unicidade entre recursos vivos quando ele muda (ignorando o
/// próprio registro) e protege a corrida traduzindo a violação do índice único
/// parcial em <c>NomeJaExiste</c>.
/// </summary>
public static class AtualizarRecursoAcessibilidadeCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarRecursoAcessibilidadeCommand command,
        IRecursoAcessibilidadeRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        RecursoAcessibilidade? recurso = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (recurso is null)
        {
            return Result.Failure(new DomainError(
                RecursoAcessibilidadeErrorCodes.NaoEncontrado,
                "Recurso de acessibilidade não encontrado."));
        }

        // Nome é case-sensitive (Ordinal), normalizado por Trim no agregado — só
        // checa colisão quando o nome efetivamente muda.
        if (!string.Equals(command.Nome.Trim(), recurso.Nome, StringComparison.Ordinal)
            && await repository.NomeExisteEntreVivosAsync(command.Nome, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(NomeJaExisteErro(command.Nome));
        }

        Result atualizarResult = recurso.Atualizar(command.Nome, command.Descricao);
        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsNomeConflict(constraint))
        {
            // Corrida entre a checagem de unicidade e o UPDATE: o índice único parcial
            // dispara 23505 e viramos o mesmo NomeJaExiste do caminho não-race.
            return Result.Failure(NomeJaExisteErro(command.Nome));
        }

        return Result.Success();
    }

    private static DomainError NomeJaExisteErro(string nome) =>
        new(RecursoAcessibilidadeErrorCodes.NomeJaExiste,
            $"Já existe um recurso de acessibilidade vivo com o nome '{nome}'.");
}
