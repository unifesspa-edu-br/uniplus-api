namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Configuração EF Core do catálogo de fatos do candidato (<see cref="FatoCandidato"/>) — a tabela
/// <c>dados_do_candidato</c> (UNI-REQ-0077, ADR-0111).
/// </summary>
/// <remarks>
/// <para>
/// O fato é desativado, nunca apagado, e a entidade deriva de <c>EntityBase</c> puro (sem
/// soft-delete). Por isso o índice único <c>ux_dados_do_candidato_codigo</c> é
/// <strong>total</strong>: o código é chave natural imutável de uma linha que nunca é removida.
/// </para>
/// <para>
/// Os enums são persistidos como código de contrato UPPER_SNAKE por value converter (falha ao ler
/// um código desconhecido), cada um com um CHECK que restringe o texto ao domínio fechado.
/// <c>ponto_resolucao</c>/<c>binding</c> (ADR-0116) são referência por valor, sem FK.
/// </para>
/// </remarks>
public sealed class FatoCandidatoConfiguration : IEntityTypeConfiguration<FatoCandidato>
{
    private const int CodigoMaxLength = 50;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;
    private const int CodigoDeContratoMaxLength = 20;
    private const int FonteValoresMaxLength = 30;
    private const int PontoResolucaoMaxLength = 50;
    private const int BindingMaxLength = 200;

    public void Configure(EntityTypeBuilder<FatoCandidato> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        builder.ToTable("dados_do_candidato", ChecksDoFatoCandidato.Configurar);
        ConfigurarComentarios(builder);
        ConfigurarIdentificacao(builder);
        ConfigurarClassificacaoDoFato(builder);
        ConfigurarProtecaoDeDados(builder);
        ConfigurarAprovacaoDaProtecao(builder);
        ConfigurarControle(builder);
        ConfigurarReferenciasPorValor(builder);
        ConfigurarRegrasPadrao(builder);
        ConfigurarDependencias(builder);
        ConfigurarValoresDeDominio(builder);
        ConfigurarIndice(builder);
        builder.HasData(MaterializarSeed());
    }

    private static void ConfigurarComentarios(EntityTypeBuilder<FatoCandidato> builder)
    {
        builder.Property(f => f.Id).HasComment("Identificador do dado do candidato.");
        builder.Property(f => f.Codigo).HasComment("Código único do dado, como RENDA_PER_CAPITA.");
        builder.Property(f => f.Nome).HasComment("Nome apresentado para o dado.");
        builder.Property(f => f.Descricao).HasComment("Descrição do significado e uso do dado.");
        builder.Property(f => f.ComponenteExibicao).HasComment("Apresentação sugerida: MOEDA ou SITUACAO; nula quando não definida.");
        builder.Property(f => f.Dominio).HasComment("Tipo do dado: CATEGORICO, BOOLEANO, NUMERICO, TEXTO, DATA ou ENDERECO.");
        builder.Property(f => f.Origem).HasComment("Origem do dado: DERIVADO, DECLARADO ou INTEGRACAO.");
        builder.Property(f => f.Cardinalidade).HasComment("Quantidade de valores: ESCALAR ou MULTIVALORADO.");
        builder.Property(f => f.FonteValores).HasComment("Origem das opções: GLOBAL, PROCESSO, MODALIDADE, MUNICIPIOS_BONUS (municípios do Fator de Atenuação das Desigualdades de Acesso (FADA)), MUNICIPIOS_PROVA_PRESENCIAL, GEO_UF, GEO_MUNICIPIO ou TIPOS_DEFICIENCIA; nula para outros tipos.");
        builder.Property(f => f.Formato).HasComment("Formato do texto: LIVRE, CPF, EMAIL, TELEFONE, CEP ou NOME_PESSOA; nulo para outros tipos.");
        builder.Property(f => f.Escopo).HasComment("Pessoa a que se refere: CANDIDATO ou MEMBRO_GRUPO.");
        builder.Property(f => f.ClassificacaoProtecao).HasComment("Classificação de proteção: PUBLICO, INTERNO, PESSOAL, IDENTIFICADOR ou SENSIVEL.");
        builder.Property(f => f.FinalidadeTratamento).HasComment("Finalidade informada para o tratamento do dado.");
        builder.Property(f => f.HipoteseLegal).HasComment("Hipótese legal: CONSENTIMENTO, CUMPRIMENTO_OBRIGACAO_LEGAL, EXECUCAO_POLITICAS_PUBLICAS, ESTUDOS_POR_ORGAO_DE_PESQUISA, EXECUCAO_CONTRATO, EXERCICIO_REGULAR_DE_DIREITOS, PROTECAO_DA_VIDA, TUTELA_DA_SAUDE, INTERESSE_LEGITIMO, PROTECAO_DO_CREDITO ou PREVENCAO_A_FRAUDE.");
        builder.Property(f => f.SemeadoPeloSistema).HasComment("Indica se o dado foi fornecido pelo sistema.");
        builder.Property(f => f.Ativo).HasComment("Indica se o dado está disponível para uso.");
        builder.Property(f => f.PontoResolucao).HasComment("Código da fase em que o valor fica disponível.");
        builder.Property(f => f.Binding).HasComment("Referência à fonte usada para obter o valor.");
        builder.Property(f => f.RegrasPadrao).HasComment("Regras de cálculo padrão do dado, em documento JSON.");
        builder.Property(f => f.Dependencias).HasComment("Códigos dos dados necessários ao cálculo pelo sistema.");
    }

