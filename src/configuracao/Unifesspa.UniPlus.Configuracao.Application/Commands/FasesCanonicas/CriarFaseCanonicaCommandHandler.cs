namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarFaseCanonicaCommand"/> (convention-based Wolverine).
/// Orquestra: construção do agregado por inteiro primeiro (sem I/O — formato,
/// pertença ao conjunto canônico e coerência acumulados no mesmo lote, 422), só
/// então a unicidade do código entre vivos (409), persistência e commit. Protege
/// a corrida check-then-act traduzindo a violação do índice único parcial em
/// <c>CodigoJaExiste</c>.
/// </summary>
public static class CriarFaseCanonicaCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarFaseCanonicaCommand command,
        IFaseCanonicaRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<FaseCanonica> criar = FaseCanonica.Criar(
            command.Codigo,
            command.Nome,
            command.Descricao,
            command.DonoTipico,
            command.AgrupaEtapas,
            command.PermiteComplementacao,
            command.BaseLegal,
            command.ProduzResultado,
            command.ResultadoDefinitivo,
            command.ColetaInscricao,
            command.ColetaSolicitacaoIsencao,
            command.OrigemData);

        if (criar.IsFailure)
        {
            return Result<Guid>.ValidationFailure(criar.Errors);
        }

        FaseCanonica fase = criar.Value!;

        if (await repository.CodigoExisteEntreVivosAsync(fase.Codigo.Valor, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoJaExisteErro());
        }

        await repository.AdicionarAsync(fase, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsCodigoConflict(constraint))
        {
            // Sem descartar, a entidade Added continua rastreada e o SaveChangesAsync
            // automático do Wolverine (AutoApplyTransactions) tenta a mesma inserção de
            // novo FORA deste catch — a mesma violação estoura sem tradução, e o 409
            // pretendido vira 500.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(CodigoJaExisteErro());
        }

        return Result<Guid>.Success(fase.Id);
    }

    private static DomainError CodigoJaExisteErro() =>
        new(FaseCanonicaErrorCodes.CodigoJaExiste,
            "Já existe uma fase canônica viva com o código informado.");
}
