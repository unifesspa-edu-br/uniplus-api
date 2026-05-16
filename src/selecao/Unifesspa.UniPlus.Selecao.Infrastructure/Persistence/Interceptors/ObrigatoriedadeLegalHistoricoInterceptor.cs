namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Interceptors;

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Intercepta <c>SavingChanges</c> do <see cref="SelecaoDbContext"/> para
/// gravar uma linha em <c>obrigatoriedade_legal_historico</c> a cada mutação
/// de <see cref="ObrigatoriedadeLegal"/> (CA-06 da Story #460). Roda dentro
/// da mesma transação do save da regra — invariante atômica do histórico.
/// </summary>
/// <remarks>
/// <para>
/// Recomputa o <see cref="ObrigatoriedadeLegal.Hash"/> antes de capturar o
/// snapshot — defesa em profundidade contra mutações via reflection,
/// hidratação EF fora-de-factory ou seeds que não passaram por
/// <c>ObrigatoriedadeLegal.Criar</c>/<c>Atualizar</c>.
/// </para>
/// <para>
/// Registrado como <c>Scoped</c> na DI do módulo (adjacente aos interceptors
/// canônicos <c>SoftDeleteInterceptor</c> e <c>AuditableInterceptor</c>) por
/// depender de <see cref="IUserContext"/> Scoped para preencher
/// <c>snapshot_by</c>. Singleton causaria captive dependency, congelando o
/// usuário no primeiro request servido pelo processo.
/// </para>
/// <para>
/// O snapshot JSON é o MESMO payload canônico computado por
/// <see cref="HashCanonicalComputer"/> + os campos de governança
/// (<c>proprietario</c>, <c>areasDeInteresse</c>) e id da regra — assim a
/// reconstituição forense não depende de juntar tabelas: cada linha do
/// histórico é completa.
/// </para>
/// </remarks>
public sealed class ObrigatoriedadeLegalHistoricoInterceptor : SaveChangesInterceptor
{
    private const string SystemUser = "system";

    private readonly IUserContext? _userContext;

    public ObrigatoriedadeLegalHistoricoInterceptor(IUserContext? userContext = null)
    {
        _userContext = userContext;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData?.Context is not null)
        {
            CapturarHistorico(eventData.Context);
        }

        return base.SavingChanges(eventData!, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData?.Context is not null)
        {
            CapturarHistorico(eventData.Context);
        }

