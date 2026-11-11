namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Domain.Entities;
using Domain.Interfaces;

/// <summary>
/// A leitura dos motivos escolhidos pelo processo para as transições que congelam versão: fica na
/// porta do processo porque é parte da configuração que ela congela, e reusa a persistência própria
/// dos motivos escolhidos.
/// </summary>
public sealed partial class ProcessoSeletivoRepository
{
    public Task<IReadOnlyList<MotivoEscolhidoComOrigem>> ListarMotivosEscolhidosAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default) =>
        new MotivoDoProcessoRepository(_context).ListarComOrigemAsync(processoSeletivoId, cancellationToken);

    public async Task DescartarMotivosPendentesAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default)
    {
        MotivoDoProcessoRepository motivos = new(_context);
        IReadOnlyList<MotivoDoProcesso> escolhidos = await motivos.ListarDoProcessoAsync(processoSeletivoId, cancellationToken).ConfigureAwait(false);
        foreach (MotivoDoProcesso pendente in escolhidos.Where(static e => !e.Congelado))
        {
            motivos.Remover(pendente);
        }
    }
}
