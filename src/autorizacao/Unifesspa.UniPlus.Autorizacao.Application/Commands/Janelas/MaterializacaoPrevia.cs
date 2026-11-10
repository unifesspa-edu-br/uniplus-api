namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>
/// Materializa o fim da janela de migração que já não está efetivamente aberta antes de uma operação que depende do
/// estado físico das concessões: sem isso, a concessão antiga vencida pelo prazo continuaria não revogada e a concessão
/// nova do mesmo beneficiário esbarraria nela. Roda em transação própria, num escopo novo, antes de a operação tomar as
/// travas dela, e só quando uma leitura sem trava indica que há o que materializar.
/// </summary>
public sealed class MaterializacaoPrevia
{
    private readonly IJanelaDeMigracaoRepository _janelas;
    private readonly IServiceScopeFactory _escopos;
    private readonly TimeProvider _clock;

    public MaterializacaoPrevia(IJanelaDeMigracaoRepository janelas, IServiceScopeFactory escopos, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(janelas);
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(clock);

        _janelas = janelas;
        _escopos = escopos;
        _clock = clock;
    }

    public async Task GarantirAsync(Guid perfilId, CancellationToken cancellationToken)
    {
        if (!await _janelas.TemJanelaPorMaterializarAsync(perfilId, _clock.GetUtcNow(), cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        AsyncServiceScope escopo = _escopos.CreateAsyncScope();
        await using (escopo.ConfigureAwait(false))
        {
            await escopo.ServiceProvider.GetRequiredService<ICommandBus>()
                .Send(new MaterializarJanelaVencidaCommand(perfilId), cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