        return base.SavingChangesAsync(eventData!, result, cancellationToken);
    }

    private void CapturarHistorico(DbContext context)
    {
        DateTimeOffset snapshotAt = DateTimeOffset.UtcNow;
        string snapshotBy = ResolveSnapshotBy();

        // Snapshot ANTES de mutar o ChangeTracker — adicionar entries
        // durante a iteração pode invalidar o enumerador.
        List<EntityEntry<ObrigatoriedadeLegal>> entries = [];
        foreach (EntityEntry<ObrigatoriedadeLegal> entry in context
            .ChangeTracker
            .Entries<ObrigatoriedadeLegal>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entries.Add(entry);
            }
        }

        if (entries.Count == 0)
        {
            return;
        }

        DbSet<ObrigatoriedadeLegalHistorico> historicoSet =
            context.Set<ObrigatoriedadeLegalHistorico>();

        foreach (EntityEntry<ObrigatoriedadeLegal> entry in entries)
        {
            ObrigatoriedadeLegal regra = entry.Entity;
            string hash = regra.RecomputeHash();

            // EF Core não reflete writes feitos no entity em propriedades já
            // marcadas como Modified sem um novo DetectChanges. Para garantir
            // que o save persiste o hash recomputado, atualiza o current value
            // da property no entry explicitamente.
            entry.Property(static r => r.Hash).CurrentValue = hash;
            entry.Property(static r => r.Hash).IsModified = true;

            HashSet<AreaCodigo> areasParaSnapshot = ResolverAreasParaSnapshot(
                context,
                regra,
                entry.State);

            string conteudoJson = SerializarConteudoCanonical(regra, hash, areasParaSnapshot);
            historicoSet.Add(ObrigatoriedadeLegalHistorico.Snapshot(
                regraId: regra.Id,
                conteudoJson: conteudoJson,
                hash: hash,
                snapshotAt: snapshotAt,
                snapshotBy: snapshotBy));
        }
    }

    /// <summary>
    /// Resolve o conjunto de áreas a serialização no snapshot. Em
    /// <see cref="EntityState.Added"/> a regra acabou de ser hidratada via
    /// factory e o set in-memory é a fonte da verdade — o repositório
    /// ainda vai persistir as junctions correspondentes neste mesmo save
    /// (Story #461). Em <see cref="EntityState.Modified"/>, entretanto, o
    /// EF carregou a regra sem nav property para a junction (ADR-0060) e
    /// o set in-memory pode estar vazio mesmo quando há bindings vigentes
    /// — recorremos à consulta direta da tabela para preservar a evidência
    /// forense de governance.
    /// </summary>
    private static HashSet<AreaCodigo> ResolverAreasParaSnapshot(
        DbContext context,
        ObrigatoriedadeLegal regra,
        EntityState state)
    {
        if (state == EntityState.Added)
        {
            return [.. regra.AreasDeInteresse];
        }

        // Em Modified (inclui soft-delete via SoftDeleteInterceptor), buscar
        // os bindings vigentes da junction. Como o save ainda não comitou,
        // alterações pendentes em AreaDeInteresseBinding no mesmo SaveChanges
        // já estão no ChangeTracker — combinamos: tracked entries (Added/
        // Modified) sobrescrevem os valid_to=null persistidos.
        Guid regraId = regra.Id;

        IEnumerable<AreaCodigo> trackedAdded = context.ChangeTracker
            .Entries<AreaDeInteresseBinding<ObrigatoriedadeLegal>>()
            .Where(e => e.State is EntityState.Added or EntityState.Unchanged or EntityState.Modified
                       && e.Entity.ParentId == regraId
                       && e.Entity.ValidoAte is null)
            .Select(e => e.Entity.AreaCodigo);

        IEnumerable<AreaCodigo> trackedRemoved = context.ChangeTracker
            .Entries<AreaDeInteresseBinding<ObrigatoriedadeLegal>>()
            .Where(e => e.State == EntityState.Deleted
                       && e.Entity.ParentId == regraId)
            .Select(e => e.Entity.AreaCodigo)
            .ToHashSet();

        HashSet<AreaCodigo> trackedIds = [..
            context.ChangeTracker
                .Entries<AreaDeInteresseBinding<ObrigatoriedadeLegal>>()
                .Where(e => e.Entity.ParentId == regraId)
                .Select(e => e.Entity.AreaCodigo)];

        // Linhas persistidas vigentes que o ChangeTracker não conhece —
        // bindings antigos preservados na junction sem mutação nesta save.
        IEnumerable<AreaCodigo> persistedVigentes = context
            .Set<AreaDeInteresseBinding<ObrigatoriedadeLegal>>()
            .AsNoTracking()
            .Where(b => b.ParentId == regraId && b.ValidoAte == null)
            .Select(b => b.AreaCodigo)
            .ToList()
            .Where(area => !trackedIds.Contains(area));

        HashSet<AreaCodigo> resolvidas = [.. trackedAdded, .. persistedVigentes];
        resolvidas.ExceptWith(trackedRemoved);
        return resolvidas;
    }

    private string ResolveSnapshotBy()
    {
        if (_userContext is { IsAuthenticated: true, UserId: { Length: > 0 } userId })
        {
            return userId;
        }

        return SystemUser;
    }

    /// <summary>
    /// Serializa o snapshot canônico da regra (campos semânticos + governance
    /// + id + hash). Reusa <see cref="HashCanonicalComputer.CanonicalOptions"/>
    /// e <see cref="HashCanonicalComputer.CanonicalizeRecursive"/> — invariante
    /// load-bearing: o JSON persistido em <c>obrigatoriedade_legal_historico.conteudo_jsonb</c>
    /// é byte-equivalente ao payload que alimentou o hash, garantindo
    /// reprodutibilidade forense ("recomputar hash a partir do snapshot
    /// retorna o mesmo valor").
    /// </summary>
    private static string SerializarConteudoCanonical(
        ObrigatoriedadeLegal regra,
        string hash,
        IReadOnlyCollection<AreaCodigo> areasDeInteresse)
    {
        JsonNode? predicadoNode = JsonSerializer.SerializeToNode(
            regra.Predicado,
            HashCanonicalComputer.CanonicalOptions);

        JsonArray areas = [];
        foreach (AreaCodigo area in areasDeInteresse.OrderBy(static a => a.Value, StringComparer.Ordinal))
        {
            areas.Add(area.Value);
        }

        JsonObject payload = new()
        {
            ["id"] = regra.Id,
            ["tipoEditalCodigo"] = regra.TipoEditalCodigo,
            ["categoria"] = regra.Categoria.ToString(),
            ["regraCodigo"] = regra.RegraCodigo,
            ["predicado"] = predicadoNode,
            ["descricaoHumana"] = regra.DescricaoHumana,
            ["baseLegal"] = regra.BaseLegal,
            ["atoNormativoUrl"] = regra.AtoNormativoUrl,
            ["portariaInternaCodigo"] = regra.PortariaInternaCodigo,
            ["vigenciaInicio"] = regra.VigenciaInicio.ToString("O", CultureInfo.InvariantCulture),
            ["vigenciaFim"] = regra.VigenciaFim?.ToString("O", CultureInfo.InvariantCulture),
            ["hash"] = hash,
            ["proprietario"] = regra.Proprietario?.Value,
            ["areasDeInteresse"] = areas,
            ["isDeleted"] = regra.IsDeleted,
        };

        JsonNode canonical = HashCanonicalComputer.CanonicalizeRecursive(payload);
        return canonical.ToJsonString();
    }
}
