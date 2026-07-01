namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Catálogo do domínio fechado das <b>quatorze fases canônicas</b> do ciclo de vida
/// de um processo seletivo (UNI-REQ-0064) e das constantes de coerência associadas.
/// Fonte única dos códigos aceitos na guarda de domínio (<c>FaseCanonica.Criar</c>),
/// no validator e no CHECK de banco <c>ck_fase_canonica_codigo_canonico</c>.
/// </summary>
/// <remarks>
/// Tratar o vocabulário de fases como catálogo (e não como enumerado compilado)
/// segue a diretriz do Tech Lead: é dado institucional configurável. Os códigos
/// aqui são o <b>domínio fechado</b> — cada <c>FaseCanonica</c> viva referencia um
/// deles, mas seus demais atributos (nome, dono típico, sinalizadores) são editáveis.
/// </remarks>
public static class FaseCanonicaCatalogo
{
    /// <summary>Código canônico da fase de avaliação — a única que agrupa Etapas pontuadas.</summary>
    public const string CodigoAvaliacao = "AVALIACAO";

    /// <summary>
    /// Os quatorze códigos canônicos das fases do ciclo, em ordem cronológica
    /// aproximada. Ordem de declaração não é semântica — a unicidade é por código.
    /// </summary>
    public static readonly IReadOnlyList<string> Codigos =
    [
        "INSCRICAO",
        "HOMOLOGACAO",
        "ENSALAMENTO",
        CodigoAvaliacao,
        "CLASSIFICACAO",
        "RESULTADO_PRELIMINAR",
        "RECURSOS",
        "RESULTADO_FINAL",
        "HABILITACAO",
        "HETEROIDENTIFICACAO",
        "MATRICULA",
        "HOMOLOGACAO_RESULTADO_FINAL",
        "LISTA_ESPERA",
        "CHAMADA",
    ];

    /// <summary>
    /// Fases em que a legislação permite complementação/reenvio documental —
    /// homologação e recursos. A habilitação é deliberadamente excluída (o SiSU
    /// veda complementação nessa fase).
    /// </summary>
    public static readonly IReadOnlyList<string> CodigosComComplementacaoPermitida =
    [
        "HOMOLOGACAO",
        "RECURSOS",
    ];

    private static readonly HashSet<string> CodigosSet = new(Codigos, StringComparer.Ordinal);

    private static readonly HashSet<string> ComplementacaoSet =
        new(CodigosComComplementacaoPermitida, StringComparer.Ordinal);

    /// <summary>Indica se <paramref name="codigo"/> pertence ao conjunto canônico das quatorze fases.</summary>
    public static bool EhCanonico(string? codigo) =>
        codigo is not null && CodigosSet.Contains(codigo);

    /// <summary>Indica se a fase <paramref name="codigo"/> admite complementação documental por lei.</summary>
    public static bool PermiteComplementacao(string? codigo) =>
        codigo is not null && ComplementacaoSet.Contains(codigo);
}
