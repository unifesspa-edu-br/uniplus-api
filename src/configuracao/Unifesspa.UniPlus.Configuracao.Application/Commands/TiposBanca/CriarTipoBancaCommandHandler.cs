namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposBanca;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarTipoBancaCommand"/> (convention-based Wolverine).
/// Valida o agregado por inteiro primeiro (sem I/O) — código, nome, fase típica e
/// descrição acumulam no mesmo lote — só depois consulta a unicidade do código
/// entre vivos (409): evita consultar o repositório com um comando que já se sabe
/// inválido por outro motivo, e evita mascarar outras violações atrás de um
/// CodigoJaExiste que chegaria primeiro. Protege a corrida check-then-act
/// traduzindo a violação do índice único parcial em <c>CodigoJaExiste</c>.
/// </summary>
public static class CriarTipoBancaCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarTipoBancaCommand command,
        ITipoBancaRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<TipoBanca> conferida = await ConferirAsync(command, repository, cancellationToken).ConfigureAwait(false);

        return conferida.IsFailure
            ? Result<Guid>.ValidationFailure(conferida.Errors)
            : await GravarAsync(conferida.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<TipoBanca>> ConferirAsync(
        CriarTipoBancaCommand command,
        ITipoBancaRepository repository,
        CancellationToken cancellationToken)
    {
        Result<TipoBanca> bancaResult = TipoBanca.Criar(command.Codigo, command.Declaracao());

        // Unicidade consultada só depois de o agregado passar em todas as regras de campo.
        return bancaResult.IsFailure || !await CodigoJaCadastradoAsync(bancaResult.Value!, repository, cancellationToken).ConfigureAwait(false)
            ? bancaResult
            : Result<TipoBanca>.Failure(CodigoJaExisteErro());
    }

    private static Task<bool> CodigoJaCadastradoAsync(
        TipoBanca banca,
        ITipoBancaRepository repository,
        CancellationToken cancellationToken) =>
        repository.CodigoExisteEntreVivosAsync(banca.Codigo, null, cancellationToken);

    private static async Task<Result<Guid>> GravarAsync(
        TipoBanca banca,
        ITipoBancaRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        await repository.AdicionarAsync(banca, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ViolaOIndiceUnicoDoCodigo(ex))
        {
            return Result<Guid>.Failure(CodigoJaExisteErro());
        }

        return Result<Guid>.Success(banca.Id);
    }

    // Corrida entre CodigoExisteEntreVivosAsync e o INSERT (check-then-act): o
    // índice único parcial dispara 23505 e viramos o mesmo CodigoJaExiste do
    // caminho não-race — 409 consistente.
    private static bool ViolaOIndiceUnicoDoCodigo(Exception ex) =>
        UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
        && UniqueConstraintViolation.IsCodigoConflict(constraint);

    private static DomainError CodigoJaExisteErro() =>
        new(TipoBancaErrorCodes.CodigoJaExiste,
            "Já existe um tipo de banca vivo com o código informado.");
}
