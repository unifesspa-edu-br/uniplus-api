namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

/// <summary>As coleções das entidades do módulo, expostas no contexto único para consultas, repositórios e testes.</summary>
public static class OrganizacaoInstitucionalDbSets
{
    extension(UniPlusDbContext contexto)
    {
        public DbSet<Unidade> Unidades => contexto.Set<Unidade>();

        public DbSet<UnidadeIdentificadorHistorico> UnidadesIdentificadoresHistorico => contexto.Set<UnidadeIdentificadorHistorico>();

        public DbSet<Instituicao> Instituicoes => contexto.Set<Instituicao>();
    }
}