    private static void ConfigurarIdentificacao(EntityTypeBuilder<FatoCandidato> builder)
    {
        builder.HasKey(f => f.Id);

        // Chave Guid v7 gerada no domínio (EntityBase): ValueGeneratedNever força o
        // EF a tratar a chave como fornecida pela aplicação (convenção do repo).
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(f => f.Nome).HasMaxLength(NomeMaxLength).IsRequired();
        builder.Property(f => f.Descricao).HasMaxLength(DescricaoMaxLength).IsRequired();
    }

    private static void ConfigurarClassificacaoDoFato(EntityTypeBuilder<FatoCandidato> builder)
    {
        ConfigurarComponenteEDominio(builder);
        ConfigurarOrigemCardinalidadeEEscopo(builder);
        ConfigurarFonteEFormato(builder);
    }

    private static void ConfigurarComponenteEDominio(EntityTypeBuilder<FatoCandidato> builder)
    {
        // componente de exibição: anulável — sem declaração vale o padrão que a consulta resolve.
        builder.Property(f => f.ComponenteExibicao)
            .HasConversion(ComponenteExibicaoConverter)
            .HasMaxLength(CodigoDeContratoMaxLength);

        builder.Property(f => f.Dominio).HasColumnName("tipo")
            .HasConversion(DominioConverter)
            .HasMaxLength(CodigoDeContratoMaxLength)
            .IsRequired();
    }

    private static void ConfigurarOrigemCardinalidadeEEscopo(EntityTypeBuilder<FatoCandidato> builder)
    {
        builder.Property(f => f.Origem)
            .HasConversion(OrigemConverter).HasMaxLength(CodigoDeContratoMaxLength).IsRequired();
        builder.Property(f => f.Cardinalidade).HasColumnName("quantidade_de_valores")
            .HasConversion(CardinalidadeConverter).HasMaxLength(CodigoDeContratoMaxLength).IsRequired();
        builder.Property(f => f.Escopo)
            .HasConversion(EscopoConverter).HasMaxLength(CodigoDeContratoMaxLength).IsRequired();
    }

    private static void ConfigurarFonteEFormato(EntityTypeBuilder<FatoCandidato> builder)
    {
        // fonte_valores (ADR-0136): anulável — só o fato categórico tem fonte; o booleano e o
        // numérico ficam com NULL, que o EF encapsula sem chamar o converter.
        builder.Property(f => f.FonteValores).HasColumnName("fonte_das_opcoes")
            .HasConversion(FonteValoresConverter)
            .HasMaxLength(FonteValoresMaxLength);

        // formato (ADR-0136): anulável — só o fato de domínio texto tem formato.
        builder.Property(f => f.Formato)
            .HasConversion(FormatoConverter)
            .HasMaxLength(CodigoDeContratoMaxLength);
    }

    // Proteção de dados (ADR-0136): classificação na escala da ADR-0081, finalidade e
    // hipótese legal do tratamento, obrigatórias em todo fato.
    private static void ConfigurarProtecaoDeDados(EntityTypeBuilder<FatoCandidato> builder)
    {
        builder.Property(f => f.ClassificacaoProtecao)
            .HasConversion<ClassificacaoDeProtecaoValueConverter>()
            .HasMaxLength(ClassificacaoDeProtecaoValueConverter.MaxLength)
            .IsRequired();
        builder.Property(f => f.FinalidadeTratamento).HasMaxLength(ProtecaoDeDados.FinalidadeMaxLength).IsRequired();
        builder.Property(f => f.HipoteseLegal)
            .HasConversion(ProtecaoDeDadosConversores.HipoteseLegal)
            .HasMaxLength(ProtecaoDeDadosConversores.HipoteseLegalMaxLength)
            .IsRequired();
    }

