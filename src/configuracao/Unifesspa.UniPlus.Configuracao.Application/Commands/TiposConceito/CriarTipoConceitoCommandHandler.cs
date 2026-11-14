namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposConceito;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Valida o agregado por inteiro primeiro (sem I/O) e só então confere a unicidade do código, já
/// normalizado e entre todos os tipos, inclusive os desativados.
/// </summary>
public static class CriarTipoConceitoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarTipoConceitoCommand command,
        ITipoConceitoRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<TipoConceito> criar = TipoConceito.Criar(command.Codigo, DefinicaoDoComando.De(command));
        return criar.IsFailure
            ? Result<Guid>.ValidationFailure(criar.Errors)
            : await RegistrarSeInedito(criar.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<Guid>> RegistrarSeInedito(
        TipoConceito tipo, ITipoConceitoRepository repository, IUnitOfWork unitOfWork, CancellationToken cancellationToken) =>
        await repository.CodigoExisteAsync(tipo.Codigo, cancellationToken).ConfigureAwait(false)
            ? Result<Guid>.Failure(RecusasDoTipoConceito.CodigoJaExiste())
            : await Persistir(tipo, repository, unitOfWork, cancellationToken).ConfigureAwait(false);

    private static async Task<Result<Guid>> Persistir(
        TipoConceito tipo, ITipoConceitoRepository repository, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        await repository.AdicionarAsync(tipo, cancellationToken).ConfigureAwait(false);
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (UniqueConstraintViolation.EhConflitoDeCodigo(exception))
        {
            // Sem descartar, a entidade Added continua rastreada e o SaveChangesAsync automático do
            // Wolverine tenta a mesma inserção de novo, fora deste catch: o 409 vira 500.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(RecusasDoTipoConceito.CodigoJaExiste());
        }

        return Result<Guid>.Success(tipo.Id);
    }
}
