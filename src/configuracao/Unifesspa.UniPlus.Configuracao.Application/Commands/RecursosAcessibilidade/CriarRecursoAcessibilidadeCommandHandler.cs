namespace Unifesspa.UniPlus.Configuracao.Application.Commands.RecursosAcessibilidade;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarRecursoAcessibilidadeCommand"/> (convention-based
/// Wolverine): confere a unicidade do nome entre recursos vivos, cria o agregado,
/// persiste e commita. Protege a corrida check-then-act traduzindo a violação do
/// índice único parcial em <c>NomeJaExiste</c>.
/// </summary>
public static class CriarRecursoAcessibilidadeCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarRecursoAcessibilidadeCommand command,
        IRecursoAcessibilidadeRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        if (await repository.NomeExisteEntreVivosAsync(command.Nome, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(NomeJaExisteErro(command.Nome));
        }

        Result<RecursoAcessibilidade> recursoResult = RecursoAcessibilidade.Criar(
            command.Nome,
            command.Descricao);

        if (recursoResult.IsFailure)
        {
            return Result<Guid>.Failure(recursoResult.Error!);
        }

        RecursoAcessibilidade recurso = recursoResult.Value!;
        await repository.AdicionarAsync(recurso, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsNomeConflict(constraint))
        {
            // Corrida entre NomeExisteEntreVivosAsync e o INSERT (check-then-act): o
            // índice único parcial dispara 23505 e viramos o mesmo NomeJaExiste do
            // caminho não-race — 409 consistente, em vez de deixar o DbUpdateException
            // virar 500 no middleware global. O filtro do `when` garante que outras
            // exceções propagam intactas.
            return Result<Guid>.Failure(NomeJaExisteErro(command.Nome));
        }

        return Result<Guid>.Success(recurso.Id);
    }

    private static DomainError NomeJaExisteErro(string nome) =>
        new(RecursoAcessibilidadeErrorCodes.NomeJaExiste,
            $"Já existe um recurso de acessibilidade vivo com o nome '{nome}'.");
}
