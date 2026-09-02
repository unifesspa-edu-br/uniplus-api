namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Catálogo do domínio fechado das <b>fases canônicas</b> do ciclo de vida
/// de um processo seletivo (UNI-REQ-0139) e das constantes de coerência associadas.
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
    /// Código canônico da janela em que o candidato pede isenção da taxa (UNI-REQ-0106).
    /// Nomeado porque o Módulo Seleção o referencia nas invariantes da janela — que abre
    /// junto com as inscrições e termina antes delas.
    /// </summary>
    public const string CodigoSolicitacaoIsencao = "SOLICITACAO_ISENCAO";

    /// <summary>
    /// As dezesseis fases, com o rótulo canônico do código, em ordem cronológica
    /// aproximada — fonte do endpoint de vocabulário
    /// (<c>GET /api/configuracao/vocabularios/fases-canonicas</c>). Ordem de declaração
    /// não é semântica para unicidade (que é por código), mas é a ordem em que o
    /// vocabulário é anunciado.
    /// </summary>
    public static IReadOnlyList<FaseCanonicaDescrito> Descritos { get; } =
    [
        new("INSCRICAO", "Inscrição"),
        new(CodigoSolicitacaoIsencao, "Solicitação de isenção"),
        new("HOMOLOGACAO", "Homologação das inscrições"),
        new("ENSALAMENTO", "Ensalamento"),
        new(CodigoAvaliacao, "Avaliação"),
        new("CLASSIFICACAO", "Classificação"),
        new("RESULTADO_PRELIMINAR", "Resultado preliminar"),
        new("RECURSOS", "Recursos"),
        new("RESULTADO_FINAL", "Resultado final"),
        new("HABILITACAO", "Habilitação"),
        new("HETEROIDENTIFICACAO", "Heteroidentificação"),
        new("AVALIACAO_BIOPSICOSSOCIAL", "Avaliação biopsicossocial"),
        new("MATRICULA", "Matrícula"),
        new("HOMOLOGACAO_RESULTADO_FINAL", "Homologação do resultado final"),
        new("LISTA_ESPERA", "Lista de espera"),
        new("CHAMADA", "Chamada"),
    ];

    /// <summary>
    /// Os códigos canônicos das fases do ciclo, derivados de <see cref="Descritos"/> — uma
    /// segunda lista escrita à mão envelheceria sem avisar quando um código novo entrasse
    /// só num dos dois lugares.
    /// </summary>
    public static IReadOnlyList<string> Codigos { get; } = [.. Descritos.Select(static d => d.Codigo)];

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

    /// <summary>Indica se <paramref name="codigo"/> pertence ao conjunto canônico de fases.</summary>
    public static bool EhCanonico(string? codigo) =>
        codigo is not null && CodigosSet.Contains(codigo);

    /// <summary>Indica se a fase <paramref name="codigo"/> admite complementação documental por lei.</summary>
    public static bool PermiteComplementacao(string? codigo) =>
        codigo is not null && ComplementacaoSet.Contains(codigo);
}
