namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O mapeamento do que a configuração de vagas guarda do quadro, na forma do ramo: as vagas ofertadas base e
/// o percentual, a conferência da reserva legal (só na Lei de Cotas), a regra de ajuste do quadro e as vagas retiradas
/// no ajuste (só no quadro fixado no edital), e as restrições que impedem a gravação de um quadro com a forma
/// de um ramo e o campo do outro.
/// </summary>
internal static class QuadroDeVagasDaConfiguracao
{
    private const int PrecisaoDoPercentual = 5;
    private const int EscalaDoPercentual = 4;
    private const string ColunasDaReservaLegalAusentes =
        "percentual_da_reserva_legal IS NULL AND reserva_legal_nominal IS NULL AND reserva_legal_efetiva IS NULL AND excedente_sobre_reserva_legal IS NULL AND vagas_cortadas_por_escassez IS NULL";
    private const string ColunasDaReservaLegalPresentes =
        "percentual_da_reserva_legal IS NOT NULL AND reserva_legal_nominal IS NOT NULL AND reserva_legal_efetiva IS NOT NULL AND excedente_sobre_reserva_legal IS NOT NULL AND vagas_cortadas_por_escassez IS NOT NULL";

    private static readonly JsonSerializerOptions OpcoesJson = OpcoesJsonDeDocumento.Criar(new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        WriteIndented = false,
    });

    // O EF não chama o conversor para nulo: a coluna nula é a ausência da regra de ajuste do quadro e das retiradas.
    private static readonly ValueConverter<ParametrosRegraAjusteDistribuicao?, string?> ConversorDoAjuste =
        new(ajuste => Serializar(ajuste!), json => Ler<ParametrosRegraAjusteDistribuicao>(json!, nameof(ConfiguracaoDistribuicaoVagas.AjusteDoQuadro)));

    private static readonly ValueComparer<ParametrosRegraAjusteDistribuicao?> ComparadorDoAjuste =
        new((a, b) => SerializarOuNulo(a) == SerializarOuNulo(b), a => SerializarOuNulo(a)!.GetHashCode(StringComparison.Ordinal), a => a);

    private static readonly ValueConverter<IReadOnlyList<VagasDaModalidade>?, string?> ConversorDasRetiradas =
        new(retiradas => Serializar(retiradas!), json => Ler<IReadOnlyList<VagasDaModalidade>>(json!, nameof(ConfiguracaoDistribuicaoVagas.VagasRetiradasNoAjuste)));

    private static readonly ValueComparer<IReadOnlyList<VagasDaModalidade>?> ComparadorDasRetiradas =
        new((a, b) => SerializarOuNulo(a) == SerializarOuNulo(b), a => SerializarOuNulo(a)!.GetHashCode(StringComparison.Ordinal), a => a);

    internal static void ConfigurarColunas(EntityTypeBuilder<ConfiguracaoDistribuicaoVagas> builder)
    {
        builder.Property(c => c.VagasOfertadasBase).IsRequired().HasComment("Quantidade de vagas ofertadas usada como base do quadro.");
        builder.Property(c => c.TotalDeVagasDoEdital).IsRequired().HasComment("Total de vagas resultante para o edital.");
        builder.Property(c => c.PercentualDaReservaLegal).HasPrecision(PrecisaoDoPercentual, EscalaDoPercentual).HasComment("Fração das vagas base reservada por lei; ausente no quadro fixado.");
        builder.Property(c => c.ReservaLegalNominal).HasComment("Quantidade de vagas reservadas pelo percentual legal, antes das garantias.");
        builder.Property(c => c.ReservaLegalEfetiva).HasComment("Quantidade de vagas concedidas à reserva legal após as garantias.");
        builder.Property(c => c.ExcedenteSobreReservaLegal).HasComment("Vagas da reserva efetiva acima da reserva nominal.");
        builder.Property(c => c.VagasCortadasPorEscassez).HasComment("Vagas cortadas das reservas por escassez.");

        builder.Property(c => c.AjusteDoQuadro).HasComment("Parâmetros do motor de ajuste do quadro fixado no edital.")
            .HasColumnType("jsonb")
            .HasConversion(ConversorDoAjuste, ComparadorDoAjuste);
        builder.Property(c => c.VagasRetiradasNoAjuste).HasComment("Vagas retiradas por modalidade pelo ajuste do quadro fixado.")
            .HasColumnType("jsonb")
            .HasConversion(ConversorDasRetiradas, ComparadorDasRetiradas);

        builder.Ignore(c => c.Quadro);
    }

    // Cada ramo tem o seu registro: a reserva legal (o percentual e a conferência) é toda presente ou toda ausente, e só a Lei de Cotas a tem;
    // as vagas retiradas no ajuste e a regra de ajuste do quadro só existem no quadro fixado no edital. O ramo pelo código da regra
    // não entra aqui, porque a lista de códigos mudaria a cada regra nova: a coerência do ramo com a forma é do
    // domínio e da leitura do envelope.
    internal static void RestricoesDoQuadro(TableBuilder<ConfiguracaoDistribuicaoVagas> tabela)
    {
        tabela.HasCheckConstraint(
            "ck_configuracoes_de_vagas_percentual_da_reserva_legal",
            "percentual_da_reserva_legal IS NULL OR (percentual_da_reserva_legal >= 0.5 AND percentual_da_reserva_legal <= 1)");
        tabela.HasCheckConstraint(
            "ck_configuracoes_de_vagas_reserva_legal_completa",
            $"({ColunasDaReservaLegalAusentes}) OR ({ColunasDaReservaLegalPresentes})");
        tabela.HasCheckConstraint(
            "ck_configuracoes_de_vagas_excedente_sobre_reserva_legal",
            "excedente_sobre_reserva_legal IS NULL OR excedente_sobre_reserva_legal = GREATEST(0, reserva_legal_efetiva - reserva_legal_nominal)");
        tabela.HasCheckConstraint(
            "ck_configuracoes_de_vagas_ramo_do_quadro",
            "reserva_legal_nominal IS NULL OR (vagas_retiradas_no_ajuste IS NULL AND ajuste_do_quadro IS NULL)");
    }

    private static string Serializar<T>(T valor) => JsonSerializer.Serialize(valor, OpcoesJson);

    private static string? SerializarOuNulo<T>(T? valor) => valor is null ? null : Serializar(valor);

    private static T Ler<T>(string json, string nomeDoValor) =>
        ObjetoDeValorGravado.DesserializarDocumento(
            () => JsonSerializer.Deserialize<T>(json, OpcoesJson) ?? throw new JsonException("documento nulo"),
            nomeDoValor);
}
