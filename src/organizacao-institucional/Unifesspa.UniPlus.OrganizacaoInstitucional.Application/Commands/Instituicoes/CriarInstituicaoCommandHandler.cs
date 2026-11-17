namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

/// <summary>
/// Handler do <see cref="CriarInstituicaoCommand"/>. Convention-based para Wolverine — método estático
/// <c>Handle</c> com dependências por parâmetro. A conferência é de <see cref="ConferenciaDoCadastroDaInstituicao"/>
/// (payload, singleton e vínculo com a Unidade raiz, nesta ordem) e a gravação, de <see cref="GravacaoDaInstituicao"/>.
/// </summary>
public static class CriarInstituicaoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarInstituicaoCommand command,
        ConferenciaDoCadastroDaInstituicao conferencia,
        GravacaoDaInstituicao gravacao,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(conferencia);
        ArgumentNullException.ThrowIfNull(gravacao);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<Instituicao> conferida = await conferencia
            .ConferirNovaAsync(command, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return conferida.IsFailure
            ? Result<Guid>.ValidationFailure(conferida.Errors)
            : await gravacao.GravarNovaAsync(conferida.Value!, cancellationToken).ConfigureAwait(false);
    }
}
