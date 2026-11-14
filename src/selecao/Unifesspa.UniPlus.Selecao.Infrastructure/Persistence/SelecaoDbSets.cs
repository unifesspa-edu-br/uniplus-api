namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

/// <summary>As coleções das entidades do módulo, expostas no contexto único para consultas, repositórios e testes.</summary>
public static class SelecaoDbSets
{
    extension(UniPlusDbContext contexto)
    {
        public DbSet<ProcessoSeletivo> ProcessosSeletivos => contexto.Set<ProcessoSeletivo>();

        public DbSet<EtapaProcesso> EtapasProcesso => contexto.Set<EtapaProcesso>();

        public DbSet<OfertaAtendimentoEspecializado> OfertasAtendimentoEspecializado => contexto.Set<OfertaAtendimentoEspecializado>();

        public DbSet<ConfiguracaoDistribuicaoVagas> ConfiguracoesDistribuicaoVagas => contexto.Set<ConfiguracaoDistribuicaoVagas>();

        public DbSet<ModalidadeSelecionada> ModalidadesSelecionadas => contexto.Set<ModalidadeSelecionada>();

        public DbSet<VagaOfertada> VagasOfertadas => contexto.Set<VagaOfertada>();

        public DbSet<ConfiguracaoBonusRegional> ConfiguracoesBonusRegional => contexto.Set<ConfiguracaoBonusRegional>();

        public DbSet<CriterioDesempate> CriteriosDesempate => contexto.Set<CriterioDesempate>();

        public DbSet<ConfiguracaoCascataRemanejamento> ConfiguracoesCascataRemanejamento => contexto.Set<ConfiguracaoCascataRemanejamento>();

        public DbSet<DestinoRemanejamento> DestinosRemanejamento => contexto.Set<DestinoRemanejamento>();

        public DbSet<ConfiguracaoClassificacao> ConfiguracoesClassificacao => contexto.Set<ConfiguracaoClassificacao>();

        public DbSet<RegraEliminacao> RegrasEliminacao => contexto.Set<RegraEliminacao>();

        public DbSet<GrupoPesoAreaEnemCongelado> GruposPesoAreaEnemCongelados => contexto.Set<GrupoPesoAreaEnemCongelado>();

        public DbSet<AreaPesoAreaEnemCongelada> AreasPesoAreaEnemCongeladas => contexto.Set<AreaPesoAreaEnemCongelada>();

        public DbSet<FaseCronograma> FasesCronograma => contexto.Set<FaseCronograma>();

        public DbSet<RegraRecursoFase> RegrasRecursoFase => contexto.Set<RegraRecursoFase>();

        public DbSet<BancaRequerida> BancasRequeridas => contexto.Set<BancaRequerida>();

        /// <summary>Regras tipadas e versionadas, seed-governadas e append-only: a única escrita é o seed da migration, e a leitura passa por <c>IRegraCatalogoReader</c>.</summary>
        public DbSet<RegraCatalogo> RolDeRegras => contexto.Set<RegraCatalogo>();

        public DbSet<MotivoDeDecisao> MotivosDeDecisao => contexto.Set<MotivoDeDecisao>();

        public DbSet<MotivoDoProcesso> MotivosDoProcesso => contexto.Set<MotivoDoProcesso>();

        public DbSet<ObrigatoriedadeLegal> ObrigatoriedadesLegais => contexto.Set<ObrigatoriedadeLegal>();

        /// <summary>Append-only; a linha entra na mesma transação do save da regra, pelo <c>ObrigatoriedadeLegalHistoricoInterceptor</c>.</summary>
        public DbSet<ObrigatoriedadeLegalHistorico> ObrigatoriedadeLegalHistorico => contexto.Set<ObrigatoriedadeLegalHistorico>();

        public DbSet<DocumentoAPublicar> DocumentosAPublicar => contexto.Set<DocumentoAPublicar>();

        public DbSet<AtoDoProcesso> AtosDoProcesso => contexto.Set<AtoDoProcesso>();

        public DbSet<ApresentacaoDeDocumento> ApresentacoesDeDocumento => contexto.Set<ApresentacaoDeDocumento>();

        public DbSet<ChaveALimpar> ChavesALimpar => contexto.Set<ChaveALimpar>();

        public DbSet<ArquivoDaApresentacao> ArquivosDaApresentacao => contexto.Set<ArquivoDaApresentacao>();

        public DbSet<ModeloDeDocumento> ModelosDeDocumento => contexto.Set<ModeloDeDocumento>();

        /// <summary>Persistida pelo <c>IProcessoSeletivoRepository</c> por fora da raiz do processo; referencia o ato que a criou por valor, sem chave estrangeira.</summary>
        public DbSet<VersaoConfiguracao> VersoesConfiguracao => contexto.Set<VersaoConfiguracao>();

        /// <summary>A existência da linha é a publicidade do certame.</summary>
        public DbSet<CertameDivulgado> CertamesDivulgados => contexto.Set<CertameDivulgado>();

        /// <summary>Efêmera: apagada no fechamento e no descarte da retificação, sem histórico.</summary>
        public DbSet<RascunhoRetificacao> RascunhosRetificacao => contexto.Set<RascunhoRetificacao>();

        /// <summary>Conteúdo opaco: nada aqui alimenta a publicação.</summary>
        public DbSet<RascunhoDePublicacao> RascunhosDePublicacao => contexto.Set<RascunhoDePublicacao>();

        public DbSet<InscricaoRecord> Inscricoes => contexto.Set<InscricaoRecord>();

        public DbSet<RespostaDaInscricaoRecord> RespostasDaInscricao => contexto.Set<RespostaDaInscricaoRecord>();

        public DbSet<EnderecoDaInscricaoRecord> EnderecosDaInscricao => contexto.Set<EnderecoDaInscricaoRecord>();

        public DbSet<OcorrenciaDeGrupoDaInscricaoRecord> OcorrenciasDeGrupoDaInscricao => contexto.Set<OcorrenciaDeGrupoDaInscricaoRecord>();

        public DbSet<AceiteDaInscricaoRecord> AceitesDaInscricao => contexto.Set<AceiteDaInscricaoRecord>();

        public DbSet<ComprovanteInscricao> ComprovantesInscricao => contexto.Set<ComprovanteInscricao>();

        public DbSet<LoteDeNotasDoEnem> LotesDeNotasDoEnem => contexto.Set<LoteDeNotasDoEnem>();

        public DbSet<InscritoDoLoteDeNotas> InscritosDoLoteDeNotas => contexto.Set<InscritoDoLoteDeNotas>();

        public DbSet<LancamentoDeNota> LancamentosDeNota => contexto.Set<LancamentoDeNota>();

        public DbSet<AvaliacaoDaEtapa> AvaliacoesDaEtapa => contexto.Set<AvaliacaoDaEtapa>();

        public DbSet<OcorrenciaDoLoteDeNotas> OcorrenciasDoLoteDeNotas => contexto.Set<OcorrenciaDoLoteDeNotas>();

        public DbSet<GeracaoDeEnvioAoInep> GeracoesDeEnvioAoInep => contexto.Set<GeracaoDeEnvioAoInep>();
    }
}
