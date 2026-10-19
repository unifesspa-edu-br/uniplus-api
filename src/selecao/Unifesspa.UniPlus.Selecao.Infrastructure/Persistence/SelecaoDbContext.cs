namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

public sealed class SelecaoDbContext : DbContext, ISelecaoUnitOfWork
{
    /// <summary>
    /// Schema do módulo no banco único do monólito modular. Tabelas,
    /// índices, FKs e idempotency_cache deste DbContext vivem neste schema.
    /// </summary>
    public const string Schema = "selecao";

    /// <summary>
    /// Sequência do número de inscrição (issue #1858): gerado só na primeira submissão, sem
    /// exigência de não ter lacunas (um `nextval` de uma submissão que falhe antes de gravar não
    /// é reaproveitado) — <c>bigint</c> nativo do Postgres, nunca um contador em memória nem uma
    /// coluna identity da tabela, porque o número precisa existir ANTES da linha gravada (o
    /// domínio o recebe pronto em <see cref="Domain.Entities.Inscricao.Submeter"/>).
    /// </summary>
    public const string SequenciaDeNumeroDeInscricao = "numero_inscricao_seq";

    public SelecaoDbContext(DbContextOptions<SelecaoDbContext> options) : base(options)
    {
    }

    public DbSet<ProcessoSeletivo> ProcessosSeletivos => Set<ProcessoSeletivo>();

    /// <summary>
    /// Entidades de configuração do agregado <see cref="ProcessoSeletivo"/>
    /// (Story #758). Expostas como DbSet para consultas e seeds de teste; a
    /// escrita passa sempre pela raiz via <c>IProcessoSeletivoRepository</c>.
    /// </summary>
    public DbSet<EtapaProcesso> EtapasProcesso => Set<EtapaProcesso>();
    public DbSet<OfertaAtendimentoEspecializado> OfertasAtendimentoEspecializado => Set<OfertaAtendimentoEspecializado>();

    /// <summary>
    /// Distribuição de vagas por oferta de curso (Story #773):
    /// os inputs (VO_base, PR, referência à regra tipada, snapshot da
    /// referência demográfica) e as modalidades selecionadas. O quadro de
    /// vagas (output derivado, issue #848/ADR-0115) é materializado junto,
    /// na mesma factory — ver <see cref="VagasOfertadas"/>.
    /// </summary>
    public DbSet<ConfiguracaoDistribuicaoVagas> ConfiguracoesDistribuicaoVagas => Set<ConfiguracaoDistribuicaoVagas>();
    public DbSet<ModalidadeSelecionada> ModalidadesSelecionadas => Set<ModalidadeSelecionada>();
    public DbSet<VagaOfertada> VagasOfertadas => Set<VagaOfertada>();

    /// <summary>
    /// Bônus regional (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015) e critérios de desempate (Story #774) — ambos por
    /// referência ao <c>rol_de_regras</c>.
    /// </summary>
    public DbSet<ConfiguracaoBonusRegional> ConfiguracoesBonusRegional => Set<ConfiguracaoBonusRegional>();
    public DbSet<CriterioDesempate> CriteriosDesempate => Set<CriterioDesempate>();

    /// <summary>
    /// Cascata de remanejamento das cotas federais (Story #575, cascata de remanejamento de cotas, UNI-REQ-0056)
    /// — a sequência que a flag <c>SegueCascata</c> de <see cref="ModalidadeSelecionada"/>
    /// invoca.
    /// </summary>
    public DbSet<ConfiguracaoCascataRemanejamento> ConfiguracoesCascataRemanejamento => Set<ConfiguracaoCascataRemanejamento>();
    public DbSet<DestinoRemanejamento> DestinosRemanejamento => Set<DestinoRemanejamento>();

    /// <summary>
    /// Classificação (15º bloco canônico, Story #775) —
    /// compõe por referência a fórmula, precisão, eliminação e ordem de
    /// alocação.
    /// </summary>
    public DbSet<ConfiguracaoClassificacao> ConfiguracoesClassificacao => Set<ConfiguracaoClassificacao>();
    public DbSet<RegraEliminacao> RegrasEliminacao => Set<RegraEliminacao>();

