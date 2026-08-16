namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Valida o agregado por inteiro primeiro (sem I/O) — código, nome e descrição
/// acumulam no mesmo lote — só então confere a unicidade do código entre vivos,
/// com o código já normalizado.
/// </summary>
public static class CriarTipoEtapaCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarTipoEtapaCommand command,
        ITipoEtapaRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<TipoEtapa> criar = TipoEtapa.Criar(command.Codigo, command.Nome, command.Descricao);
        if (criar.IsFailure)
        {
            return Result<Guid>.ValidationFailure(criar.Errors);
        }

        TipoEtapa tipo = criar.Value!;

        if (await repository.CodigoExisteAsync(tipo.Codigo, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoJaExiste());
        }

        await repository.AdicionarAsync(tipo, cancellationToken).ConfigureAwait(false);
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (UniqueConstraintViolation.EhConflitoDeCodigo(exception))
        {
            // Sem descartar, a entidade Added continua rastreada e o SaveChangesAsync
            // automático do Wolverine (AutoApplyTransactions) tenta a mesma inserção de
            // novo FORA deste catch — a mesma violação estoura sem tradução, e o 409
            // pretendido vira 500.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(CodigoJaExiste());
        }
        return Result<Guid>.Success(tipo.Id);
    }

    private static DomainError CodigoJaExiste() => new(
        TipoEtapaErrorCodes.CodigoJaExiste,
        "Já existe um tipo de etapa com o código informado.");
}
