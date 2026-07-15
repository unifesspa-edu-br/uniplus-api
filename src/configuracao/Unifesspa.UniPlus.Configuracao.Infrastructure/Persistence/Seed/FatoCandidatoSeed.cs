namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Fonte única do seed do catálogo <c>rol_de_fatos_candidato</c> (UNI-REQ-0077,
/// ADR-0111): os nove fatos do vocabulário fechado do candidato. Consumida tanto
/// pela configuração EF Core (que materializa as linhas via <c>HasData</c> na
/// migration) quanto pelos testes (que conferem o seed do banco contra esta
/// lista), garantindo uma única definição por fato.
/// </summary>
/// <remarks>
/// <para>
/// O conteúdo é a modelagem da ADR-0111 (autoridade), portada linha a linha.
/// Os <see cref="Guid"/> são fixos determinísticos (não <c>Guid.CreateVersion7</c>)
/// porque seed precisa de identidade estável entre execuções — o mesmo molde de
/// <c>RegraCatalogoSeed</c>.
/// </para>
/// <para>
/// A distinção estático × escopo-processo de um categórico é a <b>nulidade</b> de
/// <see cref="FatoCandidatoSeedItem.ValoresDominio"/>, não um campo próprio:
/// <c>COR_RACA</c>/<c>SEXO</c> trazem o conjunto fechado; <c>MODALIDADE</c>/
/// <c>CONDICAO_ATENDIMENTO</c> têm <see langword="null"/> (os valores válidos vêm
/// da oferta congelada do processo, resolvidos pelo consumidor). Booleano e
/// numérico têm sempre <see langword="null"/>.
/// </para>
/// <para>
/// <see cref="NaturezaFato"/> (origem do dado) de todos os nove fatos desta
/// colheita é <see cref="NaturezaFato.BrutoInformado"/> — dado respondido pelo
/// candidato. O eixo existe para fatos futuros de outra origem (ex.: uma
/// modalidade <see cref="NaturezaFato.Derivado"/> computada pelo motor), mas
/// nenhum deles é semeado aqui.
/// </para>
/// </remarks>
public static class FatoCandidatoSeed
{
    // Prefixo determinístico próprio do catálogo de fatos (distinto do
    // rol_de_regras, para não confundir identidades entre tabelas).
    private static Guid SeedId(int n) =>
        Guid.Parse($"fa700000-0000-7000-8000-{n:D12}");

    /// <summary>Os nove fatos do vocabulário, na ordem canônica da ADR-0111.</summary>
    public static IReadOnlyList<FatoCandidatoSeedItem> Itens { get; } =
    [
        new(SeedId(1), "COR_RACA", "Cor ou raça", null,
            DominioFato.Categorico, NaturezaFato.BrutoInformado, CardinalidadeFato.Escalar,
            ["BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_INFORMADO"]),

        new(SeedId(2), "QUILOMBOLA", "Quilombola", null,
            DominioFato.Booleano, NaturezaFato.BrutoInformado, CardinalidadeFato.Escalar,
            null),

        new(SeedId(3), "PCD", "Pessoa com deficiência", null,
            DominioFato.Booleano, NaturezaFato.BrutoInformado, CardinalidadeFato.Escalar,
            null),

        new(SeedId(4), "EGRESSO_ESCOLA_PUBLICA", "Egresso de escola pública", null,
            DominioFato.Booleano, NaturezaFato.BrutoInformado, CardinalidadeFato.Escalar,
            null),

        new(SeedId(5), "RENDA_PER_CAPITA", "Renda familiar per capita", null,
            DominioFato.Numerico, NaturezaFato.BrutoInformado, CardinalidadeFato.Escalar,
            null),

        new(SeedId(6), "FAIXA_ETARIA", "Faixa etária", null,
            DominioFato.Numerico, NaturezaFato.BrutoInformado, CardinalidadeFato.Escalar,
            null),

        new(SeedId(7), "SEXO", "Sexo", null,
            DominioFato.Categorico, NaturezaFato.BrutoInformado, CardinalidadeFato.Escalar,
            ["FEMININO", "MASCULINO", "INTERSEXO"]),

        new(SeedId(8), "MODALIDADE", "Modalidade de concorrência", null,
            DominioFato.Categorico, NaturezaFato.BrutoInformado, CardinalidadeFato.Multivalorado,
            null),

        new(SeedId(9), "CONDICAO_ATENDIMENTO", "Condição de atendimento especializado", null,
            DominioFato.Categorico, NaturezaFato.BrutoInformado, CardinalidadeFato.Multivalorado,
            null),
    ];
}

/// <summary>
/// Definição de um fato do seed (fonte única), na forma da entidade
/// <c>FatoCandidato</c>. Não passa pela factory (seed materializa linhas
/// diretamente); a coerência com as invariantes de domínio é garantida por
/// teste de unidade sobre a factory e por CHECKs de banco sobre a tabela.
/// </summary>
public sealed record FatoCandidatoSeedItem(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    DominioFato Dominio,
    NaturezaFato Natureza,
    CardinalidadeFato Cardinalidade,
    IReadOnlyList<string>? ValoresDominio);
