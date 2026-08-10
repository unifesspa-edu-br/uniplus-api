namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class CriarTipoProcessoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarTipoProcessoCommand command,
        ITipoProcessoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        if (await repository.CodigoExisteAsync(command.Codigo, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoJaExiste(command.Codigo));
        }

        Result<TipoProcesso> criar = TipoProcesso.Criar(command.Codigo, command.Nome, command.Descricao);
        if (criar.IsFailure)
        {
            return Result<Guid>.Failure(criar.Error!);
        }

        await repository.AdicionarAsync(criar.Value!, cancellationToken).ConfigureAwait(false);
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (UniqueConstraintViolation.EhConflitoDeCodigo(exception))
        {
            return Result<Guid>.Failure(CodigoJaExiste(command.Codigo));
        }
        return Result<Guid>.Success(criar.Value!.Id);
    }

    private static DomainError CodigoJaExiste(string codigo) => new(
        TipoProcessoErrorCodes.CodigoJaExiste,
        $"O código '{codigo.Trim()}' já foi reservado para um tipo de processo seletivo.");
}
