namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

// Convenções de mapeamento dos Value Objects para o EF Core.
// Chamado no override `ConfigureConventions` dos DbContexts dos módulos
// (Selecao, Ingresso, Portal, e os módulos da Sprint 3) para que toda
// propriedade tipada como Cpf/Email/NomeSocial/NotaFinal/AreaCodigo receba
// o converter e as restrições de coluna apropriadas (tipo, tamanho máximo,
// precisão).
//
// IMPORTANTE — incompatível com `OwnsOne` para os mesmos VOs:
// se um IEntityTypeConfiguration<T> existente mapeia Cpf/Email/NomeSocial
// via `builder.OwnsOne(c => c.Cpf, ...)`, esse mapeamento deve ser
// substituído por `builder.Property(c => c.Cpf)` antes de ativar esta
// convenção no contexto. EF Core não permite o mesmo tipo CLR ser
// owned-type e scalar-with-converter simultaneamente.
//
// Hoje, módulos ainda usam OwnsOne (ex.: CandidatoConfiguration em
// Selecao). A adoção desta convenção será incremental, módulo a módulo,
// junto com a migração do schema. Os converters podem ser usados de
// forma isolada via HasConversion<T>() em IEntityTypeConfiguration sem
// precisar acionar a convenção global.
public static class ValueObjectConventions
{
    // Limite prático RFC 5321 para endereços de e-mail.
    private const int EmailMaxLength = 254;

    // Cpf é sempre 11 dígitos após normalização pelo VO. O tipo escolhido
    // é `varchar(11)` (não `char(11)`) porque Postgres faz padding com
    // espaços ao recuperar `char(N)`, o que corromperia o valor lido
    // ("12345678901 " em vez de "12345678901"). `varchar(11)` enforça o
    // limite sem padding e mantém performance equivalente desde
    // Postgres 11 (sem diferença de armazenamento).
    private const int CpfLength = 11;

    // (9,4) acomoda notas até 99.999,9999 com 4 casas de precisão — escala
    // alinhada ao Math.Round(valor, 4) aplicado no construtor do VO.
    private const int NotaFinalPrecision = 9;
    private const int NotaFinalScale = 4;

    // AreaCodigo é normalizado para 2-32 caracteres uppercase ASCII pelo VO
    // (ADR-0055). `varchar(32)` enforça o limite no schema.
    private const int AreaCodigoMaxLength = 32;

    public static void ConfigureValueObjectConverters(this ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<Cpf>()
            .HaveConversion<CpfValueConverter>()
            .HaveColumnType($"varchar({CpfLength})")
            .HaveMaxLength(CpfLength);

        configurationBuilder.Properties<Email>()
            .HaveConversion<EmailValueConverter>()
            .HaveMaxLength(EmailMaxLength);

        configurationBuilder.Properties<NomeSocial>()
            .HaveConversion<NomeSocialValueConverter>()
            .HaveColumnType("jsonb");

        configurationBuilder.Properties<NotaFinal>()
            .HaveConversion<NotaFinalValueConverter>()
            .HavePrecision(NotaFinalPrecision, NotaFinalScale);

        configurationBuilder.Properties<AreaCodigo>()
            .HaveConversion<AreaCodigoValueConverter>()
            .HaveColumnType($"varchar({AreaCodigoMaxLength})")
            .HaveMaxLength(AreaCodigoMaxLength);
    }
}
