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
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
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
/// <see cref="HashCanonicalComputer"/> + id da regra — assim a reconstituição
/// forense não depende de juntar tabelas: cada linha do histórico é completa.
/// </para>
/// </remarks>
public sealed class ObrigatoriedadeLegalHistoricoInterceptor : SaveChangesInterceptor
{
    private const string SystemUser = "system";

    private readonly IUserContext? _userContext;
    private readonly TimeProvider _timeProvider;

    // TimeProvider é obrigatório (sem fallback TimeProvider.System): o relógio
    // é sempre injetado pela DI (Singleton). IUserContext permanece opcional —
    // o fallback "system" é regra legítima para fluxos sem principal (jobs,
    // migrations), não um backdoor de não-determinismo.
    public ObrigatoriedadeLegalHistoricoInterceptor(TimeProvider timeProvider, IUserContext? userContext = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
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
            CapturarHistorico(eventData.Context);
        }

        return await base.SavingChangesAsync(eventData!, result, cancellationToken).ConfigureAwait(false);
    }

    private void CapturarHistorico(DbContext context)
    {
        DateTimeOffset snapshotAt = _timeProvider.GetUtcNow();
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
            historicoSet.Add(CriarSnapshot(entry, snapshotAt, snapshotBy));
        }
    }

    // Snapshot ANTES de mutar o ChangeTracker — adicionar entries durante a
    // iteração pode invalidar o enumerador. Retorna lista materializada para
    // que o caller consuma sem reentrar no tracker. Captura toda regra
    // criada ou alterada no save corrente.
    private static List<EntityEntry<ObrigatoriedadeLegal>> CapturarEntries(DbContext context)
    {
        return [.. context
            .ChangeTracker
            .Entries<ObrigatoriedadeLegal>()
            .Where(static entry => entry.State is EntityState.Added or EntityState.Modified)];
    }

    private static ObrigatoriedadeLegalHistorico CriarSnapshot(
        EntityEntry<ObrigatoriedadeLegal> entry,
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

        string conteudoJson = SerializarConteudoCanonical(regra, hash);
        return ObrigatoriedadeLegalHistorico.Snapshot(
            regraId: regra.Id,
            conteudoJson: conteudoJson,
            hash: hash,
            snapshotAt: snapshotAt,
            snapshotBy: snapshotBy);
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
    /// Serializa o snapshot canônico da regra (campos semânticos + id + hash).
    /// Reusa <see cref="HashCanonicalComputer.CanonicalOptions"/> e
    /// <see cref="HashCanonicalComputer.CanonicalizeRecursive"/> — invariante
    /// load-bearing: o JSON persistido em <c>obrigatoriedade_legal_historico.conteudo_jsonb</c>
    /// é byte-equivalente ao payload que alimentou o hash, garantindo
    /// reprodutibilidade forense ("recomputar hash a partir do snapshot
    /// retorna o mesmo valor").
    /// </summary>
    private static string SerializarConteudoCanonical(
        ObrigatoriedadeLegal regra,
        string hash)
    {
        JsonNode? predicadoNode = JsonSerializer.SerializeToNode(
            regra.Predicado,
            HashCanonicalComputer.CanonicalOptions);

        JsonObject payload = new()
        {
            ["id"] = regra.Id,
            ["tipoProcessoCodigo"] = regra.TipoProcessoCodigo,
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
            ["isDeleted"] = regra.IsDeleted,
        };

        JsonNode canonical = HashCanonicalComputer.CanonicalizeRecursive(payload);
        return canonical.ToJsonString();
    }
}
