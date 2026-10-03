namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Fonte única do seed de <c>fato_valor_dominio</c> (ADR-0116): a descrição por
/// valor dos fatos categóricos <b>estáticos</b> desta colheita —
/// <c>COR_RACA</c> (6), <c>SEXO</c> (3), <c>NACIONALIDADE</c> (3) e <c>PARENTESCO</c> (12). Consumida pela
/// configuração EF Core (<c>HasData</c> na migration) e pelos testes de
/// integração, mesmo papel de <see cref="FatoCandidatoSeed"/> para o pai.
/// </summary>
/// <remarks>
/// <c>MODALIDADE</c>, <c>CONDICAO_ATENDIMENTO</c> e <c>TIPO_DEFICIENCIA</c> são
/// categóricos de <b>escopo-processo</b> — não têm linhas aqui; seu domínio vem do
/// cadastro vivo (Modalidade, CondicaoAtendimentoEspecializado, TipoDeficiencia) via
/// projeção do processo, nunca duplicado neste catálogo.
/// </remarks>
public static class FatoValorDominioSeed
{
    // Prefixo determinístico próprio de FatoValorDominio, distinto do prefixo
    // "fa700000" do FatoCandidato pai — para não confundir identidades entre tabelas.
    private static Guid SeedId(int n) =>
        Guid.Parse($"fa70d000-0000-7000-8000-{n:D12}");

    private static Guid FatoCandidatoId(string codigo) =>
        FatoCandidatoSeed.Itens.Single(item => item.Codigo == codigo).Id;

    /// <summary>As linhas do seed (6 COR_RACA + 3 SEXO + 3 NACIONALIDADE + 12 PARENTESCO).</summary>
    public static IReadOnlyList<FatoValorDominioSeedItem> Itens { get; } =
    [
        // ── COR_RACA ───────────────────────────────────────────────────────
        new(SeedId(1), FatoCandidatoId("COR_RACA"), "BRANCA",
            "Autodeclaração de cor/raça branca.", 0, true),
        new(SeedId(2), FatoCandidatoId("COR_RACA"), "PRETA",
            "Autodeclaração de cor/raça preta, conforme Lei 12.711/2012 e resoluções "
            + "da Unifesspa sobre heteroidentificação.", 1, true),
        new(SeedId(3), FatoCandidatoId("COR_RACA"), "PARDA",
            "Autodeclaração de cor/raça parda, conforme Lei 12.711/2012 e resoluções "
            + "da Unifesspa sobre heteroidentificação.", 2, true),
        new(SeedId(4), FatoCandidatoId("COR_RACA"), "AMARELA",
            "Autodeclaração de cor/raça amarela (ascendência asiática).", 3, true),
        new(SeedId(5), FatoCandidatoId("COR_RACA"), "INDIGENA",
            "Autodeclaração de povo indígena.", 4, true),
        new(SeedId(6), FatoCandidatoId("COR_RACA"), "NAO_INFORMADO",
            "Candidato optou por não informar cor ou raça.", 5, true),

        // ── SEXO ───────────────────────────────────────────────────────────
        new(SeedId(7), FatoCandidatoId("SEXO"), "FEMININO",
            "Sexo feminino.", 0, true),
        new(SeedId(8), FatoCandidatoId("SEXO"), "MASCULINO",
            "Sexo masculino.", 1, true),
        new(SeedId(9), FatoCandidatoId("SEXO"), "INTERSEXO",
            "Pessoa intersexo — variação natural das características sexuais.", 2, true),

        // ── NACIONALIDADE ────────────────────────────────────────────────
        new(SeedId(10), FatoCandidatoId("NACIONALIDADE"), "NATO",
            "Brasileiro nato, nascido no Brasil ou nas condições previstas pela Constituição.", 0, true),
        new(SeedId(11), FatoCandidatoId("NACIONALIDADE"), "NATURALIZADO",
            "Brasileiro naturalizado, conforme processo de naturalização reconhecido.", 1, true),
        new(SeedId(12), FatoCandidatoId("NACIONALIDADE"), "ESTRANGEIRO",
            "Cidadão estrangeiro, não brasileiro.", 2, true),

        // ── PARENTESCO ───────────────────────────────────────────────────
        // A relação de cada membro com o candidato; o próprio candidato é o membro que o grupo que
        // o inclui identifica.
        new(SeedId(13), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), CandidatoComoMembro.ProprioCandidato,
            "O próprio candidato.", 0, true),
        new(SeedId(14), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "CONJUGE_OU_COMPANHEIRO",
            "Cônjuge ou companheiro(a) do candidato.", 1, true),
        new(SeedId(15), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "FILHO_OU_ENTEADO",
            "Filho(a) ou enteado(a) do candidato.", 2, true),
        new(SeedId(16), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "PAI_OU_MAE",
            "Pai ou mãe do candidato.", 3, true),
        new(SeedId(17), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "PADRASTO_OU_MADRASTA",
            "Padrasto ou madrasta do candidato.", 4, true),
        new(SeedId(18), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "IRMAO",
            "Irmão ou irmã do candidato.", 5, true),
        new(SeedId(19), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "AVO",
            "Avô ou avó do candidato.", 6, true),
        new(SeedId(20), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "NETO",
            "Neto(a) do candidato.", 7, true),
        new(SeedId(21), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "SOGRO",
            "Sogro ou sogra do candidato.", 8, true),
        new(SeedId(22), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "GENRO_OU_NORA",
            "Genro ou nora do candidato.", 9, true),
        new(SeedId(23), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "OUTRO_PARENTE",
            "Outro parente do candidato.", 10, true),
        new(SeedId(24), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "NAO_PARENTE",
            "Pessoa sem parentesco com o candidato que integra o grupo familiar.", 11, true),
    ];
}

/// <summary>
/// Definição de uma linha do seed de <see cref="Domain.Entities.FatoValorDominio"/>
/// (fonte única), sempre de fato de sistema. Não passa pela factory: o seed materializa as linhas
/// diretamente, e o fato de sistema não recebe valor por <c>FatoCandidato.AdicionarValorDominio</c>.
/// A unicidade do código é garantida pelo índice único de banco e pelo teste do seed.
/// </summary>
public sealed record FatoValorDominioSeedItem(
    Guid Id,
    Guid FatoCandidatoId,
    string Codigo,
    string Descricao,
    int Ordem,
    bool Ativo);
