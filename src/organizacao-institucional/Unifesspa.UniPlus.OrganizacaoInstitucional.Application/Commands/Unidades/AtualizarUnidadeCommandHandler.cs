namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

/// <summary>
/// Handler do <see cref="AtualizarUnidadeCommand"/>. A conferência é de <see cref="ConferenciaDoCadastroDaUnidade"/>:
/// valida o payload por inteiro antes de qualquer I/O (validação sempre vence 404) e só então busca a Unidade,
/// confere unicidade e hierarquia. A gravação é de <see cref="GravacaoDaUnidade"/>.
/// </summary>
public static class AtualizarUnidadeCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarUnidadeCommand command,
        ConferenciaDoCadastroDaUnidade conferencia,
        GravacaoDaUnidade gravacao,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(conferencia);
        ArgumentNullException.ThrowIfNull(gravacao);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<Unidade> conferida = await conferencia.ConferirAtualizacaoAsync(command, cancellationToken).ConfigureAwait(false);
        return conferida.IsFailure
            ? Result.ValidationFailure(conferida.Errors)
            : await gravacao.GravarAtualizacaoAsync(conferida.Value!, command, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
    }
}
