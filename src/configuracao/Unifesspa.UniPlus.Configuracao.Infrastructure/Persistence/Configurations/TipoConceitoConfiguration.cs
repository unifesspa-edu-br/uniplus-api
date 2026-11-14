namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

internal sealed class TipoConceitoConfiguration : IEntityTypeConfiguration<TipoConceito>
{
    private const int CodigoMaxLength = 64;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;
    private const int NaturezaMaxLength = 20;

    public void Configure(EntityTypeBuilder<TipoConceito> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        builder.ToTable("tipos_conceito", ConfigurarTabela);
        builder.HasKey(tipo => tipo.Id);
        builder.Property(tipo => tipo.Id).ValueGeneratedNever().HasComment("Identificador do tipo de conceito (UUIDv7).");
        ConfigurarIdentificacao(builder);
        ConfigurarNaturezaEEscala(builder);
        ConfigurarAuditoria(builder);
        ConfigurarValores(builder);

        // A unicidade abrange também os tipos desativados: o código nunca é reutilizado.
        builder.HasIndex(tipo => tipo.Codigo).IsUnique().HasDatabaseName("ix_tipos_conceito_codigo");
        builder.HasIndex(tipo => tipo.Ativo).HasDatabaseName("ix_tipos_conceito_ativo");
    }

    private static void ConfigurarTabela(TableBuilder<TipoConceito> tabela)
    {
        tabela.HasComment("Tipos de conceito com que uma etapa avalia: numéricos, medidos numa escala, ou categóricos, escolhidos entre valores ordenados.");
        string tokens = string.Join(", ", NaturezasConceito.TokensCanonicos.Select(static token => $"'{token}'"));
        tabela.HasCheckConstraint("ck_tipos_conceito_natureza", $"natureza IN ({tokens})");

        // O numérico traz a escala inteira e o categórico não traz nenhum dos três campos da escala.
        tabela.HasCheckConstraint(
            "ck_tipos_conceito_parametros_da_natureza",
            "(natureza = 'NUMERICO' AND escala_minima IS NOT NULL AND escala_maxima IS NOT NULL AND casas_decimais IS NOT NULL "
            + "AND escala_minima < escala_maxima AND casas_decimais BETWEEN 0 AND 4) "
            + "OR (natureza = 'CATEGORICO' AND escala_minima IS NULL AND escala_maxima IS NULL AND casas_decimais IS NULL)");
    }

    private static void ConfigurarIdentificacao(EntityTypeBuilder<TipoConceito> builder)
    {
        builder.Property(tipo => tipo.Codigo).HasMaxLength(CodigoMaxLength).IsRequired()
            .HasComment("Chave natural do tipo, em NFC e imutável; único entre todos os tipos, inclusive os desativados.");
        builder.Property(tipo => tipo.Nome).HasMaxLength(NomeMaxLength).IsRequired()
            .HasComment("Nome do tipo de conceito, em NFC.");
        builder.Property(tipo => tipo.Descricao).HasMaxLength(DescricaoMaxLength)
            .HasComment("Descrição livre do tipo, em NFC. Nula quando não informada.");
        builder.Property(tipo => tipo.Ativo).IsRequired()
            .HasComment("Falso quando o tipo foi desativado: deixa de ser oferecido, e o código continua reservado.");

        // Token de concorrência otimista na coluna de sistema `xmin` do Postgres (shadow property
        // `uint` + IsRowVersion, sem coluna nem migration própria). O PUT substitui os valores
        // categóricos; sem o token, duas edições concorrentes do mesmo tipo gravariam sem saber uma
        // da outra. Quem confirma por último recebe DbUpdateConcurrencyException (ADR-0119).
        builder.Property<uint>("Version").IsRowVersion();
    }

