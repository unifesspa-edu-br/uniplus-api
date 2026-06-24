namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarPesoAreaEnemCommand"/> (convention-based Wolverine):
/// confere a unicidade do par (resolução, grupo de área) entre linhas vivas, cria
/// o agregado, persiste e commita.
/// </summary>
public static class CriarPesoAreaEnemCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarPesoAreaEnemCommand command,
        IPesoAreaEnemRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        if (await repository.ParExisteEntreVivosAsync(command.Resolucao, command.GrupoCurso, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(ParJaExisteErro(command.Resolucao, command.GrupoCurso));
        }

        Result<PesoAreaEnem> pesoResult = PesoAreaEnem.Criar(
            command.Resolucao,
            command.GrupoCurso,
            command.PesoRedacao,
            command.PesoCienciasNatureza,
            command.PesoCienciasHumanas,
            command.PesoLinguagens,
            command.PesoMatematica,
            command.CorteRedacao,
            command.BaseLegal);

        if (pesoResult.IsFailure)
        {
            return Result<Guid>.Failure(pesoResult.Error!);
        }

        PesoAreaEnem peso = pesoResult.Value!;
        await repository.AdicionarAsync(peso, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsParConflict(constraint))
        {
            // Corrida entre ParExisteEntreVivosAsync e o INSERT (check-then-act): o
            // índice único parcial dispara 23505 e viramos o mesmo ParJaExiste do
            // caminho não-race — 409 consistente, em vez de deixar o DbUpdateException
            // virar 500 no middleware global. O filtro do `when` garante que outras
            // exceções propagam intactas.
            return Result<Guid>.Failure(ParJaExisteErro(command.Resolucao, command.GrupoCurso));
        }

        return Result<Guid>.Success(peso.Id);
    }

    private static DomainError ParJaExisteErro(string resolucao, string grupoCurso) =>
        new(PesoAreaEnemErrorCodes.ParJaExiste,
            $"Já existe uma linha de pesos viva para a resolução '{resolucao}' e o grupo '{grupoCurso}'.");
}
