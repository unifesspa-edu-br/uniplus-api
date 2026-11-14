namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

/// <summary>As coleções das entidades do módulo, expostas no contexto único para consultas, repositórios e testes.</summary>
public static class PublicacoesDbSets
{
    extension(UniPlusDbContext contexto)
    {
        public DbSet<TipoAtoPublicado> TiposAtoPublicado => contexto.Set<TipoAtoPublicado>();

        /// <summary>Registro central e append-only: só recebe <c>INSERT</c>; <c>UPDATE</c> e <c>DELETE</c> são bloqueados por trigger.</summary>
        public DbSet<AtoNormativo> AtosNormativos => contexto.Set<AtoNormativo>();

        public DbSet<VinculoAtoEntidade> VinculosAtoEntidade => contexto.Set<VinculoAtoEntidade>();

        /// <summary>Append-only como o ato que os carrega.</summary>
        public DbSet<AnexoDoAto> AnexosDoAto => contexto.Set<AnexoDoAto>();

        /// <summary>A chave que o banco trava para que um objeto não tenha duas linhagens de atos do mesmo tipo único.</summary>
        public DbSet<VagaDeLinhagem> VagasDeLinhagem => contexto.Set<VagaDeLinhagem>();
    }
}