    private static void ConfigurarNaturezaEEscala(EntityTypeBuilder<TipoConceito> builder)
    {
        builder.Property(tipo => tipo.Natureza)
            .HasConversion<NaturezaConceitoValueConverter>()
            .HasMaxLength(NaturezaMaxLength).IsRequired()
            .HasComment("Natureza do conceito: NUMERICO, medido na escala desta linha, ou CATEGORICO, escolhido entre os valores de valores_tipo_conceito.");
        builder.Property(tipo => tipo.EscalaMinima).HasColumnType("numeric")
            .HasComment("Menor valor da escala. Só no tipo numérico.");
        builder.Property(tipo => tipo.EscalaMaxima).HasColumnType("numeric")
            .HasComment("Maior valor da escala, sempre acima do menor. Só no tipo numérico.");
        builder.Property(tipo => tipo.CasasDecimais)
            .HasComment("Número de casas decimais da escala, de 0 a 4. Só no tipo numérico.");
    }

    private static void ConfigurarAuditoria(EntityTypeBuilder<TipoConceito> builder)
    {
        builder.Property(tipo => tipo.CreatedAt).HasComment("Instante em que o tipo foi criado.");
        builder.Property(tipo => tipo.UpdatedAt).HasComment("Instante da última alteração. Nulo enquanto o tipo não foi alterado.");
        builder.Property(tipo => tipo.CreatedBy).HasMaxLength(255).HasComment("Quem criou o tipo.");
        builder.Property(tipo => tipo.UpdatedBy).HasMaxLength(255).HasComment("Quem fez a última alteração. Nulo enquanto o tipo não foi alterado.");
    }

    // Os valores ficam numa tabela filha, substituídos em bloco a cada edição; a chave própria deixa o
    // EF apagar as linhas antigas e inserir as novas sem tratar a troca como alteração de chave. O agregado
    // expõe os valores ordenados pela posição; o EF lê e grava o campo.
    private static void ConfigurarValores(EntityTypeBuilder<TipoConceito> builder)
    {
        builder.OwnsMany(tipo => tipo.Valores, valor =>
        {
            valor.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            valor.ToTable("valores_tipo_conceito", ConfigurarTabelaDosValores);
            valor.WithOwner().HasForeignKey("TipoConceitoId");
            valor.Property<int>("Id").ValueGeneratedOnAdd().HasComment("Identificador técnico da linha; os valores são substituídos em bloco a cada edição.");
            valor.HasKey("Id");
            valor.Property<Guid>("TipoConceitoId").HasComment("Tipo de conceito categórico a que o valor pertence.");
            valor.Property(v => v.Codigo).HasMaxLength(50).IsRequired()
                .HasComment("Código do valor (maiúsculas, dígitos e sublinhado), único dentro do tipo.");
            valor.Property(v => v.Rotulo).HasMaxLength(100).IsRequired()
                .HasComment("Rótulo exibido ao avaliador, único dentro do tipo.");
            valor.Property(v => v.ContaComoAprovacao).IsRequired()
                .HasComment("Verdadeiro quando escolher este valor conta como aprovação. Um tipo pode não ter nenhum valor assim.");
            valor.Property(v => v.Posicao).IsRequired()
                .HasComment("Lugar do valor na lista do tipo, a partir de 1; é a única expressão da ordem.");
            valor.HasIndex("TipoConceitoId", nameof(ValorCategorico.Codigo)).IsUnique().HasDatabaseName(RestricoesDoBanco.TipoDeConceitoValorCodigoUnico);
            valor.HasIndex("TipoConceitoId", nameof(ValorCategorico.Rotulo)).IsUnique().HasDatabaseName(RestricoesDoBanco.TipoDeConceitoValorRotuloUnico);
            valor.HasIndex("TipoConceitoId", nameof(ValorCategorico.Posicao)).IsUnique().HasDatabaseName(RestricoesDoBanco.TipoDeConceitoValorPosicaoUnica);
        });

        builder.Navigation(tipo => tipo.Valores).HasField("_valores").UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigurarTabelaDosValores(OwnedNavigationTableBuilder<TipoConceito, ValorCategorico> tabela)
    {
        tabela.HasComment("Valores de um tipo de conceito categórico, na ordem em que foram informados.");
        tabela.HasCheckConstraint("ck_valores_tipo_conceito_codigo", "codigo ~ '^[A-Z][A-Z0-9_]*$'");
        tabela.HasCheckConstraint("ck_valores_tipo_conceito_posicao", "posicao >= 1");
    }
}
