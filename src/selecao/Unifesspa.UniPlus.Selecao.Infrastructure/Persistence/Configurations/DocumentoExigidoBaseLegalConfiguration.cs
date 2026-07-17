namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Domain.Entities;
using Domain.Enums;

/// <summary>
/// Configuração EF Core de <see cref="DocumentoExigidoBaseLegal"/> (Story #554, PR-c,
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

        builder.ToTable("documentos_exigidos_base_legal");
        builder.HasKey(b => b.Id);
        // Chave Guid v7 do domínio (EntityBase) — ValueGeneratedNever, mesmo padrão de
        // DocumentoExigido/CondicaoGatilho.
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.Referencia).HasMaxLength(ReferenciaMaxLength).IsRequired();

        // Mesmo mapeamento canônico de OperadorCodigo/ReferenciaTipoCodigo usado no wire —
        // nunca enum.ToString() cru: a coluna carrega o mesmo token FEDERAL/ESTADUAL/.../
        // PENDENTE/RESOLVIDO que o PUT aceita e o GET devolve.
        builder.Property(b => b.Abrangencia)
            .HasConversion(AbrangenciaConverter)
            .HasMaxLength(AbrangenciaCodigoMaxLength)
            .IsRequired();

        builder.Property(b => b.Status)
            .HasConversion(StatusConverter)
            .HasMaxLength(StatusCodigoMaxLength)
            .IsRequired();

        builder.Property(b => b.Observacao).HasMaxLength(ObservacaoMaxLength);
    }

    private static readonly ValueConverter<TipoAbrangencia, string> AbrangenciaConverter =
        new(abrangencia => abrangencia.ToCodigo(), codigo => TipoAbrangenciaCodigo.FromCodigo(codigo));

    private static readonly ValueConverter<StatusBaseLegal, string> StatusConverter =
        new(status => status.ToCodigo(), codigo => StatusBaseLegalCodigo.FromCodigo(codigo));
}
