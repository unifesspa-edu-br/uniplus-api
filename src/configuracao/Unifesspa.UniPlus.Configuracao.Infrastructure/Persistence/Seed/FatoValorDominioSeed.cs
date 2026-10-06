namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Fonte única do seed de <c>fato_valor_dominio</c> (ADR-0116): a descrição por
/// valor dos fatos categóricos <b>estáticos</b> desta colheita —
/// <c>COR_RACA</c> (6), <c>SEXO</c> (3), <c>NACIONALIDADE</c> (3), <c>PARENTESCO</c> (12),
/// <c>DOCUMENTO_ESTRANGEIRO_TIPO</c> (2) e <c>ESTADO_CIVIL</c> (6). Consumida pela
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

    /// <summary>
    /// As linhas do seed (6 COR_RACA + 3 SEXO + 3 NACIONALIDADE + 12 PARENTESCO + 2
    /// DOCUMENTO_ESTRANGEIRO_TIPO + 6 ESTADO_CIVIL).
    /// </summary>
    public static IReadOnlyList<FatoValorDominioSeedItem> Itens { get; } =
    [
        // ── COR_RACA ───────────────────────────────────────────────────────
        // A descrição é o nome da opção; a orientação, o que o candidato precisa saber sobre ela.
        new(SeedId(1), FatoCandidatoId("COR_RACA"), "BRANCA", "Branca", 0, true),
        new(SeedId(2), FatoCandidatoId("COR_RACA"), "PRETA", "Preta", 1, true,
            "Quem concorre às vagas para pretos e pardos passa pela heteroidentificação da Unifesspa (Lei 12.711/2012)."),
        new(SeedId(3), FatoCandidatoId("COR_RACA"), "PARDA", "Parda", 2, true,
            "Quem concorre às vagas para pretos e pardos passa pela heteroidentificação da Unifesspa (Lei 12.711/2012)."),
        new(SeedId(4), FatoCandidatoId("COR_RACA"), "AMARELA", "Amarela", 3, true,
            "Pessoa de origem asiática."),
        new(SeedId(5), FatoCandidatoId("COR_RACA"), "INDIGENA", "Indígena", 4, true,
            "Pessoa que se reconhece como pertencente a um povo indígena."),
        new(SeedId(6), FatoCandidatoId("COR_RACA"), "NAO_INFORMADO", "Prefiro não informar", 5, true,
            "Sem essa informação, você não concorre às vagas reservadas por cor ou raça."),

        // ── SEXO ───────────────────────────────────────────────────────────
        new(SeedId(7), FatoCandidatoId("SEXO"), "FEMININO", "Feminino", 0, true),
        new(SeedId(8), FatoCandidatoId("SEXO"), "MASCULINO", "Masculino", 1, true),
        new(SeedId(9), FatoCandidatoId("SEXO"), "INTERSEXO", "Intersexo", 2, true,
            "Pessoa com variação natural das características sexuais."),

        // ── NACIONALIDADE ────────────────────────────────────────────────
        new(SeedId(10), FatoCandidatoId("NACIONALIDADE"), "NATO", "Brasileiro nato", 0, true,
            "Nascido no Brasil ou nas condições previstas pela Constituição."),
        new(SeedId(11), FatoCandidatoId("NACIONALIDADE"), "NATURALIZADO", "Brasileiro naturalizado", 1, true,
            "Com processo de naturalização reconhecido."),
        new(SeedId(12), FatoCandidatoId("NACIONALIDADE"), "ESTRANGEIRO", "Estrangeiro", 2, true),

        // ── PARENTESCO ───────────────────────────────────────────────────
        // A relação de cada membro com o candidato; o próprio candidato é o membro que o grupo que
        // o inclui identifica.
        new(SeedId(13), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), CandidatoComoMembro.ProprioCandidato, "O próprio candidato", 0, true),
        new(SeedId(14), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "CONJUGE_OU_COMPANHEIRO", "Cônjuge ou companheiro(a)", 1, true),
        new(SeedId(15), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "FILHO_OU_ENTEADO", "Filho(a) ou enteado(a)", 2, true),
        new(SeedId(16), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "PAI_OU_MAE", "Pai ou mãe", 3, true),
        new(SeedId(17), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "PADRASTO_OU_MADRASTA", "Padrasto ou madrasta", 4, true),
        new(SeedId(18), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "IRMAO", "Irmão ou irmã", 5, true),
        new(SeedId(19), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "AVO", "Avô ou avó", 6, true),
        new(SeedId(20), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "NETO", "Neto(a)", 7, true),
        new(SeedId(21), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "SOGRO", "Sogro ou sogra", 8, true),
        new(SeedId(22), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "GENRO_OU_NORA", "Genro ou nora", 9, true),
        new(SeedId(23), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "OUTRO_PARENTE", "Outro parente", 10, true),
        new(SeedId(24), FatoCandidatoId(CandidatoComoMembro.FatoParentesco), "NAO_PARENTE", "Sem parentesco", 11, true,
            "Pessoa sem parentesco com o candidato que integra o grupo familiar."),

        // ── DOCUMENTO_ESTRANGEIRO_TIPO ───────────────────────────────────
        // O documento que identifica o estrangeiro no lugar do RG.
        new(SeedId(25), FatoCandidatoId("DOCUMENTO_ESTRANGEIRO_TIPO"), "PASSAPORTE", "Passaporte", 0, true),
        new(SeedId(26), FatoCandidatoId("DOCUMENTO_ESTRANGEIRO_TIPO"), "RNM", "Registro Nacional Migratório (RNM)", 1, true),

        // ── ESTADO_CIVIL ─────────────────────────────────────────────────
        new(SeedId(27), FatoCandidatoId("ESTADO_CIVIL"), "SOLTEIRO", "Solteiro(a)", 0, true),
        new(SeedId(28), FatoCandidatoId("ESTADO_CIVIL"), "CASADO", "Casado(a)", 1, true),
        new(SeedId(29), FatoCandidatoId("ESTADO_CIVIL"), "UNIAO_ESTAVEL", "Em união estável", 2, true),
        new(SeedId(30), FatoCandidatoId("ESTADO_CIVIL"), "SEPARADO", "Separado(a) judicialmente", 3, true),
        new(SeedId(31), FatoCandidatoId("ESTADO_CIVIL"), "DIVORCIADO", "Divorciado(a)", 4, true),
        new(SeedId(32), FatoCandidatoId("ESTADO_CIVIL"), "VIUVO", "Viúvo(a)", 5, true),

        // ── TIPO_ENDERECO ────────────────────────────────────────────────
        // O tipo de localidade da residência; aldeia, comunidade tradicional e quilombo pedem o nome.
        new(SeedId(33), FatoCandidatoId("TIPO_ENDERECO"), "URBANO", "Zona urbana", 0, true),
        new(SeedId(34), FatoCandidatoId("TIPO_ENDERECO"), "RURAL", "Zona rural", 1, true),
        new(SeedId(35), FatoCandidatoId("TIPO_ENDERECO"), "ALDEIA", "Aldeia indígena", 2, true),
        new(SeedId(36), FatoCandidatoId("TIPO_ENDERECO"), "COMUNIDADE", "Comunidade tradicional", 3, true,
            "Comunidade ribeirinha, extrativista, de pescadores ou outra comunidade tradicional."),
        new(SeedId(37), FatoCandidatoId("TIPO_ENDERECO"), "QUILOMBO", "Quilombo", 4, true),
        new(SeedId(38), FatoCandidatoId("TIPO_ENDERECO"), "VILA", "Vila", 5, true),
        new(SeedId(39), FatoCandidatoId("TIPO_ENDERECO"), "OUTRO", "Outro", 6, true),
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
    bool Ativo,
    string? Orientacao = null);
