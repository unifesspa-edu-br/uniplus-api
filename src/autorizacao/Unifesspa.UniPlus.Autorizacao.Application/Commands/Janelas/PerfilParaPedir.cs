namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O perfil a pedir, lido com <c>FOR SHARE</c> até o fim da transação: a troca de vigência, que o trava para escrita,
/// espera ou conflita. O perfil em janela de migração não recebe pedido novo, e a janela vencida que ninguém materializou
/// ainda responde conflito, para que a nova tentativa a materialize antes.
/// </summary>
public sealed class PerfilParaPedir
{
    private readonly IConcessaoRepository _concessoes;
    private readonly IJanelaDeMigracaoRepository _janelas;
    private readonly TimeProvider _clock;

    public PerfilParaPedir(IConcessaoRepository concessoes, IJanelaDeMigracaoRepository janelas, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(janelas);
        ArgumentNullException.ThrowIfNull(clock);

        _concessoes = concessoes;
        _janelas = janelas;
        _clock = clock;
    }

    public async Task<Result<Perfil>> ObterAsync(Guid perfilId, CancellationToken cancellationToken)
    {
        Perfil? perfil = await _concessoes.ObterParaConcederAsync(perfilId, cancellationToken).ConfigureAwait(false);
        if (perfil is null)
        {
            return Result<Perfil>.Failure(new DomainError(PerfilErrorCodes.NaoEncontrado, $"Perfil de acesso {perfilId} não encontrado."));
        }

        DateTimeOffset agora = _clock.GetUtcNow();
        if (await _janelas.TemJanelaAbertaAsync(perfilId, agora, cancellationToken).ConfigureAwait(false))
        {
            return Result<Perfil>.Failure(MigracaoErros.PedidoEmPerfilEmMigracao());
        }

        return await _janelas.TemJanelaPorMaterializarAsync(perfilId, agora, cancellationToken).ConfigureAwait(false)
            ? Result<Perfil>.Failure(MigracaoErros.JanelaEmEncerramento())
            : Result<Perfil>.Success(perfil);
    }
}
