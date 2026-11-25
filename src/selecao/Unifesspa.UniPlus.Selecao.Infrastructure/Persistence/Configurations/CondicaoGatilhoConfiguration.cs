namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Text.Json;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Configuração EF Core de <see cref="CondicaoGatilho"/> — entidade
/// filha de <see cref="DocumentoExigido"/>, <c>EntityBase</c> puro (sem soft-delete).
/// </summary>
public sealed class CondicaoGatilhoConfiguration : IEntityTypeConfiguration<CondicaoGatilho>
{
    private const int FatoMaxLength = FatoDoCatalogo.CodigoMaxLength;

    public void Configure(EntityTypeBuilder<CondicaoGatilho> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.Property(c => c.DocumentoExigidoId).HasComment("Identificador do documento exigido ao qual a condição pertence.");
        builder.Property(c => c.Clausula).HasComment("Número da alternativa que reúne condições atendidas em conjunto.");
        builder.Property(c => c.Fato).HasComment("Código do dado do candidato usado na condição. Exemplos: RENDA_PER_CAPITA, CONCORRER_RENDA.");
        builder.Property(c => c.Operador).HasComment("Comparação aplicada ao valor.");
        builder.Property(c => c.Valor).HasComment("Valor de comparação da condição, em JSON.");

        builder.ToTable("condicoes_das_exigencias", t => t.HasComment("Condições que determinam a exigência de um documento."));
        builder.HasKey(c => c.Id);
        // Chave Guid v7 do domínio (EntityBase) — ValueGeneratedNever, mesmo padrão de
        // DocumentoExigido/FaseCronograma.
        builder.Property(c => c.Id).ValueGeneratedNever().HasComment("Identificador da condição da exigência.");
        builder.Property(c => c.CreatedAt).HasComment("Data e hora em que a condição foi criada.");
        builder.Property(c => c.UpdatedAt).HasComment("Data e hora da última alteração da condição.");

        builder.Property(c => c.Clausula).HasColumnName("alternativa").IsRequired();
        builder.Property(c => c.Fato).HasColumnName("codigo_do_dado").HasMaxLength(FatoMaxLength).IsRequired();

        // A convenção do modelo grava o código declarado no enum (ADR-0176), o mesmo token
        // ALGUM_DE/DIFERENTE/IGUAL/MAIOR_IGUAL/MENOR_IGUAL/NENHUM_DE do wire; aqui só o tamanho da coluna.
        builder.Property(c => c.Operador)
            .HasMaxLength(OperadorCodigoMaxLength)
            .IsRequired();

        // Mesmo padrão jsonb de RegraCatalogoConfiguration (DeclaracaoDaRegra/Invariantes) —
        // serializa pelo texto bruto do JsonElement, reidrata desanexado do JsonDocument
        // de origem.
        builder.Property(c => c.Valor)
            .HasConversion(JsonElementConverter, JsonElementComparer)
            .HasColumnType("jsonb")
            .IsRequired();
    }

    private const int OperadorCodigoMaxLength = 20;

    private static readonly ValueConverter<JsonElement, string> JsonElementConverter =
        new(element => element.GetRawText(), json => Parse(json));

    private static readonly ValueComparer<JsonElement> JsonElementComparer =
        new(
            (a, b) => a.GetRawText() == b.GetRawText(),
            v => v.GetRawText().GetHashCode(StringComparison.Ordinal),
            v => Parse(v.GetRawText()));

    private static JsonElement Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
