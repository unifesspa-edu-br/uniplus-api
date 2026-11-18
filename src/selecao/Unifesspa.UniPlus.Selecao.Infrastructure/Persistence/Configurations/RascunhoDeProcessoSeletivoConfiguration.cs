namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;
using Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class RascunhoDeProcessoSeletivoConfiguration : IEntityTypeConfiguration<RascunhoDeProcessoSeletivo>
{
    public void Configure(EntityTypeBuilder<RascunhoDeProcessoSeletivo> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("rascunhos_processos_seletivos", tabela =>
        {
            tabela.HasCheckConstraint("ck_rascunhos_processos_seletivos_revisao", "revisao >= 1");
            tabela.HasCheckConstraint("ck_rascunhos_processos_seletivos_documento_objeto", "jsonb_typeof(documento) = 'object'");
        });
        builder.HasKey(r => r.Id).HasName("pk_rascunhos_processos_seletivos");
        // Guid v7 gerado no domínio (EntityBase): é o identificador que o processo herda na publicação.
        builder.Property(r => r.Id).ValueGeneratedNever();

        // Sem token de concorrência: quem coordena os editores é a trava da linha e o ETag da revisão.
        builder.Property(r => r.Revisao).IsRequired();

        // Escalar sem FK para a unidade, que vive em outro módulo (ADR-0061).
        builder.Property(r => r.UnidadeAdministradoraOrigemId)
            .IsRequired()
            .HasComment("Id da Unidade administradora em Organização Institucional (ADR-0061, sem FK cross-schema): decide quem lê, grava, exclui e publica o rascunho.");

        builder.Property(r => r.Documento)
            .ClassificaComo(ClassificacaoDeProtecao.Pessoal)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasComment("Documento da configuração. Traz o identificador da conta de quem assina (dado pessoal pseudonimizado); o formato não prevê nome, CPF nem e-mail, mas o texto livre de campos como o dos assinantes é guardado como veio.");
        builder.Property(r => r.Nome).HasMaxLength(DocumentoDoRascunho.NomeMaxLength);

        // A consulta por unidade dona entrega a ordem do identificador (Guid v7, que segue a criação).
        builder.HasIndex(r => new { r.UnidadeAdministradoraOrigemId, r.Id })
            .HasDatabaseName("ix_rascunhos_processos_seletivos_unidade_id");
    }
}
