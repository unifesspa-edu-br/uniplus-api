namespace Unifesspa.UniPlus.Configuracao.Application.Commands.RecursosAcessibilidade;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarRecursoAcessibilidadeCommand"/> (convention-based
/// Wolverine): valida o agregado por inteiro primeiro (422, sem I/O — validação sempre
/// vence I/O), confere a unicidade do código e do nome entre recursos vivos (409, DB),
/// persiste e commita. Protege a corrida check-then-act traduzindo a violação do
/// índice único parcial no conflito da constraint violada.
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

        Result<RecursoAcessibilidade> recursoResult = RecursoAcessibilidade.Criar(
            command.Codigo,
            command.Nome,
            command.Descricao);

        if (recursoResult.IsFailure)
        {
            return Result<Guid>.ValidationFailure(recursoResult.Errors);
        }

        RecursoAcessibilidade recurso = recursoResult.Value!;

        if (await repository.CodigoExisteEntreVivosAsync(recurso.Codigo.Valor, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(UniqueConstraintViolation.CodigoJaExisteErro());
        }

        if (await repository.NomeExisteEntreVivosAsync(recurso.Nome, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(UniqueConstraintViolation.NomeJaExisteErro());
        }

        await repository.AdicionarAsync(recurso, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.ConflitoDeUnicidade(ex) is { } conflito)
        {
            // Corrida entre as checagens de unicidade e o INSERT (check-then-act): o
            // índice único parcial dispara 23505 e viramos o mesmo conflito do caminho
            // não-race — 409 consistente, em vez de deixar o DbUpdateException virar 500
            // no middleware global. Descarta o rastreamento da entidade que não foi
            // persistida, senão o save automático do Wolverine repetiria a violação fora
            // deste catch.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(conflito);
        }

        return Result<Guid>.Success(recurso.Id);
    }
}
