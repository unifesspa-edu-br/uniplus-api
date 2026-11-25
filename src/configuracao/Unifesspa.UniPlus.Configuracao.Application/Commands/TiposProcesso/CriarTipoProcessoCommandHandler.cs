namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Valida o agregado por inteiro primeiro (sem I/O) — código, nome e descrição
/// acumulam no mesmo lote — só então confere a unicidade do código entre vivos,
/// com o código validado.
/// </summary>
public static class CriarTipoProcessoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarTipoProcessoCommand command,
        ITipoProcessoRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        Result<TipoProcesso> criar = TipoProcesso.Criar(command.Codigo, command.Nome, command.Descricao);
        if (criar.IsFailure)
        {
            return Result<Guid>.ValidationFailure(criar.Errors);
        }
        TipoProcesso tipo = criar.Value!;
        if (await repository.CodigoExisteAsync(tipo.Codigo, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoJaExiste());
        }
        return await Persistir(tipo, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<Guid>> Persistir(
        TipoProcesso tipo, ITipoProcessoRepository repository, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        await repository.AdicionarAsync(tipo, cancellationToken).ConfigureAwait(false);
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result<Guid>.Success(tipo.Id);
        }
        catch (Exception exception) when (UniqueConstraintViolation.EhConflitoDeCodigo(exception))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(CodigoJaExiste());
        }
    }

    private static DomainError CodigoJaExiste() => new(
        TipoProcessoErrorCodes.CodigoJaExiste,
        "Já existe um tipo de processo seletivo com o código informado.");
}
