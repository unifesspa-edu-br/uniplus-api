namespace Unifesspa.UniPlus.Selecao.Application.Services.Inscricoes;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>Prepara a inscrição nova na versão divulgada, dentro do período publicado.</summary>
public sealed class InicioDaInscricao(
    IInscricaoRepository inscricoes,
    ICertameDivulgadoRepository certames,
    IProcessoSeletivoRepository processos,
    PeriodoDaInscricaoResolver periodo,
    IUserContext usuario,
    TimeProvider timeProvider)
{
    public async Task<Result<Inscricao>> IniciarAsync(Guid processoId, Cpf cpf, CancellationToken cancellationToken)
    {
        Result<VersaoDivulgada> divulgada = await VersaoDivulgadaResolver.ResolverAsync(processoId, certames, processos, cancellationToken).ConfigureAwait(false);
        if (divulgada.IsFailure)
        {
            return Result<Inscricao>.Failure(divulgada.Error!);
        }
        return periodo.Conferir(divulgada.Value!.Versao, timeProvider.GetUtcNow())?.Recusa is { } recusa
            ? Result<Inscricao>.Failure(recusa)
            : await CriarAsync(processoId, cpf, divulgada.Value!.Versao.Id, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<Inscricao>> CriarAsync(Guid processoId, Cpf cpf, Guid versaoId, CancellationToken cancellationToken)
    {
        Result<Inscricao> criada = Inscricao.Iniciar(processoId, cpf, usuario.UserId ?? string.Empty, usuario.Issuer ?? string.Empty, versaoId);
        if (criada.IsSuccess)
        {
            await inscricoes.AdicionarAsync(criada.Value!, cancellationToken).ConfigureAwait(false);
        }
        return criada;
    }
}
