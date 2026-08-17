namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarTermoConsentimentoCommand"/> (convention-based
/// Wolverine): constrói o agregado (invariantes de campo conferidas no próprio
/// <c>TermoConsentimento.Criar</c>), persiste e commita.
/// </summary>
public static class CriarTermoConsentimentoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarTermoConsentimentoCommand command,
        ITermoConsentimentoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<TermoConsentimento> termoResult = TermoConsentimento.Criar(
            command.Nome, command.TextoRascunho, command.BaseLegalRascunho, command.FormaAceiteRascunho);

        if (termoResult.IsFailure)
        {
            return Result<Guid>.ValidationFailure(termoResult.Errors);
        }

        TermoConsentimento termo = termoResult.Value!;
        await repository.AdicionarAsync(termo, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(termo.Id);
    }
}
