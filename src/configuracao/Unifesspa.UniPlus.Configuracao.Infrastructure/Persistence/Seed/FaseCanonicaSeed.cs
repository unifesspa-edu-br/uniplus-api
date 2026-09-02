namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Fonte única do seed das dezesseis fases canônicas do ciclo de vida de um processo
/// seletivo. Consumida pela configuração EF Core (que materializa as linhas na
/// migration) e pelos testes (que conferem o seed contra esta lista), garantindo
/// uma única definição por fase.
/// </summary>
/// <remarks>
/// <para>
/// A fase é <b>vocabulário estrutural</b>: as dezesseis existem porque o ciclo do
/// certame as tem, não porque alguém as escolheu. Não há autor a registrar, e por
/// isso entram por migration em vez de endpoint admin — critério da ADR-0062,
/// Emenda 2. O CRUD admin permanece disponível para editar o que é de fato
/// administrável (nome, descrição, base legal); o que é normativo vem daqui.
/// </para>
/// <para>
/// <b>Antes: as arestas nasciam semeadas e os vértices não.</b> O seed de
/// <c>PrecedenciaFase</c> referencia códigos de fase que só existiam se alguém
/// rodasse a carga por API — nenhuma constraint impedia, porque os CHECK de
/// <c>precedencia_fase</c> conferem o código contra o vocabulário, não contra a
/// existência da linha. As duas metades do mesmo grafo seguiam regras opostas.
/// </para>
/// <para>
/// Os <see cref="Guid"/> são fixos determinísticos porque seed precisa de
/// identidade estável entre execuções — mesmo molde de
/// <c>PrecedenciaFaseSeed</c>/<c>CategoriaDocumentoSeed</c>.
/// </para>
/// </remarks>
public static class FaseCanonicaSeed
{
    // Prefixo determinístico próprio do catálogo de fases, distinto do usado pelas
    // precedências (93ec…) para não confundir identidades entre as duas tabelas do
    // mesmo grafo.
    private static Guid SeedId(int n) =>
        Guid.Parse($"f45e0000-0000-7000-8000-{n:D12}");

