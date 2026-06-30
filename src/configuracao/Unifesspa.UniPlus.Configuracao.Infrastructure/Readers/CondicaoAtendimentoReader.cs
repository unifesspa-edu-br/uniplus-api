namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="ICondicaoAtendimentoReader"/> (ADR-0056): leitura
/// direta do banco de Configuração (<c>AsNoTracking</c>, query filter de
/// soft-delete por convenção). Sem cache — o cadastro é de baixo volume e baixa
/// frequência de leitura viva; o congelamento por valor no consumidor (ADR-0061)
/// dispensa releitura quente (mesmo padrão do <c>TipoDocumentoReader</c>).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
internal sealed class CondicaoAtendimentoReader : ICondicaoAtendimentoReader
{
    private readonly ConfiguracaoDbContext _dbContext;

    public CondicaoAtendimentoReader(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CondicaoAtendimentoView>> ListarVivosAsync(
        CancellationToken cancellationToken = default)
    {
        List<CondicaoAtendimentoEspecializado> entidades = await _dbContext.CondicoesAtendimento
            .AsNoTracking()
            .OrderBy(c => c.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. entidades.Select(ParaView)];
    }

    public async Task<CondicaoAtendimentoView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        CondicaoAtendimentoEspecializado? entidade = await _dbContext.CondicoesAtendimento
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entidade is null ? null : ParaView(entidade);
    }

    private static CondicaoAtendimentoView ParaView(CondicaoAtendimentoEspecializado c) =>
        new(
            c.Id,
            c.Codigo.Valor,
            c.Nome);
}