    /// <summary>Quadro de pesos por área do ENEM congelado na classificação: um item por grupo de área, com as áreas.</summary>
    public DbSet<GrupoPesoAreaEnemCongelado> GruposPesoAreaEnemCongelados => Set<GrupoPesoAreaEnemCongelado>();
    public DbSet<AreaPesoAreaEnemCongelada> AreasPesoAreaEnemCongeladas => Set<AreaPesoAreaEnemCongelada>();

    /// <summary>
    /// Cronograma de fases do processo (Story #851) — o eixo temporal, distinto das
    /// etapas (eixo de pontuação). Exposta como DbSet para consultas e seeds de teste;
    /// a escrita passa sempre pela raiz via <c>IProcessoSeletivoRepository</c>.
    /// </summary>
    public DbSet<FaseCronograma> FasesCronograma => Set<FaseCronograma>();
    public DbSet<RegraRecursoFase> RegrasRecursoFase => Set<RegraRecursoFase>();
    public DbSet<BancaRequerida> BancasRequeridas => Set<BancaRequerida>();

    /// <summary>
    /// Biblioteca <c>rol_de_regras</c> (Story #772) — regras tipadas e
    /// versionadas que a configuração do Processo Seletivo referencia
    /// (<c>codigo</c>+<c>versao</c>+<c>hash</c>), congeladas no snapshot de
    /// publicação (congelamento dos parâmetros na publicação, UNI-REQ-0019). Seed-governada e append-only (não é CRUD de admin):
    /// a leitura passa por <c>IRegraCatalogoReader</c> e a única escrita é o
    /// seed da migration.
    /// </summary>
    public DbSet<RegraCatalogo> RolDeRegras => Set<RegraCatalogo>();

    /// <summary>
    /// Catálogo institucional de motivos de decisão de isenção (UNI-REQ-0120) —
    /// cadastro administrativo, protegido pela permissão de manutenção. A mesma
    /// lista serve à análise documental inicial e ao recurso do fundamento.
    /// </summary>
    public DbSet<MotivoDecisaoIsencao> MotivosDecisaoIsencao => Set<MotivoDecisaoIsencao>();

    /// <summary>
    /// Catálogo data-driven de regras legais (Story #460, ADR-0058). O CRUD
    /// admin entra em #461; em V1 esta DbSet é populada via factory direta
    /// para testes e seeds.
    /// </summary>
    public DbSet<ObrigatoriedadeLegal> ObrigatoriedadesLegais => Set<ObrigatoriedadeLegal>();

    /// <summary>
    /// Histórico append-only de mutações de <see cref="ObrigatoriedadesLegais"/>
    /// (CA-03). Linha inserida na mesma transação do save da regra pelo
    /// <c>ObrigatoriedadeLegalHistoricoInterceptor</c>.
    /// </summary>
    public DbSet<ObrigatoriedadeLegalHistorico> ObrigatoriedadeLegalHistorico =>
        Set<ObrigatoriedadeLegalHistorico>();

    /// <summary>
    /// Documentos que o processo envia para publicar (PDF, DOCX ou ODT), vinculados ao
    /// processo por FK — não são filhos do agregado <see cref="ProcessoSeletivo"/> (ver
    /// comentário de <see cref="DocumentoAPublicar"/>).
    /// </summary>
    public DbSet<DocumentoAPublicar> DocumentosAPublicar => Set<DocumentoAPublicar>();

    /// <summary>
    /// Registro dos atos do processo, criem versão de configuração ou não, gravado na mesma transação
    /// do envio ao outbox — ver <see cref="AtoDoProcesso"/>.
    /// </summary>
    public DbSet<AtoDoProcesso> AtosDoProcesso => Set<AtoDoProcesso>();

    /// <summary>Modelos de documento que as exigências oferecem ao candidato — fora do agregado, como o documento a publicar.</summary>
    public DbSet<ModeloDeDocumento> ModelosDeDocumento => Set<ModeloDeDocumento>();

    /// <summary>
    /// Versões congeladas da configuração do certame (congelamento dos parâmetros na publicação, UNI-REQ-0019, ADR-0104/0100) —
    /// <see cref="VersaoConfiguracao"/> é <c>IForensicEntity</c> e agregado
    /// próprio: referencia o ato que a criou por valor, sem chave estrangeira,
    /// e é persistida pelo <c>IProcessoSeletivoRepository</c> por fora da raiz.
    /// </summary>
    public DbSet<VersaoConfiguracao> VersoesConfiguracao => Set<VersaoConfiguracao>();

