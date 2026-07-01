namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposBanca;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarTipoBancaCommand"/> (convention-based Wolverine).
/// Orquestra: unicidade do código entre vivos (409), construção do agregado
/// (formato + pertença ao conjunto canônico, 422), persistência e commit. Protege
/// a corrida check-then-act traduzindo a violação do índice único parcial em
/// <c>CodigoJaExiste</c>.
/// </summary>
public static class CriarTipoBancaCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarTipoBancaCommand command,
        ITipoBancaRepository repository,
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

        Result<TipoBanca> bancaResult = TipoBanca.Criar(
            command.Codigo,
            command.Nome,
            command.FaseTipica,
            command.Descricao);

        if (bancaResult.IsFailure)
        {
            return Result<Guid>.Failure(bancaResult.Error!);
        }

        TipoBanca banca = bancaResult.Value!;

        await repository.AdicionarAsync(banca, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsCodigoConflict(constraint))
        {
            // Corrida entre CodigoExisteEntreVivosAsync e o INSERT (check-then-act): o
            // índice único parcial dispara 23505 e viramos o mesmo CodigoJaExiste do
            // caminho não-race — 409 consistente.
            return Result<Guid>.Failure(CodigoJaExisteErro(command.Codigo));
        }

        return Result<Guid>.Success(banca.Id);
    }

    private static DomainError CodigoJaExisteErro(string codigo) =>
        new(TipoBancaErrorCodes.CodigoJaExiste,
            $"Já existe um tipo de banca vivo com o código '{codigo}'.");
}
