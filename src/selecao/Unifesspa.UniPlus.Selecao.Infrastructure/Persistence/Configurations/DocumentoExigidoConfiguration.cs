namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Text.Json;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Converters;

/// <summary>
/// Configuração EF Core de <see cref="DocumentoExigido"/> — entidade
/// filha do agregado <see cref="ProcessoSeletivo"/>, <c>EntityBase</c> puro (sem
/// soft-delete). As colunas que gravam o código de um enum (aplicabilidade, escolhas, unidade, efeitos e tipo
/// da data de referência) têm CHECK com os códigos declarados no enum (ADR-0176); a consequência do
/// indeferimento fica sem CHECK. O resto da integridade é C# (guard de domínio + FluentValidation), mesmo
/// padrão de <see cref="FaseCronograma"/>/<see cref="ModalidadeSelecionada"/>.
/// <see cref="FormatosPermitidos"/> é a exceção: o CHECK
/// <c>ck_documentos_exigidos_formatos_permitidos_coerente</c> é defesa em profundidade contra a
/// corrida check-then-act do domínio, mesmo padrão de <c>FatoCandidatoConfiguration</c> (módulo
/// Configuração).
/// </summary>
public sealed class DocumentoExigidoConfiguration : IEntityTypeConfiguration<DocumentoExigido>
{
    private const int TipoDocumentoCodigoMaxLength = 60;
    // Espelha TipoDocumentoConfiguration.NomeMaxLength (Configuracao.Infrastructure) — um
    // snapshot-copy mais curto que a origem trunca silenciosamente (ou falha no Postgres)
    // um cadastro legítimo de até 200 caracteres.
    private const int TipoDocumentoNomeMaxLength = 200;
    private const int TipoDocumentoCategoriaMaxLength = 60;
    private const int FinalidadeTratamentoMaxLength = LimitesDoEnvelope.FinalidadeTratamento;
    private const int HipoteseLegalMaxLength = LimitesDoEnvelope.HipoteseLegal;
    private const int ConsequenciaDoDescumprimentoMaxLength = 30;
    private const int CodigoDoPrazoEDaValidadeMaxLength = 30;
    private const int UnidadeDoPrazoMaxLength = 10;