    /// <summary>
    /// Projeção pública do certame. A existência da linha é a publicidade — ver
    /// <see cref="CertameDivulgado"/>.
    /// </summary>
    public DbSet<CertameDivulgado> CertamesDivulgados => Set<CertameDivulgado>();

    /// <summary>
    /// Sessões editoriais de retificação em curso (ADR-0110 D3) — filha 1:1 do agregado,
    /// exposta como DbSet para consultas e asserções de teste; a escrita passa sempre pela
    /// raiz. É <b>efêmera</b>: apagada no fechamento e no descarte, sem histórico.
    /// </summary>
    public DbSet<RascunhoRetificacao> RascunhosRetificacao => Set<RascunhoRetificacao>();

    /// <summary>
    /// O bloco do ato que o operador já transcreveu no passo de Revisão, guardado para
    /// sobreviver a um recarregamento. Conteúdo <b>opaco</b> (ver
    /// <see cref="RascunhoDePublicacao"/>): nada aqui alimenta a publicação, que continua
    /// recebendo os dados do ato no próprio corpo do comando.
    /// </summary>
    public DbSet<RascunhoDePublicacao> RascunhosDePublicacao => Set<RascunhoDePublicacao>();

    /// <summary>
    /// Cache de Idempotency-Key (ADR-0027). Vive no mesmo banco do agregado
    /// para permitir gravação adjacente no outbox; entries cifradas at-rest
    /// via <c>IUniPlusEncryptionService</c>.
    /// </summary>
    public DbSet<IdempotencyEntry> IdempotencyEntries => Set<IdempotencyEntry>();

    /// <summary>Modelo de persistência de <see cref="Domain.Entities.Inscricao"/> (issue #1858) — ver <c>InscricaoRecord</c>.</summary>
    public DbSet<InscricaoRecord> Inscricoes => Set<InscricaoRecord>();

    public DbSet<RespostaDaInscricaoRecord> RespostasDaInscricao => Set<RespostaDaInscricaoRecord>();

    public DbSet<EnderecoDaInscricaoRecord> EnderecosDaInscricao => Set<EnderecoDaInscricaoRecord>();

    public DbSet<OcorrenciaDeGrupoDaInscricaoRecord> OcorrenciasDeGrupoDaInscricao => Set<OcorrenciaDeGrupoDaInscricaoRecord>();

    public DbSet<AceiteDaInscricaoRecord> AceitesDaInscricao => Set<AceiteDaInscricaoRecord>();

    /// <summary>Comprovantes de submissão (issue #1858) — mapeado direto, sem record de tradução (sem campo cifrado).</summary>
    public DbSet<ComprovanteInscricao> ComprovantesInscricao => Set<ComprovanteInscricao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        // Banco único, schema-por-módulo.
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SelecaoDbContext).Assembly);
        // Configurações cross-cutting de Infrastructure.Core (ex.: idempotency_cache).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdempotencyEntry).Assembly);
        // Convenção global de soft-delete (issue #629): aplica `!IsDeleted` a todo
        // tipo ISoftDeletable, após os ApplyConfigurations registrarem os tipos.
        modelBuilder.AplicarFiltroGlobalSoftDelete();
        base.OnModelCreating(modelBuilder);

        // Mapeia PgFunctions.NormalizeForComparison para a função criada por migration. Sem esse
        // registro, a consulta que a chama não traduz e o EF tentaria avaliá-la no cliente, onde o
        // stub lança.
        modelBuilder.HasDbFunction(
            typeof(PgFunctions).GetMethod(nameof(PgFunctions.NormalizeForComparison))!)
            .HasName("normalizar_para_comparacao")
            .HasSchema(Schema);

        modelBuilder.HasSequence<long>(SequenciaDeNumeroDeInscricao, Schema)
            .StartsAt(1)
            .IncrementsBy(1);
    }

    public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
    {
        return await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public void DescartarAlteracoesNaoSalvas()
    {
        ChangeTracker.Clear();
    }
}
