namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarFaseCanonicaCommand"/> (convention-based Wolverine).
/// Orquestra: unicidade do código entre vivos (409), construção do agregado
/// (formato + pertença ao conjunto canônico + coerência, 422), persistência e
/// commit. Protege a corrida check-then-act traduzindo a violação do índice único
/// parcial em <c>CodigoJaExiste</c>.
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

        if (await repository.CodigoExisteEntreVivosAsync(command.Codigo, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoJaExisteErro(command.Codigo));
        }

        Result<FaseCanonica> faseResult = FaseCanonica.Criar(
            command.Codigo,
            command.Nome,
            command.Descricao,
            command.DonoTipico,
            command.AgrupaEtapas,
            command.PermiteComplementacao,
            command.BaseLegal);

        if (faseResult.IsFailure)
        {
            return Result<Guid>.Failure(faseResult.Error!);
        }

        FaseCanonica fase = faseResult.Value!;

        await repository.AdicionarAsync(fase, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsCodigoConflict(constraint))
        {
            // Corrida entre CodigoExisteEntreVivosAsync e o INSERT (check-then-act): o
            // índice único parcial dispara 23505 e viramos o mesmo CodigoJaExiste do
            // caminho não-race — 409 consistente. O filtro do `when` garante que
            // outras exceções propagam intactas.
            return Result<Guid>.Failure(CodigoJaExisteErro(command.Codigo));
        }

        return Result<Guid>.Success(fase.Id);
    }

    private static DomainError CodigoJaExisteErro(string codigo) =>
        new(FaseCanonicaErrorCodes.CodigoJaExiste,
            $"Já existe uma fase canônica viva com o código '{codigo}'.");
}