    // Aprovação da proteção de dados (ADR-0153): table splitting em colunas protecao_*. Todo fato
    // nasce pendente, o semeado inclusive; a semente dá só o valor inicial da proteção.
    private static void ConfigurarAprovacaoDaProtecao(EntityTypeBuilder<FatoCandidato> builder)
    {
        builder.OwnsOne(f => f.Aprovacao, ConfigurarColunasDaAprovacao);
        builder.Navigation(f => f.Aprovacao).IsRequired();
    }

    private static void ConfigurarColunasDaAprovacao(OwnedNavigationBuilder<FatoCandidato, AprovacaoDaProtecao> aprovacao)
    {
        AprovacaoDaProtecaoOwnedConfiguration.Configure(aprovacao);
        aprovacao.Property(a => a.Situacao).HasComment("Situação da análise: PENDENTE, SOLICITADA, APROVADA, AJUSTE_RECOMENDADO ou REPROVADA.");
        aprovacao.Property(a => a.Ciclo).HasComment("Número do ciclo de análise da proteção, iniciado em 1.");
        aprovacao.Property(a => a.SolicitadaEm).HasComment("Data e hora da solicitação de análise, quando houver.");
        aprovacao.Property(a => a.SolicitadaPor).HasComment("Identificador de quem solicitou a análise, quando houver.");
        aprovacao.Property(a => a.DecididaEm).HasComment("Data e hora da decisão, quando houver.");
        aprovacao.Property(a => a.DecididaPor).HasComment("Identificador de quem decidiu, quando houver.");
        aprovacao.Property(a => a.ClassificacaoRecomendada).HasComment("Classificação recomendada: PUBLICO, INTERNO, PESSOAL, IDENTIFICADOR ou SENSIVEL; nula quando não houver.");
        aprovacao.Property(a => a.Justificativa).HasComment("Justificativa da decisão, quando informada.");
        aprovacao.Property(a => a.AceitaEm).HasComment("Data e hora da aceitação, quando houver.");
        aprovacao.Property(a => a.AceitaPor).HasComment("Identificador de quem aceitou, quando houver.");
        aprovacao.Property(a => a.ManifestadaEm).HasComment("Data e hora da última manifestação no ciclo, quando houver.");
        aprovacao.Property(a => a.ManifestacaoDesfavoravelVigente).HasComment("Indica se há manifestação desfavorável vigente.");
        aprovacao.Property(a => a.ManifestacoesNoCiclo).HasComment("Quantidade de manifestações no ciclo atual.");
        aprovacao.HasData(FatoCandidatoSeed.Itens.Select(item => new { FatoCandidatoId = item.Id, Situacao = SituacaoDaAprovacao.Pendente, Ciclo = CicloDaAprovacao.Primeiro, ManifestacaoDesfavoravelVigente = false, ManifestacoesNoCiclo = 0 }));
    }

    private static void ConfigurarControle(EntityTypeBuilder<FatoCandidato> builder)
    {
        builder.Property(f => f.SemeadoPeloSistema).IsRequired();
        builder.Property(f => f.Ativo).IsRequired();
        builder.Property(f => f.CreatedBy).HasMaxLength(255);
        builder.Property(f => f.UpdatedBy).HasMaxLength(255);

        // Concorrência otimista pela coluna de sistema xmin (convenção do provider Npgsql, sem
        // coluna própria): duas edições concorrentes do mesmo fato não se sobrescrevem.
        builder.Property<uint>("Version").IsRowVersion();
    }

    // ponto_resolucao/binding (ADR-0116): referência por valor, sem FK — mesmo
    // padrão de dominio/origem/cardinalidade (código como valor, não linha viva).
    private static void ConfigurarReferenciasPorValor(EntityTypeBuilder<FatoCandidato> builder)
    {
        builder.Property(f => f.PontoResolucao).HasColumnName("fase_de_disponibilidade").HasMaxLength(PontoResolucaoMaxLength).IsRequired();
        builder.Property(f => f.Binding).HasColumnName("fonte_do_valor").HasMaxLength(BindingMaxLength).IsRequired();
    }

    // Regras padrão do derivado por regra (ADR-0136): lidas e gravadas sempre inteiras, junto do
    // fato, sem consulta por condição — por isso num documento, e não em linhas.
    private static void ConfigurarRegrasPadrao(EntityTypeBuilder<FatoCandidato> builder)
    {
        builder.Property(f => f.RegrasPadrao)
            .HasColumnType("jsonb")
            .HasConversion(RegrasPadraoJson.Converter, RegrasPadraoJson.Comparer)
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired();
    }

