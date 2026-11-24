namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Persistence;
using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarTermoConsentimentoCommand"/> (convention-based
/// Wolverine): constrói o agregado (invariantes de campo e código conferidos no próprio
/// <c>TermoConsentimento.Criar</c>), confere a unicidade do código, persiste e commita.
/// </summary>
public static class CriarTermoConsentimentoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarTermoConsentimentoCommand command,
        ITermoConsentimentoRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<TermoConsentimento> termoResult = TermoConsentimento.Criar(
            command.Nome, command.TextoRascunho, command.BaseLegalRascunho, command.FormaAceiteRascunho);

        return termoResult.IsFailure
            ? Result<Guid>.ValidationFailure(termoResult.Errors)
            : await GravarAsync(termoResult.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    // O código é único entre os termos vivos: a conferência prévia dá a recusa comum e o índice único decide a corrida.
    private static async Task<Result<Guid>> GravarAsync(
        TermoConsentimento termo,
        ITermoConsentimentoRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (await repository.CodigoExisteAsync(termo.Codigo, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoJaExiste(termo));
        }

        await repository.AdicionarAsync(termo, cancellationToken).ConfigureAwait(false);

        return await SalvarAsync(termo, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<Guid>> SalvarAsync(TermoConsentimento termo, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result<Guid>.Success(termo.Id);
        }
        catch (Exception exception) when (ConstraintVioladaNaGravacao.NomeDaConstraintViolada(exception)
            == RestricoesDoBanco.TermoDeConsentimentoCodigoAtivos)
        {
            // Sem descartar, a inserção continua rastreada e o SaveChanges do outbox a repete fora deste catch (500 em vez de 409).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(CodigoJaExiste(termo));
        }
    }

    private static DomainError CodigoJaExiste(TermoConsentimento termo) => new(
        TermoConsentimentoErrorCodes.CodigoJaExiste,
        $"Já existe um termo de consentimento com o código {termo.Codigo.Valor}: um nome equivalente já está cadastrado.");
}