    /// <summary>
    /// As dezesseis fases, em ordem cronológica aproximada do certame. A ordem aqui é
    /// de leitura: a unicidade é por código, e a ordem real de um processo vem do
    /// grafo de precedências mais o cronograma que o operador monta.
    /// </summary>
    public static IReadOnlyList<FaseCanonicaSeedItem> Itens { get; } =
    [
        new(SeedId(1), "INSCRICAO", "Inscrição",
            "Período em que o candidato se inscreve no processo seletivo.",
            DonoTipico.Ceps, OrigemDataFase.Propria,
            ProduzResultado: false, ResultadoDefinitivo: false,
            ColetaInscricao: true, AgrupaEtapas: false, PermiteComplementacao: false,
            BaseLegal: null),

        new(SeedId(2), FaseCanonicaCatalogo.CodigoSolicitacaoIsencao, "Solicitação de isenção",
            "Janela em que o candidato pede isenção da taxa de inscrição. Abre junto com as inscrições e termina antes delas.",
            DonoTipico.Ceps, OrigemDataFase.Propria,
            ProduzResultado: true, ResultadoDefinitivo: false,
            ColetaInscricao: false, AgrupaEtapas: false, PermiteComplementacao: false,
            BaseLegal: "Lei nº 12.799/2013", ColetaSolicitacaoIsencao: true),

        new(SeedId(3), "HOMOLOGACAO", "Homologação das inscrições",
            "Conferência das inscrições recebidas e publicação de quais foram homologadas.",
            DonoTipico.Ceps, OrigemDataFase.Propria,
            ProduzResultado: true, ResultadoDefinitivo: false,
            ColetaInscricao: false, AgrupaEtapas: false, PermiteComplementacao: true,
            BaseLegal: null),

        new(SeedId(4), "ENSALAMENTO", "Ensalamento",
            "Distribuição dos candidatos pelos locais de prova.",
            DonoTipico.Ceps, OrigemDataFase.Propria,
            ProduzResultado: false, ResultadoDefinitivo: false,
            ColetaInscricao: false, AgrupaEtapas: false, PermiteComplementacao: false,
            BaseLegal: null),

        new(SeedId(5), FaseCanonicaCatalogo.CodigoAvaliacao, "Avaliação",
            "Fase que agrupa as etapas pontuadas do certame.",
            DonoTipico.Ceps, OrigemDataFase.Propria,
            ProduzResultado: false, ResultadoDefinitivo: false,
            ColetaInscricao: false, AgrupaEtapas: true, PermiteComplementacao: false,
            BaseLegal: null),

        new(SeedId(6), "CLASSIFICACAO", "Classificação",
            "Apuração das notas e ordenação dos candidatos por modalidade.",
            DonoTipico.Ceps, OrigemDataFase.Propria,
            ProduzResultado: false, ResultadoDefinitivo: false,
            ColetaInscricao: false, AgrupaEtapas: false, PermiteComplementacao: false,
            BaseLegal: null),

        new(SeedId(7), "RESULTADO_PRELIMINAR", "Resultado preliminar",
            "Publicação do resultado que ainda admite recurso.",
            DonoTipico.Ceps, OrigemDataFase.Propria,
            ProduzResultado: true, ResultadoDefinitivo: false,
            ColetaInscricao: false, AgrupaEtapas: false, PermiteComplementacao: false,
            BaseLegal: null),

        new(SeedId(8), "RECURSOS", "Recursos",
            "Interposição e análise dos recursos contra o resultado preliminar.",
            DonoTipico.Ceps, OrigemDataFase.Propria,
            ProduzResultado: false, ResultadoDefinitivo: false,
            ColetaInscricao: false, AgrupaEtapas: false, PermiteComplementacao: true,
            BaseLegal: null),

        new(SeedId(9), "RESULTADO_FINAL", "Resultado final",
            "Publicação do resultado depois de julgados os recursos.",
            DonoTipico.Ceps, OrigemDataFase.Propria,
            ProduzResultado: true, ResultadoDefinitivo: true,
            ColetaInscricao: false, AgrupaEtapas: false, PermiteComplementacao: false,
            BaseLegal: null),

        new(SeedId(10), "HETEROIDENTIFICACAO", "Heteroidentificação",
            "Procedimento de heteroidentificação étnico-racial dos candidatos que concorrem por cota.",
            DonoTipico.Ceps, OrigemDataFase.Propria,
            ProduzResultado: true, ResultadoDefinitivo: false,
            ColetaInscricao: false, AgrupaEtapas: false, PermiteComplementacao: false,
            BaseLegal: "Lei nº 12.711/2012; Portaria Normativa MEC nº 4/2018"),

        new(SeedId(16), "AVALIACAO_BIOPSICOSSOCIAL", "Avaliação biopsicossocial",
            "Avaliação multiprofissional e interdisciplinar que verifica se o candidato com deficiência atende aos requisitos legais para concorrer às vagas reservadas às pessoas com deficiência.",
            DonoTipico.Ceps, OrigemDataFase.Propria,
            ProduzResultado: true, ResultadoDefinitivo: false,
            ColetaInscricao: false, AgrupaEtapas: false, PermiteComplementacao: false,
            BaseLegal: "Lei nº 13.146/2015, art. 2º §1º e art. 30; Lei nº 12.711/2012 c/c Lei nº 13.409/2016"),

        new(SeedId(11), "HABILITACAO", "Habilitação",
            "Comprovação documental dos requisitos declarados pelo candidato.",
            DonoTipico.Crca, OrigemDataFase.Propria,
            ProduzResultado: true, ResultadoDefinitivo: false,
            ColetaInscricao: false, AgrupaEtapas: false, PermiteComplementacao: false,
            BaseLegal: null),

        new(SeedId(12), "HOMOLOGACAO_RESULTADO_FINAL", "Homologação do resultado final",
            "Ato do conselho que homologa o resultado final do processo seletivo.",
            DonoTipico.Consepe, OrigemDataFase.Propria,
            ProduzResultado: true, ResultadoDefinitivo: true,
            ColetaInscricao: false, AgrupaEtapas: false, PermiteComplementacao: false,
            BaseLegal: null),

        new(SeedId(13), "MATRICULA", "Matrícula",
            "Efetivação do vínculo do candidato aprovado com a instituição.",
            DonoTipico.Crca, OrigemDataFase.Propria,
            ProduzResultado: false, ResultadoDefinitivo: false,
            ColetaInscricao: false, AgrupaEtapas: false, PermiteComplementacao: false,
            BaseLegal: null),

        new(SeedId(14), "LISTA_ESPERA", "Lista de espera",
            "Fila de candidatos classificados além das vagas, convocados conforme as vagas são liberadas.",
            DonoTipico.Mec, OrigemDataFase.Delegada,
            ProduzResultado: false, ResultadoDefinitivo: false,
            ColetaInscricao: false, AgrupaEtapas: false, PermiteComplementacao: false,
            BaseLegal: null),

        new(SeedId(15), "CHAMADA", "Chamada",
            "Convocação dos candidatos da lista de espera para ocupar vagas remanescentes.",
            DonoTipico.Mec, OrigemDataFase.Delegada,
            ProduzResultado: true, ResultadoDefinitivo: false,
            ColetaInscricao: false, AgrupaEtapas: false, PermiteComplementacao: false,
            BaseLegal: null),
    ];
}

/// <summary>
/// Definição de uma fase do seed (fonte única), na forma da entidade
/// <c>FaseCanonica</c>. Não passa pela factory — seed materializa linhas
/// diretamente —, e a coerência com as invariantes de domínio é garantida por
/// teste que submete cada item à <c>FaseCanonica.Criar</c>.
/// </summary>
public sealed record FaseCanonicaSeedItem(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    DonoTipico DonoTipico,
    OrigemDataFase OrigemData,
    bool ProduzResultado,
    bool ResultadoDefinitivo,
    bool ColetaInscricao,
    bool AgrupaEtapas,
    bool PermiteComplementacao,
    string? BaseLegal,
    bool ColetaSolicitacaoIsencao = false);
