namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

/// <summary>
/// Registro só acrescentado das manifestações técnicas na proteção do catálogo de tipos de documento e de fatos do
/// candidato (ADR-0163).
/// </summary>
public sealed class ManifestacaoDaProtecaoRepository : IManifestacaoDaProtecaoRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public ManifestacaoDaProtecaoRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public void Adicionar(ManifestacaoDaProtecaoDoCatalogo manifestacao)
    {
        ArgumentNullException.ThrowIfNull(manifestacao);
        _dbContext.ManifestacoesDaProtecaoDoCatalogo.Add(manifestacao);
    }

    public async Task<IReadOnlyList<ManifestacaoDaProtecaoDoCatalogo>> ListarDoCicloAsync(
        ItemDaProtecao item, CicloDaAprovacao ciclo, CancellationToken cancellationToken) =>
        await DoItem(item)
            .Where(m => m.Ciclo == ciclo)
            .OrderBy(m => m.Instante)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ManifestacaoDaProtecaoDoCatalogo>> ListarDoItemAsync(ItemDaProtecao item, CancellationToken cancellationToken) =>
        await DoItem(item)
            .OrderBy(m => m.Ciclo)
            .ThenBy(m => m.Instante)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private IQueryable<ManifestacaoDaProtecaoDoCatalogo> DoItem(ItemDaProtecao item)
    {
        CadastroDoCatalogo cadastro = item.Cadastro;
        Guid itemId = item.ItemId;
        return _dbContext.ManifestacoesDaProtecaoDoCatalogo
            .AsNoTracking()
            .Where(m => EF.Property<CadastroDoCatalogo>(m, ManifestacaoDaProtecaoDoCatalogoConfiguration.CampoDoCadastro) == cadastro
                && EF.Property<Guid>(m, ManifestacaoDaProtecaoDoCatalogoConfiguration.CampoDoItem) == itemId);
    }
}