    private static readonly JsonSerializerOptions JsonOptions = OpcoesJsonDeDocumento.Criar(new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    public void Configure(EntityTypeBuilder<DocumentoExigido> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("documentos_exigidos", t =>
        {
            t.HasComment("Documentos que o processo seletivo exige do candidato: em que fase, para quem, o que restringe o arquivo e quando a análise recusa o documento.");
            t.HasCheckConstraint(
                "ck_documentos_exigidos_formatos_permitidos_coerente",
                "(formatos_permitidos_qualquer = true AND formatos_permitidos_lista IS NULL) OR " +
                "(formatos_permitidos_qualquer = false AND formatos_permitidos_lista IS NOT NULL " +
                "AND jsonb_array_length(formatos_permitidos_lista) > 0)");
            RestringirCodigosDosEnums(t);
        });
        builder.HasKey(d => d.Id);
        // Chave Guid v7 do domínio (EntityBase) — ValueGeneratedNever, mesmo padrão de
        // FaseCronograma/ModalidadeSelecionada.
        builder.Property(d => d.Id).ValueGeneratedNever().HasComment("Identificador da exigência de documento.");
        ConfigurarColunasDaExigencia(builder);
        ConfigurarRelacionamentos(builder);
        ConfigurarRequisitos(builder);
    }

    private static void ConfigurarColunasDaExigencia(EntityTypeBuilder<DocumentoExigido> builder)
    {
        builder.Property(d => d.Chave)
            .HasConversion<ChaveLocalValueConverter>()
            .HasColumnName("chave_local").HasMaxLength(ChaveLocal.ComprimentoMaximo).IsRequired()
            .HasComment("Identificador que a parte guarda dentro da configuração publicada; único no processo.");
        builder.HasIndex(d => new { d.ProcessoSeletivoId, d.Chave })
            .IsUnique().HasDatabaseName("ux_documentos_exigidos_processo_chave_local");
        builder.Property(d => d.ProcessoSeletivoId).HasComment("Processo seletivo que faz a exigência.");
        builder.Property(d => d.CreatedAt).HasComment("Data e hora em que a exigência foi criada.");
        builder.Property(d => d.UpdatedAt).HasComment("Data e hora da última alteração da exigência.");
        builder.Property(d => d.ExigidoNaFaseId).IsRequired().HasComment("Fase do cronograma do processo em que o documento é exigido.");
        builder.Property(d => d.TipoDocumentoOrigemId).IsRequired().HasComment("Tipo de documento do catálogo que a exigência pede, como estava ao gravar a exigência.");
        builder.Property(d => d.TipoDocumentoCodigo).HasMaxLength(TipoDocumentoCodigoMaxLength).IsRequired().HasComment("Código do tipo de documento, copiado do catálogo ao gravar a exigência.");
        builder.Property(d => d.TipoDocumentoNome).HasMaxLength(TipoDocumentoNomeMaxLength).IsRequired().HasComment("Nome do tipo de documento, copiado do catálogo ao gravar a exigência.");
        builder.Property(d => d.TipoDocumentoCategoria).HasMaxLength(TipoDocumentoCategoriaMaxLength).IsRequired().HasComment("Categoria do tipo de documento, copiada do catálogo ao gravar a exigência.");
        builder.Property(d => d.ClassificacaoProtecao)
            .HasMaxLength(ColunaDaClassificacao.MaxLength)
            .IsRequired()
            .HasComment("Classificação de proteção de dados do documento, copiada do tipo de documento.");
        builder.Property(d => d.FinalidadeTratamento).HasMaxLength(FinalidadeTratamentoMaxLength).IsRequired().HasComment("Finalidade do tratamento dos dados do documento, copiada do tipo de documento.");
        builder.Property(d => d.HipoteseLegal).HasMaxLength(HipoteseLegalMaxLength).IsRequired().HasComment("Hipótese legal do tratamento dos dados do documento, copiada do tipo de documento.");
        builder.Property(d => d.FatosComprovados).HasColumnName("dados_comprovados").IsRequired().HasComment("Códigos dos dados do candidato que o documento comprova; vazio quando a exigência não comprova dados.");
        builder.Property(d => d.Aplicabilidade).HasMaxLength(TamanhoDoCodigoDeContrato.Maximo).IsRequired()
            .HasComment("A quem a exigência se aplica: todos os candidatos ou só quem atende às condições da exigência.");
        builder.Property(d => d.AbrePrazoParaReenvio).HasComment("Indica se a exigência abre prazo para reenvio do documento.");
        builder.Property(d => d.Obrigatorio).IsRequired().HasComment("Indica se a falta do documento impede a inscrição de seguir.");
        builder.Property(d => d.ConsequenciaDoDescumprimento).HasMaxLength(ConsequenciaDoDescumprimentoMaxLength)
            .HasComment("O que acontece com o candidato quando o documento é indeferido; vazio quando a exigência não determina resultado.");
        // A etapa que coleta o documento, quando a fase se subdivide — opcional.
        builder.Property(d => d.ExigidoNaEtapaId).HasComment("Etapa da fase que coleta o documento; vazio quando o documento é da fase inteira.");
        // O formulário a que o documento pertence — nulo fora de fase de formulário. A coerência com a fase é do domínio.
        builder.Property(d => d.Finalidade).HasMaxLength(TamanhoDoCodigoDeContrato.Maximo)
            .HasComment("Formulário a que o documento pertence; vazio quando a fase não responde formulário.");
    }

    private static void ConfigurarRelacionamentos(EntityTypeBuilder<DocumentoExigido> builder)
    {
        // FK real para fases_cronograma — a checagem "fase viva do mesmo processo"
        // (ProcessoSeletivo.DefinirDocumentosExigidos) é check-then-act não-atômico; a
        // constraint do banco é a defesa realmente atômica (mesmo padrão de
        // FaseCronogramaConfiguration). Restrict, não Cascade: reconfigurar o cronograma
        // (DefinirCronogramaFases) não pode apagar silenciosamente uma fase ainda
        // referenciada por uma exigência viva — falha explícita.
        builder.HasOne<FaseCronograma>()
            .WithMany()
            .HasForeignKey(d => d.ExigidoNaFaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.ExigidoNaFaseId)
            .HasDatabaseName("ix_documentos_exigidos_exigido_na_fase_id");

        // FK Restrict pela mesma razão da fase: reconfigurar as etapas não pode apagar a
        // etapa que uma exigência viva aponta. O guard de domínio equivalente está em
        // DefinirEtapas (EtapaReferenciadaPorExigenciaDocumental); a constraint é a
        // defesa atômica que o check-then-act dele não dá.
        builder.HasOne<EtapaProcesso>()
            .WithMany()
            .HasForeignKey(d => d.ExigidoNaEtapaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.ExigidoNaEtapaId)
            .HasDatabaseName("ix_documentos_exigidos_exigido_na_etapa_id");

        // Gatilho DNF — substituível por inteiro junto com o próprio DocumentoExigido, mesmo
        // padrão de FaseCronograma/BancasRequeridas.
        builder.HasMany(d => d.Condicoes)
            .WithOne()
            .HasForeignKey(c => c.DocumentoExigidoId)
            .HasConstraintName("fk_condicoes_das_exigencias_documento_exigido")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(d => d.Condicoes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Base legal 1:N — substituível por inteiro junto com o próprio DocumentoExigido, mesmo
        // padrão de Condicoes.
        builder.HasMany(d => d.BasesLegais)
            .WithOne()
            .HasForeignKey(b => b.DocumentoExigidoId)
            .HasConstraintName("fk_documentos_exigidos_base_legal_documento_exigido")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(d => d.BasesLegais)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    // Os códigos gravados nas colunas dos enums; as colunas do prazo de emissão e da validade são anuláveis.
    private static void RestringirCodigosDosEnums(TableBuilder<DocumentoExigido> tabela)
    {
        tabela.HasCheckConstraint("ck_documentos_exigidos_aplicabilidade", $"aplicabilidade IN ({SqlDosCodigosDeContrato.Lista<Aplicabilidade>()})");
        RestringirCodigoOpcional<FinalidadeFormulario>(tabela, "finalidade");
        RestringirCodigoOpcional<ConsequenciaDoDescumprimento>(tabela, "consequencia_do_descumprimento");
        RestringirCodigoOpcional<FormatoDeDocumento>(tabela, "modelo_formato");
        RestringirCodigoOpcional<EscolhaDoPrazoDeEmissao>(tabela, "prazo_emissao_escolha");
        RestringirCodigoOpcional<UnidadeDoPrazoDeEmissao>(tabela, "prazo_emissao_unidade");
        RestringirCodigoOpcional<EfeitoNaAnaliseDoDocumento>(tabela, "prazo_emissao_efeito");
        RestringirCodigoOpcional<TipoDaDataDeReferencia>(tabela, "prazo_emissao_referencia_tipo");
        RestringirCodigoOpcional<EscolhaDaValidade>(tabela, "validade_escolha");
        RestringirCodigoOpcional<EfeitoNaAnaliseDoDocumento>(tabela, "validade_efeito");
        RestringirCodigoOpcional<TipoDaDataDeReferencia>(tabela, "validade_referencia_tipo");
    }

    private static void RestringirCodigoOpcional<TEnum>(TableBuilder<DocumentoExigido> tabela, string coluna)
        where TEnum : struct, Enum =>
        tabela.HasCheckConstraint($"ck_documentos_exigidos_{coluna}", $"{coluna} IS NULL OR {coluna} IN ({SqlDosCodigosDeContrato.Lista<TEnum>()})");

    private static void ConfigurarRequisitos(EntityTypeBuilder<DocumentoExigido> builder)
    {
        builder.OwnsOne(d => d.Requisitos, requisitos =>
        {
            requisitos.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            ConfigurarFormatosPermitidos(requisitos);
            requisitos.Property(d => d.TamanhoMaximoBytes).HasColumnName("tamanho_maximo_bytes")
                .HasComment("Tamanho máximo do arquivo do documento, em bytes; vazio quando não há limite.");
            ConfigurarModelo(requisitos);
            requisitos.OwnsOne(d => d.PrazoDeEmissao, ConfigurarPrazoDeEmissao);
            requisitos.OwnsOne(d => d.Validade, ConfigurarValidade);
        });
        builder.Navigation(d => d.Requisitos).IsRequired();
    }

    // Duas colunas: `qualquer` (bool NOT NULL) e `lista` (jsonb, não-nula ⟺ !qualquer) — CHECK de
    // coerência na tabela (ck_documentos_exigidos_formatos_permitidos_coerente).
    private static void ConfigurarFormatosPermitidos(OwnedNavigationBuilder<DocumentoExigido, RequisitosDoDocumento> requisitos)
    {
        requisitos.OwnsOne(d => d.FormatosPermitidos, formatos =>
        {
            formatos.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            formatos.Property(f => f.Qualquer).HasColumnName("formatos_permitidos_qualquer").IsRequired()
                .HasComment("Indica se o documento pode ser enviado em qualquer formato de arquivo.");
            formatos.Property(f => f.Lista)
                .HasColumnName("formatos_permitidos_lista")
                .HasConversion(FormatosPermitidosListaConverter, FormatosPermitidosListaComparer)
                .HasColumnType("jsonb")
                .HasComment("Lista dos formatos de arquivo aceitos, cada um com o limite de tamanho próprio quando houver; vazia quando qualquer formato é aceito.");
        });
        requisitos.Navigation(d => d.FormatosPermitidos).IsRequired();
    }

    // Cópia por valor do modelo confirmado, como o tipo de documento: colunas nullable, 0..1 por exigência, sem FK,
    // porque o modelo confirmado é imutável e não tem remoção.
    private static void ConfigurarModelo(OwnedNavigationBuilder<DocumentoExigido, RequisitosDoDocumento> requisitos)
    {
        requisitos.OwnsOne(d => d.Modelo, modelo =>
        {
            modelo.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            modelo.Property(m => m.ModeloId).HasColumnName("modelo_id").HasComment("Modelo de documento que a exigência oferece ao candidato; vazio quando não há modelo.");
            modelo.Property(m => m.NomeArquivo).HasColumnName("modelo_nome_arquivo").HasMaxLength(ModeloDeDocumento.NomeArquivoMaxLength)
                .HasComment("Nome do arquivo do modelo de documento, copiado ao gravar a exigência.");
            modelo.Property(m => m.Formato).HasColumnName("modelo_formato").HasMaxLength(4)
                .HasComment("Formato do arquivo do modelo, copiado ao gravar a exigência.");
            modelo.Property(m => m.HashSha256).HasColumnName("modelo_hash_sha256").HasMaxLength(64)
                .HasComment("Resumo SHA-256 do arquivo do modelo, copiado ao gravar a exigência.");
        });
    }

    // Escolha sobre o prazo de emissão e seus detalhes, todos anuláveis: a coerência entre eles é do
    // PrazoDeEmissao, sem CHECK nem chave estrangeira no banco.
    private static void ConfigurarPrazoDeEmissao(OwnedNavigationBuilder<RequisitosDoDocumento, PrazoDeEmissao> prazo)
    {
        prazo.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        prazo.Property(p => p.Escolha).HasColumnName("prazo_emissao_escolha").HasMaxLength(CodigoDoPrazoEDaValidadeMaxLength)
            .HasComment("Escolha sobre o prazo de emissão do documento; vazio enquanto a escolha não foi feita.");
        prazo.Property(p => p.Valor).HasColumnName("prazo_emissao_valor")
            .HasComment("Quantidade de dias, meses ou anos do prazo de emissão, maior que zero; preenchida só quando a escolha é COM_PRAZO.");
        prazo.Property(p => p.Unidade).HasColumnName("prazo_emissao_unidade").HasMaxLength(UnidadeDoPrazoMaxLength)
            .HasComment("Unidade do prazo de emissão; preenchida só quando a escolha é COM_PRAZO.");
        prazo.Property(p => p.EfeitoForaDoPrazo).HasColumnName("prazo_emissao_efeito").HasMaxLength(CodigoDoPrazoEDaValidadeMaxLength)
            .HasComment("O que a análise faz com o documento emitido fora do prazo; preenchida só quando a escolha é COM_PRAZO.");
        prazo.OwnsOne(p => p.Referencia, referencia => ConfigurarReferencia(referencia, "prazo_emissao", "do prazo de emissão", "COM_PRAZO"));
    }

    private static void ConfigurarValidade(OwnedNavigationBuilder<RequisitosDoDocumento, ValidadeDoDocumento> validade)
    {
        validade.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        validade.Property(v => v.Escolha).HasColumnName("validade_escolha").HasMaxLength(CodigoDoPrazoEDaValidadeMaxLength)
            .HasComment("Escolha sobre a validade do documento; vazio enquanto a escolha não foi feita.");
        validade.Property(v => v.EfeitoDoVencimento).HasColumnName("validade_efeito").HasMaxLength(CodigoDoPrazoEDaValidadeMaxLength)
            .HasComment("O que a análise faz com o documento vencido; preenchida só quando a escolha é EXIGE_DOCUMENTO_VALIDO.");
        validade.OwnsOne(v => v.Referencia, referencia => ConfigurarReferencia(referencia, "validade", "da validade", "EXIGE_DOCUMENTO_VALIDO"));
    }

    private static void ConfigurarReferencia<TDono>(
        OwnedNavigationBuilder<TDono, DataDeReferenciaDoDocumento> referencia, string prefixo, string deQuem, string escolhaQuePreenche)
        where TDono : class
    {
        referencia.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        referencia.Property(r => r.Tipo).HasColumnName($"{prefixo}_referencia_tipo").HasMaxLength(CodigoDoPrazoEDaValidadeMaxLength)
            .HasComment($"Data de referência {deQuem}; preenchida só quando a escolha é {escolhaQuePreenche}.");
        referencia.Property(r => r.Data).HasColumnName($"{prefixo}_referencia_data")
            .HasComment($"Data específica usada como referência {deQuem}; preenchida só quando o tipo da data de referência é DATA_ESPECIFICA.");
        referencia.Property(r => r.FaseId).HasColumnName($"{prefixo}_referencia_fase_id")
            .HasComment($"Fase do cronograma cujo início ou fim é a referência {deQuem}; preenchida só quando o tipo da data de referência é INICIO_DA_FASE ou FIM_DA_FASE.");
    }

    private static readonly ValueConverter<IReadOnlyList<FormatoPermitidoEntry>?, string?> FormatosPermitidosListaConverter =
        new(
            lista => SerializeFormatosPermitidosLista(lista),
            json => DeserializeFormatosPermitidosLista(json));

    private static readonly ValueComparer<IReadOnlyList<FormatoPermitidoEntry>?> FormatosPermitidosListaComparer =
        new(
            (a, b) => SerializeFormatosPermitidosLista(a) == SerializeFormatosPermitidosLista(b),
            v => v == null ? 0 : SerializeFormatosPermitidosLista(v)!.GetHashCode(StringComparison.Ordinal),
            v => DeserializeFormatosPermitidosLista(SerializeFormatosPermitidosLista(v)));

    private static string? SerializeFormatosPermitidosLista(IReadOnlyList<FormatoPermitidoEntry>? lista) =>
        lista is null
            ? null
            : JsonSerializer.Serialize(
                lista.Select(static e => new FormatoPermitidoEntryJson(e.Formato.Codigo(), e.TamanhoMaximoBytesMax)),
                JsonOptions);

    /// <summary>
    /// Reidrata direto do jsonb já validado na escrita (<see cref="FormatosPermitidos.Criar"/>) —
    /// mesmo raciocínio de <c>DocumentoExigido.Reidratar</c>: revalidar aqui é defesa em
    /// profundidade contra dado corrompido, não uma segunda fonte de verdade.
    /// </summary>
    private static List<FormatoPermitidoEntry>? DeserializeFormatosPermitidosLista(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        List<FormatoPermitidoEntryJson>? itens = ObjetoDeValorGravado.DesserializarDocumento(
            () => JsonSerializer.Deserialize<List<FormatoPermitidoEntryJson>>(json, JsonOptions),
            nameof(DocumentoExigido.FormatosPermitidos));
        return itens is null ? null : [.. itens.Select(static item => EntradaDoFormato(item))];
    }

    private static FormatoPermitidoEntry EntradaDoFormato(FormatoPermitidoEntryJson item) =>
        ObjetoDeValorGravado.Reidratar(
            FormatoPermitidoEntry.Criar(CatalogoDe<FormatoPermitido>.Obter(item.Formato), item.TamanhoMaximoBytesMax),
            nameof(FormatoPermitidoEntry));

    private sealed record FormatoPermitidoEntryJson(string Formato, int? TamanhoMaximoBytesMax);
}
