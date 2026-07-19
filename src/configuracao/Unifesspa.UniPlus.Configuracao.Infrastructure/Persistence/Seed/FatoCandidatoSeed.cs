namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Fonte única do seed do catálogo <c>rol_de_fatos_candidato</c> (UNI-REQ-0077,
/// ADR-0111, refinada pela ADR-0116): os onze fatos do vocabulário fechado do
/// candidato. Consumida tanto pela configuração EF Core (que materializa as linhas
/// via <c>HasData</c> na migration) quanto pelos testes (que conferem o seed do
/// banco contra esta lista), garantindo uma única definição por fato.
/// </summary>
/// <remarks>
/// <para>
/// O conteúdo é a modelagem da ADR-0111/ADR-0116 (autoridade), portada linha a
/// linha. Os <see cref="Guid"/> são fixos determinísticos (não
/// <c>Guid.CreateVersion7</c>) porque seed precisa de identidade estável entre
/// execuções — o mesmo molde de <c>RegraCatalogoSeed</c>.
/// </para>
/// <para>
/// A distinção estático × escopo-processo de um categórico é a <b>nulidade</b> de
/// <see cref="FatoCandidatoSeedItem.ValoresDominio"/>, não um campo próprio:
/// <c>NACIONALIDADE</c> tem o conjunto fechado como <see cref="Domain.Entities.FatoValorDominio"/>
/// filhos (não mais jsonb, ver <see cref="FatoValorDominioSeed"/>); <c>MODALIDADE</c>,
/// <c>CONDICAO_ATENDIMENTO</c> e <c>TIPO_DEFICIENCIA</c> têm
/// <see langword="null"/> (os valores válidos vêm da oferta congelada do processo,
/// resolvidos pelo consumidor). Booleano e numérico têm sempre
/// <see langword="null"/>.
/// </para>
/// <para>
/// <see cref="OrigemFato"/> (ADR-0116) reclassifica os nove fatos originais:
/// <c>FAIXA_ETARIA</c> e <c>RENDA_PER_CAPITA</c> são <see cref="OrigemFato.Derivado"/>
/// (computados — o motor deriva o valor, o candidato não o responde diretamente); os
/// demais sete + <c>NACIONALIDADE</c>/<c>TIPO_DEFICIENCIA</c> são
/// <see cref="OrigemFato.Declarado"/> (resposta/seleção direta do candidato).
/// <see cref="OrigemFato.Integracao"/> fica reservada, sem fato semeado (fonte
/// externa futura, ex.: SIGAA #874).
/// </para>
/// <para>
/// Todos os onze fatos resolvem em <c>PontoResolucao = "INSCRICAO"</c> — são
/// respondidos/derivados no cadastro de inscrição do candidato, nenhum depende de
/// fase posterior (o gate que recusaria isso é a Story #916/PR2).
/// </para>
/// </remarks>
public static class FatoCandidatoSeed
{
    private const string PontoResolucaoInscricao = "INSCRICAO";

    // Prefixo determinístico próprio do catálogo de fatos (distinto do
    // rol_de_regras, para não confundir identidades entre tabelas).
    private static Guid SeedId(int n) =>
        Guid.Parse($"fa700000-0000-7000-8000-{n:D12}");

    /// <summary>Os onze fatos do vocabulário, na ordem canônica da ADR-0111/ADR-0116.</summary>
    public static IReadOnlyList<FatoCandidatoSeedItem> Itens { get; } =
    [
        new(SeedId(1), "COR_RACA", "Cor ou raça", null,
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar,
            null, PontoResolucaoInscricao, "CAMPO_INSCRICAO:COR_RACA"),

        new(SeedId(2), "QUILOMBOLA", "Quilombola", null,
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar,
            null, PontoResolucaoInscricao, "CAMPO_INSCRICAO:QUILOMBOLA"),

        new(SeedId(3), "PCD", "Pessoa com deficiência", null,
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar,
            null, PontoResolucaoInscricao, "CAMPO_INSCRICAO:PCD"),

        new(SeedId(4), "EGRESSO_ESCOLA_PUBLICA", "Egresso de escola pública", null,
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar,
            null, PontoResolucaoInscricao, "CAMPO_INSCRICAO:EGRESSO_ESCOLA_PUBLICA"),

        new(SeedId(5), "RENDA_PER_CAPITA", "Renda familiar per capita", null,
            DominioFato.Numerico, OrigemFato.Derivado, CardinalidadeFato.Escalar,
            null, PontoResolucaoInscricao, "ATRIBUTO_CANDIDATO:RENDA_PER_CAPITA"),

        new(SeedId(6), "FAIXA_ETARIA", "Faixa etária", null,
            DominioFato.Numerico, OrigemFato.Derivado, CardinalidadeFato.Escalar,
            null, PontoResolucaoInscricao, "ATRIBUTO_CANDIDATO:FAIXA_ETARIA"),

        new(SeedId(7), "SEXO", "Sexo", null,
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar,
            null, PontoResolucaoInscricao, "CAMPO_INSCRICAO:SEXO"),

        new(SeedId(8), "MODALIDADE", "Modalidade de concorrência", null,
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Multivalorado,
            null, PontoResolucaoInscricao, "CAMPO_INSCRICAO:MODALIDADE"),

        new(SeedId(9), "CONDICAO_ATENDIMENTO", "Condição de atendimento especializado", null,
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Multivalorado,
            null, PontoResolucaoInscricao, "CAMPO_INSCRICAO:CONDICAO_ATENDIMENTO"),

        new(SeedId(10), "NACIONALIDADE", "Nacionalidade", null,
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar,
            null, PontoResolucaoInscricao, "CAMPO_INSCRICAO:NACIONALIDADE"),

        new(SeedId(11), "TIPO_DEFICIENCIA", "Tipo de deficiência", null,
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar,
            null, PontoResolucaoInscricao, "CAMPO_INSCRICAO:TIPO_DEFICIENCIA"),
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
    OrigemFato Origem,
    CardinalidadeFato Cardinalidade,
    IReadOnlyList<string>? ValoresDominio,
    string PontoResolucao,
    string Binding);
