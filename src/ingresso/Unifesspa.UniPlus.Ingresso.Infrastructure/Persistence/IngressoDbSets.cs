namespace Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>As coleções das entidades do módulo, expostas no contexto único para consultas, repositórios e testes.</summary>
public static class IngressoDbSets
{
    extension(UniPlusDbContext contexto)
    {
        public DbSet<Chamada> Chamadas => contexto.Set<Chamada>();

        public DbSet<Convocacao> Convocacoes => contexto.Set<Convocacao>();

        public DbSet<Matricula> Matriculas => contexto.Set<Matricula>();

        public DbSet<DocumentoMatricula> DocumentosMatricula => contexto.Set<DocumentoMatricula>();
    }
}
