namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

/// <summary>
/// Handler do <see cref="RemoverUnidadeCommand"/>. Executa soft-delete via
/// <c>SoftDeleteInterceptor</c> — bloqueia se a unidade for superior de outra
/// unidade viva, ou se for a raiz (reitoria) de uma Instituição viva.
/// </summary>
/// <remarks>
/// O bloqueio por <c>instituicoes.reitoria_id</c> preserva a integridade do
/// vínculo da Instituição com a reitoria: uma Unidade referenciada
/// como raiz não pode ser removida enquanto a Instituição viva apontar para ela.
/// A conferência é de <see cref="ConferenciaDoCadastroDaUnidade"/> e a gravação, de <see cref="GravacaoDaUnidade"/>.
/// </remarks>
public static class RemoverUnidadeCommandHandler
{
    public static async Task<Result> Handle(
        RemoverUnidadeCommand command,
        ConferenciaDoCadastroDaUnidade conferencia,
        GravacaoDaUnidade gravacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(conferencia);
        ArgumentNullException.ThrowIfNull(gravacao);

        Result<Unidade> conferida = await conferencia.ConferirRemocaoAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (conferida.IsFailure)
        {
            return Result.ValidationFailure(conferida.Errors);
        }

        await gravacao.GravarRemocaoAsync(conferida.Value!, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
