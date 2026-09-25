namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

/// <summary>
/// Classificação de exposição de cada bloco da configuração congelada: público, interno, ou já
/// público por outro contrato.
/// </summary>
/// <remarks>
/// A configuração congelada cresce a cada incremento do domínio — já cresceu com taxa de inscrição,
/// localidade, calendário e algoritmo de contagem de prazo. Sem uma lista fechada, o bloco seguinte
/// nasce e alguém, projetando por conveniência, o publica sem que ninguém tenha decidido que ele é
/// público. É o risco de exposição excessiva no nível da propriedade: servir o objeto inteiro e
/// entregar campo interno que nunca se pretendeu expor.
/// <para>
/// Esta lista é a contrapartida verificável da projeção por tipos declarados. A projeção já impede
/// o vazamento por omissão — o campo novo só atravessa quando alguém o escreve lá. A lista cobre o
/// caso em que alguém contorna a forma, e transforma a decisão de exposição, que hoje dependeria de
/// revisão humana atenta, em obrigação que quebra a construção.
/// </para>
/// <para>
/// Acrescentar bloco ao envelope <b>sem</b> classificá-lo aqui faz a verificação falhar, nomeando o
/// bloco. Classificar é a decisão; a lista é onde ela fica registrada.
/// </para>
/// </remarks>
public static class ClassificacaoDosBlocosDoCertame
{
    /// <summary>
    /// Blocos que o contrato público do certame projeta. <c>retificacao</c> é condicional: só
    /// existe no envelope depois que o edital é emendado. <c>identificadorLegivel</c> é o endereço
    /// público do certame — publicá-lo é o propósito dele.
    /// </summary>
    public static readonly IReadOnlySet<string> Publicados = new HashSet<string>(StringComparer.Ordinal)
    {
        "tipoProcesso",
        "periodo",
        "identificadorLegivel",
        "localidade",
        "identidadesUnidade",
        "hashesEdital",
        "ofertas",
        "modalidadesOfertadas",
        "vagas",
        "etapas",
        "cronogramaFases",
        "documentosExigidos",
        "atendimento",
        "taxaInscricao",
        "retificacao",
    };

    /// <summary>
    /// Blocos que não atravessam a fronteira pública. São o critério que decide o resultado do
    /// certame, a mecânica de distribuição e remanejamento, a árvore documental com o grafo de
    /// dependência entre fatos, e os parâmetros do interpretador de regras — nada que o cidadão
    /// precise ler para se inscrever, e boa parte do que a instituição não publica num endereço
    /// anônimo.
    /// </summary>
    public static readonly IReadOnlySet<string> Internos = new HashSet<string>(StringComparer.Ordinal)
    {
        "classificacao",
        "criteriosDesempate",
        "bonusRegional",
        "cascataRemanejamento",
        "distribuicao",
        "modalidades",
        "arvoreSatisfacao",
        "grafoDependencia",
        "regrasDerivacao",
        "fatosColetados",
        "divulgacao",
        "calendarioDiasUteis",
        "algoritmoContagemPrazo",
        "versaoInterpretador",
    };

    /// <summary>
    /// Blocos que são públicos, mas por outro contrato — não se projetam aqui para não criar duas
    /// fontes do mesmo conteúdo. O formulário de inscrição já é servido pelo endpoint de
    /// renderização.
    /// </summary>
    public static readonly IReadOnlySet<string> PublicosPorOutroContrato = new HashSet<string>(StringComparer.Ordinal)
    {
        "formulario",
    };

    /// <summary>Todo bloco classificado, em qualquer das três categorias.</summary>
    public static IReadOnlySet<string> Classificados { get; } =
        new HashSet<string>(Publicados.Concat(Internos).Concat(PublicosPorOutroContrato), StringComparer.Ordinal);
}
