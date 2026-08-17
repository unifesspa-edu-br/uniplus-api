namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarPesoAreaEnemCommand"/> (convention-based Wolverine):
/// valida o payload primeiro (422, sem I/O — validação sempre vence I/O), confere
/// a unicidade do par (resolução, grupo de área) entre linhas vivas (409, DB),
/// persiste e commita.
/// </summary>
public static class CriarPesoAreaEnemCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarPesoAreaEnemCommand command,
        IPesoAreaEnemRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

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
            return Result<Guid>.ValidationFailure(pesoResult.Errors);
        }

        PesoAreaEnem peso = pesoResult.Value!;

        if (await repository.ParExisteEntreVivosAsync(peso.Resolucao, peso.GrupoCurso.Valor, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(ParJaExisteErro());
        }

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
            // exceções propagam intactas. Descarta o rastreamento da entidade que não
            // foi persistida, senão o save automático do Wolverine repetiria a
            // violação fora deste catch.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(ParJaExisteErro());
        }

        return Result<Guid>.Success(peso.Id);
    }

    private static DomainError ParJaExisteErro() =>
        new(PesoAreaEnemErrorCodes.ParJaExiste,
            "Já existe uma linha de pesos viva para a resolução e o grupo informados.");
}
