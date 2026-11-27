namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

/// <summary>
/// Handler do <see cref="AtualizarUnidadeCommand"/>. A conferência é de <see cref="ConferenciaDoCadastroDaUnidade"/>:
/// valida o payload por inteiro antes de qualquer I/O (validação sempre vence 404) e só então busca a Unidade,
/// confere unicidade e hierarquia. A gravação é de <see cref="GravacaoDaUnidade"/>, que registra a mudança de
/// identificadores no dia civil do fuso da instituição; se esse fuso não é reconhecido, nada é gravado.
/// </summary>
public static class AtualizarUnidadeCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarUnidadeCommand command,
        ConferenciaDoCadastroDaUnidade conferencia,
        GravacaoDaUnidade gravacao,
        IResolvedorFusoInstitucional fuso,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(conferencia);
        ArgumentNullException.ThrowIfNull(gravacao);
        ArgumentNullException.ThrowIfNull(fuso);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<Unidade> conferida = await conferencia.ConferirAtualizacaoAsync(command, cancellationToken).ConfigureAwait(false);

        return conferida.IsFailure
            ? Result.ValidationFailure(conferida.Errors)
            : await GravarNoDiaCivilAsync(conferida.Value!, command, gravacao, fuso.DiaCivilDe(timeProvider.GetUtcNow()), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> GravarNoDiaCivilAsync(
        Unidade unidade,
        AtualizarUnidadeCommand command,
        GravacaoDaUnidade gravacao,
        Result<DateOnly> dia,
        CancellationToken cancellationToken) =>
        dia.IsFailure
            ? Result.Failure(dia.Error!)
            : await gravacao.GravarAtualizacaoAsync(unidade, command, dia.Value, cancellationToken).ConfigureAwait(false);
}
