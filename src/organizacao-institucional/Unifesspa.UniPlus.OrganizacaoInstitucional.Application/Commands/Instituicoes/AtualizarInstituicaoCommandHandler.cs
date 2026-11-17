namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

/// <summary>
/// Handler do <see cref="AtualizarInstituicaoCommand"/>. A conferência é de <see cref="ConferenciaDoCadastroDaInstituicao"/>:
/// valida antes de I/O só o que é determinável sem o registro persistido e só então busca a Instituição por Id
/// (validação sempre vence 404). A gravação é de <see cref="GravacaoDaInstituicao"/>.
/// </summary>
public static class AtualizarInstituicaoCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarInstituicaoCommand command,
        ConferenciaDoCadastroDaInstituicao conferencia,
        GravacaoDaInstituicao gravacao,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(conferencia);
        ArgumentNullException.ThrowIfNull(gravacao);
        ArgumentNullException.ThrowIfNull(timeProvider);

        DateTimeOffset agora = timeProvider.GetUtcNow();
        Result<Instituicao> conferida = await conferencia.ConferirAtualizacaoAsync(command, agora, cancellationToken).ConfigureAwait(false);
        return conferida.IsFailure
            ? Result.ValidationFailure(conferida.Errors)
            : await gravacao.GravarAtualizacaoAsync(conferida.Value!, command, agora, cancellationToken).ConfigureAwait(false);
    }
}
