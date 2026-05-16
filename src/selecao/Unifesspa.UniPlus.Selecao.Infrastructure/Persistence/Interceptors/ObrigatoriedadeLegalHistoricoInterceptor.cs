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

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData?.Context is not null)
        {
            await CapturarHistoricoAsync(eventData.Context, cancellationToken).ConfigureAwait(false);
        }

        return await base.SavingChangesAsync(eventData!, result, cancellationToken).ConfigureAwait(false);
    }

    private void CapturarHistorico(DbContext context)
    {
        DateTimeOffset snapshotAt = DateTimeOffset.UtcNow;
        string snapshotBy = ResolveSnapshotBy();

        List<EntityEntry<ObrigatoriedadeLegal>> entries = CapturarEntries(context);
        if (entries.Count == 0)
        {
            return;
        }

        DbSet<ObrigatoriedadeLegalHistorico> historicoSet =
            context.Set<ObrigatoriedadeLegalHistorico>();

        foreach (EntityEntry<ObrigatoriedadeLegal> entry in entries)
        {
            HashSet<AreaCodigo> areasParaSnapshot = ResolverAreasParaSnapshot(
                context,
                entry.Entity,
                entry.State);

            historicoSet.Add(CriarSnapshot(entry, areasParaSnapshot, snapshotAt, snapshotBy));
        }
    }

    private async ValueTask CapturarHistoricoAsync(DbContext context, CancellationToken cancellationToken)
    {
        DateTimeOffset snapshotAt = DateTimeOffset.UtcNow;
        string snapshotBy = ResolveSnapshotBy();

        List<EntityEntry<ObrigatoriedadeLegal>> entries = CapturarEntries(context);
        if (entries.Count == 0)
        {
            return;
        }

        DbSet<ObrigatoriedadeLegalHistorico> historicoSet =
            context.Set<ObrigatoriedadeLegalHistorico>();

        foreach (EntityEntry<ObrigatoriedadeLegal> entry in entries)
        {
            HashSet<AreaCodigo> areasParaSnapshot = await ResolverAreasParaSnapshotAsync(
                context,
                entry.Entity,
                entry.State,
                cancellationToken).ConfigureAwait(false);

            historicoSet.Add(CriarSnapshot(entry, areasParaSnapshot, snapshotAt, snapshotBy));
        }
    }

    // Snapshot ANTES de mutar o ChangeTracker — adicionar entries durante a
    // iteração pode invalidar o enumerador. Retorna lista materializada para
    // que o caller (sync ou async) consuma sem reentrar no tracker.
    private static List<EntityEntry<ObrigatoriedadeLegal>> CapturarEntries(DbContext context) =>
        [.. context
            .ChangeTracker
            .Entries<ObrigatoriedadeLegal>()
            .Where(static entry => entry.State is EntityState.Added or EntityState.Modified)];

    private static ObrigatoriedadeLegalHistorico CriarSnapshot(
        EntityEntry<ObrigatoriedadeLegal> entry,
        HashSet<AreaCodigo> areasParaSnapshot,
        DateTimeOffset snapshotAt,
        string snapshotBy)
    {
        ObrigatoriedadeLegal regra = entry.Entity;
        string hash = regra.RecomputeHash();

        // EF Core não reflete writes feitos no entity em propriedades já
        // marcadas como Modified sem um novo DetectChanges. Para garantir
        // que o save persiste o hash recomputado, atualiza o current value
        // da property no entry explicitamente. Só faz sentido em Modified
        // — Added insere todas as colunas, IsModified é no-op.
        if (entry.State == EntityState.Modified)
        {
            entry.Property(static r => r.Hash).CurrentValue = hash;
            entry.Property(static r => r.Hash).IsModified = true;
        }

        string conteudoJson = SerializarConteudoCanonical(regra, hash, areasParaSnapshot);
        return ObrigatoriedadeLegalHistorico.Snapshot(
            regraId: regra.Id,
            conteudoJson: conteudoJson,
            hash: hash,
            snapshotAt: snapshotAt,
            snapshotBy: snapshotBy);
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

        ResolvedBindings bindings = ResolverBindingsRastreados(context, regra.Id);

        List<AreaCodigo> persistedVigentes = context
            .Set<AreaDeInteresseBinding<ObrigatoriedadeLegal>>()
            .AsNoTracking()
            .Where(b => b.ParentId == regra.Id && b.ValidoAte == null)
            .Select(b => b.AreaCodigo)
            .ToList();

        return ComporResultado(bindings, persistedVigentes);
    }

    private static async ValueTask<HashSet<AreaCodigo>> ResolverAreasParaSnapshotAsync(
        DbContext context,
        ObrigatoriedadeLegal regra,
        EntityState state,
        CancellationToken cancellationToken)
    {
        if (state == EntityState.Added)
        {
            return [.. regra.AreasDeInteresse];
        }

        ResolvedBindings bindings = ResolverBindingsRastreados(context, regra.Id);

        List<AreaCodigo> persistedVigentes = await context
            .Set<AreaDeInteresseBinding<ObrigatoriedadeLegal>>()
            .AsNoTracking()
            .Where(b => b.ParentId == regra.Id && b.ValidoAte == null)
            .Select(b => b.AreaCodigo)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return ComporResultado(bindings, persistedVigentes);
    }

    /// <summary>
    /// Passada única sobre o <c>ChangeTracker</c> classificando os bindings
    /// rastreados em "adicionados/vigentes" e "removidos". Combinada com os
    /// persistidos via <see cref="ComporResultado"/>: tracked entries têm
    /// precedência sobre <c>valid_to=null</c> persistidos (Added/Modified
    /// sobrescrevem; Deleted invalida).
    /// </summary>
    private static ResolvedBindings ResolverBindingsRastreados(DbContext context, Guid regraId)
    {
        HashSet<AreaCodigo> added = [];
        HashSet<AreaCodigo> removed = [];
        HashSet<AreaCodigo> known = [];

        foreach (EntityEntry<AreaDeInteresseBinding<ObrigatoriedadeLegal>> e in context
            .ChangeTracker
            .Entries<AreaDeInteresseBinding<ObrigatoriedadeLegal>>())
        {
            if (e.Entity.ParentId != regraId)
            {
                continue;
            }

            known.Add(e.Entity.AreaCodigo);

            if (e.State == EntityState.Deleted)
            {
                removed.Add(e.Entity.AreaCodigo);
            }
            else if (e.State is EntityState.Added or EntityState.Unchanged or EntityState.Modified
                && e.Entity.ValidoAte is null)
            {
                added.Add(e.Entity.AreaCodigo);
            }
        }

        return new ResolvedBindings(added, removed, known);
    }

    private static HashSet<AreaCodigo> ComporResultado(
        ResolvedBindings tracked,
        IReadOnlyCollection<AreaCodigo> persistedVigentes)
    {
        HashSet<AreaCodigo> resolvidas = [.. tracked.Added];
        foreach (AreaCodigo area in persistedVigentes.Where(area => !tracked.Known.Contains(area)))
        {
            resolvidas.Add(area);
        }

        resolvidas.ExceptWith(tracked.Removed);
        return resolvidas;
    }

    private readonly record struct ResolvedBindings(
        HashSet<AreaCodigo> Added,
        HashSet<AreaCodigo> Removed,
        HashSet<AreaCodigo> Known);

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

        // Branch explícito via pattern matching — preserva exatamente a
        // semântica "null quando não houver proprietário" e elimina o
        // dereference ambíguo de `Proprietario?.Value` (record struct
        // nullable) que o analisador estático ainda flagrava com a
        // simples hoist em local.
        string? proprietarioCodigo = regra.Proprietario is { } prop ? prop.Value : null;

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
            ["proprietario"] = proprietarioCodigo,
            ["areasDeInteresse"] = areas,
            ["isDeleted"] = regra.IsDeleted,
        };

        JsonNode canonical = HashCanonicalComputer.CanonicalizeRecursive(payload);
        return canonical.ToJsonString();
    }
}
