namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

/// <summary>
/// Handler do <see cref="CriarUnidadeCommand"/>. A conferência é de <see cref="ConferenciaDoCadastroDaUnidade"/>
/// (payload inteiro antes de qualquer I/O, unicidade de slug, sigla e código entre as Unidades vivas e
/// existência do superior, nesta ordem) e a gravação, de <see cref="GravacaoDaUnidade"/>, que ao final invalida
/// o cache do reader cross-módulo (ADR-0056).
/// </summary>
public static class CriarUnidadeCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarUnidadeCommand command,
        ConferenciaDoCadastroDaUnidade conferencia,
        GravacaoDaUnidade gravacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(conferencia);
        ArgumentNullException.ThrowIfNull(gravacao);

        Result<Unidade> conferida = await conferencia.ConferirNovaAsync(command, cancellationToken).ConfigureAwait(false);
        return conferida.IsFailure
            ? Result<Guid>.ValidationFailure(conferida.Errors)
            : await gravacao.GravarNovaAsync(conferida.Value!, cancellationToken).ConfigureAwait(false);
    }
}
