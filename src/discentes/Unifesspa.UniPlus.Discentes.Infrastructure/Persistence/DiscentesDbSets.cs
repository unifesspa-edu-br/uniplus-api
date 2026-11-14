namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Records;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>As coleções das entidades do módulo, expostas no contexto único para consultas, repositórios e testes.</summary>
public static class DiscentesDbSets
{
    extension(UniPlusDbContext contexto)
    {
        /// <summary>CPF cifrado em repouso (ADR-0121); o acesso de domínio é só pelo <c>IVinculoDiscenteRepository</c>.</summary>
        public DbSet<VinculoDiscenteRecord> VinculosDiscentes => contexto.Set<VinculoDiscenteRecord>();

        public DbSet<SyncRun> SyncRuns => contexto.Set<SyncRun>();
    }
}
