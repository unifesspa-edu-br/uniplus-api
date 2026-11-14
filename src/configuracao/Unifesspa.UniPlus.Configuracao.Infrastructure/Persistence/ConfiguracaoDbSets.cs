namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>As coleções das entidades do módulo, expostas no contexto único para consultas, repositórios e testes.</summary>
public static class ConfiguracaoDbSets
{
    extension(UniPlusDbContext contexto)
    {
        public DbSet<Campus> Campi => contexto.Set<Campus>();

        public DbSet<LocalOferta> LocaisOferta => contexto.Set<LocalOferta>();

        public DbSet<ReferenciaReservaDemografica> ReferenciasReservaDemografica => contexto.Set<ReferenciaReservaDemografica>();

        public DbSet<PesoAreaEnem> PesosAreaEnem => contexto.Set<PesoAreaEnem>();

        public DbSet<TipoDocumento> TiposDocumento => contexto.Set<TipoDocumento>();

        public DbSet<BaseLegalBonusRegional> BaseLegaisBonus => contexto.Set<BaseLegalBonusRegional>();

        public DbSet<CategoriaDocumento> CategoriasDocumento => contexto.Set<CategoriaDocumento>();

        public DbSet<CondicaoAtendimentoEspecializado> CondicoesAtendimento => contexto.Set<CondicaoAtendimentoEspecializado>();

        public DbSet<RecursoAcessibilidade> RecursosAcessibilidade => contexto.Set<RecursoAcessibilidade>();

        public DbSet<TipoDeficiencia> TiposDeficiencia => contexto.Set<TipoDeficiencia>();

        public DbSet<Modalidade> Modalidades => contexto.Set<Modalidade>();

        public DbSet<FaseCanonica> FasesCanonicas => contexto.Set<FaseCanonica>();

        public DbSet<TipoBanca> TiposBanca => contexto.Set<TipoBanca>();

        public DbSet<TipoProcesso> TiposProcesso => contexto.Set<TipoProcesso>();

        public DbSet<TipoEtapa> TiposEtapa => contexto.Set<TipoEtapa>();

        public DbSet<Curso> Cursos => contexto.Set<Curso>();

        public DbSet<OfertaCurso> OfertasCurso => contexto.Set<OfertaCurso>();

        public DbSet<CalendarioDiasUteis> CalendariosDiasUteis => contexto.Set<CalendarioDiasUteis>();

        public DbSet<TermoConsentimento> TermosConsentimento => contexto.Set<TermoConsentimento>();

        /// <summary>A versão promovida é adicionada por aqui, e não pela coleção em memória de um termo já rastreado.</summary>
        public DbSet<TermoConsentimentoVersao> VersoesTermoConsentimento => contexto.Set<TermoConsentimentoVersao>();

        public DbSet<FatoCandidato> FatosCandidato => contexto.Set<FatoCandidato>();

        public DbSet<ModeloFormulario> ModelosFormulario => contexto.Set<ModeloFormulario>();

        public DbSet<FatoValorDominio> FatosValorDominio => contexto.Set<FatoValorDominio>();

        public DbSet<PrecedenciaFase> PrecedenciasFase => contexto.Set<PrecedenciaFase>();

        public DbSet<AtoDaProtecaoDoCatalogo> AtosDaProtecaoDoCatalogo => contexto.Set<AtoDaProtecaoDoCatalogo>();

        public DbSet<ManifestacaoDaProtecaoDoCatalogo> ManifestacoesDaProtecaoDoCatalogo => contexto.Set<ManifestacaoDaProtecaoDoCatalogo>();
    }
}
