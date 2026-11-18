namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O perfil a pedir, lido com <c>FOR SHARE</c> até o fim da transação: a troca de vigência, que o trava para escrita,
/// espera ou conflita. O perfil em revisão das concessões não recebe pedido novo, e a revisão vencida que ninguém materializou
/// ainda responde conflito, para que a nova tentativa a materialize antes.
/// </summary>
public sealed class PerfilParaPedir
{
    private readonly IConcessaoRepository _concessoes;
    private readonly IRevisaoDasConcessoesRepository _revisoes;
    private readonly TimeProvider _clock;

    public PerfilParaPedir(IConcessaoRepository concessoes, IRevisaoDasConcessoesRepository revisoes, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(revisoes);
        ArgumentNullException.ThrowIfNull(clock);

        _concessoes = concessoes;
        _revisoes = revisoes;
        _clock = clock;
    }

    public async Task<Result<Perfil>> ObterAsync(Guid perfilId, CancellationToken cancellationToken)
    {
        Perfil? perfil = await _concessoes.ObterParaConcederAsync(perfilId, cancellationToken).ConfigureAwait(false);
        if (perfil is null)
        {
            return Result<Perfil>.Failure(new DomainError(PerfilErrorCodes.NaoEncontrado, $"Perfil de acesso {perfilId} não encontrado."));
        }

        DomainError? recusa = await RecusaDaRevisaoAsync(perfilId, cancellationToken).ConfigureAwait(false);

        return recusa is null ? Result<Perfil>.Success(perfil) : Result<Perfil>.Failure(recusa);
    }

    private async Task<DomainError?> RecusaDaRevisaoAsync(Guid perfilId, CancellationToken cancellationToken)
    {
        DateTimeOffset agora = _clock.GetUtcNow();
        if (await _revisoes.TemRevisaoAbertaAsync(perfilId, agora, cancellationToken).ConfigureAwait(false))
        {
            return RevisaoDasConcessoesErros.PedidoEmPerfilEmMigracao();
        }

        return await _revisoes.TemRevisaoPorEncerrarAsync(perfilId, agora, cancellationToken).ConfigureAwait(false)
            ? RevisaoDasConcessoesErros.RevisaoEmEncerramento()
            : null;
    }
}
