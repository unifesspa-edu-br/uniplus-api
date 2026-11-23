namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Configuração EF Core de <see cref="DocumentoExigidoBaseLegal"/> (Story #554, PR #898,
/// issue #549) — entidade filha de <see cref="DocumentoExigido"/>, <c>EntityBase</c> puro
/// (sem soft-delete), mesmo padrão de <see cref="CondicaoGatilho"/>. Sem CHECK de banco
/// para <c>tipo_abrangencia</c>/<c>status</c> — integridade em C# (enum tipado +
/// <c>.HasConversion&lt;string&gt;()</c> + guard de factory + FluentValidation no
/// comando de substituição integral), mesmo padrão do restante do módulo.
/// </summary>
public sealed class DocumentoExigidoBaseLegalConfiguration : IEntityTypeConfiguration<DocumentoExigidoBaseLegal>
{
    private const int ReferenciaMaxLength = 500;
    private const int AbrangenciaCodigoMaxLength = 20;
    private const int StatusCodigoMaxLength = 20;
    private const int ObservacaoMaxLength = 1000;

    public void Configure(EntityTypeBuilder<DocumentoExigidoBaseLegal> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("documentos_exigidos_base_legal", tabela => tabela.HasComment("Bases legais das exigências de documentos."));
        builder.HasKey(b => b.Id);
        // Chave Guid v7 do domínio (EntityBase) — ValueGeneratedNever, mesmo padrão de
        // DocumentoExigido/CondicaoGatilho.
        builder.Property(b => b.Id).ValueGeneratedNever().HasComment("Identificador da base legal da exigência.");
        builder.Property(b => b.DocumentoExigidoId).HasComment("Exigência de documento à qual a base legal pertence.");
        builder.Property(b => b.CreatedAt).HasComment("Data e hora em que a base legal foi criada.");
        builder.Property(b => b.UpdatedAt).HasComment("Data e hora da última alteração da base legal.");

        builder.Property(b => b.Referencia).HasMaxLength(ReferenciaMaxLength).IsRequired().HasComment("Referência da base legal da exigência.");

        // Mesmo mapeamento canônico de ReferenciaTipoCodigo usado no wire —
        // nunca enum.ToString() cru: a coluna carrega o mesmo token FEDERAL/ESTADUAL/.../
        // PENDENTE/RESOLVIDO que o PUT aceita e o GET devolve.
        builder.Property(b => b.Abrangencia)
            .HasConversion(AbrangenciaConverter)
            .HasMaxLength(AbrangenciaCodigoMaxLength)
            .IsRequired().HasComment("Abrangência da base legal.");

        builder.Property(b => b.Situacao)
            .HasConversion(StatusConverter)
            .HasMaxLength(StatusCodigoMaxLength)
            .IsRequired().HasComment("Situação da base legal.");

        builder.Property(b => b.Observacao).HasMaxLength(ObservacaoMaxLength).HasComment("Observação sobre a base legal, quando informada.");
    }

    private static readonly ValueConverter<TipoAbrangencia, string> AbrangenciaConverter =
        new(abrangencia => abrangencia.ToCodigo(), codigo => TipoAbrangenciaCodigo.FromCodigo(codigo));

    private static readonly ValueConverter<StatusBaseLegal, string> StatusConverter =
        new(status => status.ToCodigo(), codigo => StatusBaseLegalCodigo.FromCodigo(codigo));
}