    // Dependências do derivado do sistema (ADR-0136), declaradas pelo seed a partir do mecanismo
    // que calcula o fato.
    private static void ConfigurarDependencias(EntityTypeBuilder<FatoCandidato> builder)
    {
        builder.Property(f => f.Dependencias)
            .HasColumnName("dependencias")
            .HasDefaultValueSql("'{}'::text[]")
            .IsRequired();
    }

    // Coleção filha (ADR-0116): descrição por valor de um categórico estático.
    // Cascade porque o filho não tem sentido sem o pai (mesmo padrão de
    // AtendimentoEspecializado.Condicoes).
    private static void ConfigurarValoresDeDominio(EntityTypeBuilder<FatoCandidato> builder)
    {
        builder.HasMany(f => f.ValoresDominioDeclarados)
            .WithOne()
            .HasForeignKey(v => v.FatoCandidatoId)
            .HasConstraintName("fk_opcoes_dos_dados_dados_do_candidato")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(f => f.ValoresDominioDeclarados)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    // UNIQUE total do código (chave natural imutável): o fato é desativado, nunca apagado,
    // então não há slot a liberar — a unicidade é absoluta.
    private static void ConfigurarIndice(EntityTypeBuilder<FatoCandidato> builder)
    {
        builder.HasIndex(f => f.Codigo)
            .IsUnique()
            .HasDatabaseName(RestricoesDoBanco.FatoDoCandidatoCodigoUnico);
    }

    /// <summary>
    /// Projeta o seed (<see cref="FatoCandidatoSeed.Itens"/>) para linhas que o
    /// <c>HasData</c> congela como literais na migration inicial. O instante é fixo
    /// (as linhas não passam pelo <c>AuditableInterceptor</c>); mudar o seed muda o
    /// modelo, e a migration inicial é gerada de novo.
    /// </summary>
    private static IEnumerable<object> MaterializarSeed()
    {
        // Instante fixo do seed (HasData exige valor determinístico).
        DateTimeOffset seedCriadoEm = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        return FatoCandidatoSeed.Itens.Select(item => new
        {
            item.Id,
            item.Codigo,
            item.Nome,
            item.Descricao,
            item.ComponenteExibicao,
            item.Dominio,
            item.Origem,
            item.Cardinalidade,
            item.FonteValores,
            item.Formato,
            item.PontoResolucao,
            item.Binding,
            item.Escopo,
            item.ClassificacaoProtecao,
            item.FinalidadeTratamento,
            item.HipoteseLegal,
            SemeadoPeloSistema = true,
            Ativo = true,
            RegrasPadrao = item.RegrasPadrao ?? (IReadOnlyList<RegraDerivacao>)[],
            Dependencias = DerivadosDoSistema.Dependencias.GetValueOrDefault(item.Codigo) ?? [],
            CreatedAt = seedCriadoEm,
        });
    }

    // ── Conversores/comparadores ──────────────────────────────────────────────

    private static readonly ValueConverter<DominioFato, string> DominioConverter =
        new(dominio => DominiosFato.ParaTokenCanonico(dominio), codigo => DominiosFato.Analisar(codigo));

    private static readonly ValueConverter<OrigemFato, string> OrigemConverter =
        new(origem => OrigensFato.ParaTokenCanonico(origem), codigo => OrigensFato.Analisar(codigo));

    private static readonly ValueConverter<FonteValoresFato, string> FonteValoresConverter =
        new(fonte => FontesValoresFato.ParaTokenCanonico(fonte), codigo => FontesValoresFato.Analisar(codigo));

    private static readonly ValueConverter<CardinalidadeFato, string> CardinalidadeConverter =
        new(cardinalidade => CardinalidadesFato.ParaTokenCanonico(cardinalidade), codigo => CardinalidadesFato.Analisar(codigo));

    private static readonly ValueConverter<ComponenteDeExibicao, string> ComponenteExibicaoConverter =
        new(componente => ComponentesDeExibicao.ParaTokenCanonico(componente), codigo => ComponentesDeExibicao.Analisar(codigo));

    private static readonly ValueConverter<FormatoTexto, string> FormatoConverter =
        new(formato => FormatosTexto.ParaTokenCanonico(formato), codigo => FormatosTexto.Analisar(codigo));

    private static readonly ValueConverter<EscopoFato, string> EscopoConverter =
        new(escopo => EscoposFato.ParaTokenCanonico(escopo), codigo => EscoposFato.Analisar(codigo));
}
