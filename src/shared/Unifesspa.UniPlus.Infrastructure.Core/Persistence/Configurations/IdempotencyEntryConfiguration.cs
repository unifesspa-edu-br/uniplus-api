namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

internal sealed class IdempotencyEntryConfiguration : IEntityTypeConfiguration<IdempotencyEntry>
{
    private const int CodigoDeContratoMaxLength = 30;

    public void Configure(EntityTypeBuilder<IdempotencyEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("chaves_de_idempotencia", static tabela => tabela.HasCheckConstraint(
            "ck_chaves_de_idempotencia_status",
            $"status IN ({SqlDosCodigosDeContrato.Lista<IdempotencyStatus>()})"));

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasComment("Identificador da chave de idempotência.");

        // O escopo da chave de idempotência é user:{conta}: identifica quem agiu.
        builder.Property(e => e.Scope)
            .HasColumnName("escopo")
            .HasComment("Quem agiu: a conta autenticada, no formato user:{conta}. A chave só vale dentro desse escopo.")
            .ClassificaComo(ClassificacaoDeProtecao.Pessoal)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.Endpoint)
            .HasColumnName("rota")
            .HasComment("Operação pedida, no formato MÉTODO /modelo/da/rota (modelo da rota, não o endereço com os valores).")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.IdempotencyKey)
            .HasColumnName("chave")
            .HasComment("Chave de idempotência enviada pelo cliente no cabeçalho Idempotency-Key, sem alteração.")
            .HasMaxLength(255)
            .IsRequired();

        // SHA-256 sem sal de um corpo que pode trazer CPF: dá para confirmar um palpite, então vale a classe do corpo.
        builder.Property(e => e.BodyHash)
            .HasColumnName("resumo_do_corpo")
            .HasComment("Resumo SHA-256 do corpo do pedido, em hexadecimal; serve para detectar a mesma chave usada com outro corpo.")
            .ClassificaComo(ClassificacaoDeProtecao.Sensivel)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();

        builder.Property(e => e.Status)
            .HasMaxLength(CodigoDeContratoMaxLength)
            .HasComment("Situação da chave durante a execução e após guardar a resposta para repetição.")
            .IsRequired();

        builder.Property(e => e.ResponseStatus)
            .HasColumnName("situacao_http_da_resposta")
            .HasComment("Código de situação HTTP da resposta guardada; vazio enquanto a operação ainda executa.");

        // O corpo e os cabeçalhos cacheados são a resposta de qualquer endpoint idempotente e podem trazer dado de qualquer classe.
        builder.Property(e => e.EncryptedResponseBody)
            .HasColumnName("corpo_da_resposta_cifrado")
            .HasComment("Corpo da resposta guardado cifrado, devolvido igual quando o pedido se repete; vazio enquanto a operação executa.")
            .ClassificaComo(ClassificacaoDeProtecao.Sensivel);

        builder.Property(e => e.ResponseHeadersJson)
            .HasColumnName("cabecalhos_da_resposta")
            .HasComment("Cabeçalhos da resposta guardada (Content-Type e Location), em JSON; vazio enquanto a operação executa.")
            .ClassificaComo(ClassificacaoDeProtecao.Sensivel)
            .HasColumnType("jsonb");

        builder.Property(e => e.ExpiresAt)
            .HasColumnName("expira_em")
            .HasComment("Instante a partir do qual a chave deixa de valer e pode ser descartada.")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasComment("Instante em que a chave foi reservada.")
            .IsRequired();

        // Índice único — garante que duas requests concorrentes com a mesma
        // (escopo, rota, chave) não consigam reservar duas entries.
        // DbUpdateException por violação dessa unique é o sinal de retry
        // concorrente (tratado pelo filter com aguarda + replay).
        builder.HasIndex(e => new { e.Scope, e.Endpoint, e.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("ux_chaves_de_idempotencia_escopo_rota_chave");

        // Suporta job de cleanup por TTL (não implementado nesta story; ADR-0027
        // §"Esta ADR não decide" — política de cleanup fica aberta).
        builder.HasIndex(e => e.ExpiresAt)
            .HasDatabaseName("ix_chaves_de_idempotencia_expira_em");
    }
}
