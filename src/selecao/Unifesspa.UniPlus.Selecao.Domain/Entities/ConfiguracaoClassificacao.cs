namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Configuração de classificação do <see cref="ProcessoSeletivo"/> — o 15º
/// bloco canônico do snapshot de publicação (Story #775):
/// COMPÕE POR REFERÊNCIA as regras tipadas que amarram o cálculo do
/// resultado — fórmula da nota, precisão, eliminação (lista) e ordem de
/// alocação entre a 1ª e a 2ª opção de curso (UNI-REQ-0045).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Não é uma "regra guarda-chuva".</strong> <see cref="RegraCalculo"/>
/// é só a fórmula da nota; eliminação, ordem de alocação, bônus e desempate
/// são regras tipadas próprias. Este bloco é a composição dessas referências
/// — não um tipo de regra, não um blob, não um saco de escalares.
/// </para>
/// <para>
/// <strong>Bônus e desempate não são campos deste bloco.</strong> Já são
/// entidades do MESMO agregado (<c>ProcessoSeletivo.BonusRegional</c>,
/// <c>ProcessoSeletivo.CriteriosDesempate</c>, Story #774) —
/// referenciá-los aqui de novo duplicaria fonte de verdade. A composição por
/// referência, no nível do agregado, já é a co-localização das duas
/// dimensões sob o mesmo <c>ProcessoSeletivoId</c>.
/// </para>
/// <para>
/// <strong>Concorrência dupla não é um campo armazenado.</strong> É derivada
/// das modalidades da distribuição de vagas — ver
/// <see cref="ProcessoSeletivo.ConcorrenciaDuplaAplicavel"/>, computada sob
/// demanda a partir do estado corrente (nunca um escalar que poderia
/// dessincronizar se a distribuição de vagas mudar depois).
/// </para>
/// <para>
/// <strong>Quadro de pesos por área do ENEM.</strong> A classificação baseada em ENEM
/// com cálculo local declara a resolução de Pesos por Área que usa
/// (<see cref="ResolucaoPesoAreaEnem"/>) e congela por cópia, para cada grupo de área, o
/// peso e o corte de cada área e a base legal (<see cref="QuadroPesoAreaEnem"/>). O vínculo
/// com o cadastro é pelo valor da resolução, nunca pelo Id das linhas, e nada no processo
/// consulta área fora dessa cópia.
/// </para>
/// </remarks>
public sealed class ConfiguracaoClassificacao : EntityBase, IParteDoProcesso
{
    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>Fórmula da nota final (<c>FORMULA-MEDIA-PONDERADA</c> ou <c>CLASSIFICACAO-IMPORTADA</c>).</summary>
    public ReferenciaRegra RegraCalculo { get; private set; } = null!;

    /// <summary>
    /// Precisão da nota — obrigatória quando <see cref="RegraCalculo"/> é
    /// <c>FORMULA-MEDIA-PONDERADA</c> (cálculo local exige uma regra de
    /// arredondamento); ausente quando é <c>CLASSIFICACAO-IMPORTADA</c>
    /// (a classificação importada não exige precisão local).
    /// </summary>
    public ReferenciaRegra? RegraArredondamento { get; private set; }

    /// <summary>Casas decimais da precisão (par de <see cref="RegraArredondamento"/> — default 2, gaps 1.1).</summary>
    public int? CasasArredondamento { get; private set; }

    /// <summary>
    /// A escala é o teto da nota final, que o sistema grava como <see cref="NotaFinal"/>: não
    /// pode passar do que ela comporta.
    /// </summary>
    public const int EscalaNotaFinalPrecisao = NotaFinal.Precisao;

    /// <summary>Casas decimais da escala da nota final, as da <see cref="NotaFinal"/>.</summary>
    public const int EscalaNotaFinalEscala = NotaFinal.Escala;

    /// <summary>Campo das recusas que dizem respeito à escala da nota final.</summary>
    public const string CampoEscalaNotaFinal = "escalaNotaFinal";

    /// <summary>
    /// Escala em que a nota final é publicada (10, 100, 1.000...): a média ponderada das notas já
    /// convertidas para a escala comum é multiplicada por ela. Obrigatória para publicar com
    /// cálculo local (<c>FORMULA-MEDIA-PONDERADA</c>), cobrada pelo gate de conformidade — o
    /// rascunho a tolera ausente —, e ausente na classificação importada, cujo resultado vem
    /// pronto de fora. Congelada na publicação (UNI-REQ-0175).
    /// </summary>
    public decimal? EscalaNotaFinal { get; private set; }

    /// <summary>
    /// Ordem de alocação (<c>ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA</c>, UNI-REQ-0045): todas as 1ªs
    /// opções do curso, depois o remanejamento entre modalidades dentro do curso, e só a vaga que
    /// sobra vai à 2ª opção; quem não entra em nenhuma opção vai para a lista de espera.
    /// </summary>
    public ReferenciaRegra RegraOrdemAlocacao { get; private set; } = null!;

    /// <summary>
    /// A classificação usa a estrutura de pontuação por área do ENEM — o
    /// sinal explícito (Story #850) do qual as eliminações do ENEM (<c>ELIM-CORTE-EM-AREA</c>,
    /// <c>ELIM-ZERO-EM-AREA</c> e <c>ELIM-FALTA-EM-DIA-DE-PROVA-ENEM</c>) dependem. Deixou de
    /// ser calculado a partir de <see cref="ProcessoSeletivo.TipoProcesso"/>: o rótulo do processo não decide
    /// comportamento, só a configuração declarada decide.
    /// </summary>
    public bool BaseadoEmEnem { get; private set; }

    private readonly List<RegraEliminacao> _regrasEliminacao = [];
    public IReadOnlyCollection<RegraEliminacao> RegrasEliminacao => _regrasEliminacao.AsReadOnly();

    /// <summary>Tamanho máximo da resolução declarada — o mesmo da coluna do cadastro de Pesos por Área.</summary>
    public const int ResolucaoPesoAreaEnemMaxLength = 40;

    /// <summary>Campo das recusas que dizem respeito à resolução de Pesos por Área e ao quadro congelado dela.</summary>
    public const string CampoResolucaoPesoAreaEnem = "resolucaoPesoAreaEnem";

    /// <summary>
    /// Resolução de Pesos por Área que a classificação usa — presente se e somente se a
    /// classificação é baseada em ENEM com cálculo local (<c>FORMULA-MEDIA-PONDERADA</c>).
    /// </summary>
    public string? ResolucaoPesoAreaEnem { get; private set; }

    private readonly List<GrupoPesoAreaEnemCongelado> _quadroPesoAreaEnem = [];

    /// <summary>
    /// Cópia por valor da resolução declarada, um item por grupo de área. Vazio quando não
    /// há resolução.
    /// </summary>
    public IReadOnlyCollection<GrupoPesoAreaEnemCongelado> QuadroPesoAreaEnem => _quadroPesoAreaEnem.AsReadOnly();

    private ConfiguracaoClassificacao() { }

    /// <summary>
    /// Cria a configuração de classificação, recusando — acumuladas, em vez de na primeira
    /// (ADR-0125) — as violações que o próprio bloco consegue provar: a coerência entre
    /// <see cref="RegraCalculo"/> e <see cref="RegraArredondamento"/>, a escala da nota final, as
    /// eliminações (as que dependem do ENEM, as repetidas e o corte contra o quadro de Pesos por
    /// Área) e a resolução de Pesos por Área com o quadro dela. Por ser a mesma construção que o
    /// decoder do envelope usa, essas recusas valem também na restauração. A invariante que
    /// depende de OUTRA dimensão do agregado (a <c>etapa_ref</c> de eliminação existe no processo)
    /// continua validada pela raiz, que tem acesso a ela
    /// (<see cref="ProcessoSeletivo.DefinirClassificacao"/>). A mensagem de eliminação ENEM fora de
    /// processo ENEM não ecoa o código da regra (ADR-0023).
    /// </summary>
    public static Result<ConfiguracaoClassificacao> Criar(DeclaracaoDaClassificacao declaracao)
    {
        ArgumentNullException.ThrowIfNull(declaracao);

        List<FieldError> erros = ConferenciasDaClassificacao.Conferir(declaracao);
        return erros.Count > 0
            ? Result<ConfiguracaoClassificacao>.ValidationFailure(erros)
            : Result<ConfiguracaoClassificacao>.Success(Montar(declaracao));
    }

    private static ConfiguracaoClassificacao Montar(DeclaracaoDaClassificacao declaracao)
    {
        ConfiguracaoClassificacao configuracao = new();
        configuracao.Declarar(declaracao);
        return configuracao;
    }

    private void Declarar(DeclaracaoDaClassificacao declaracao)
    {
        RegraCalculo = declaracao.RegraCalculo;
        RegraArredondamento = declaracao.RegraArredondamento;
        CasasArredondamento = declaracao.CasasArredondamento;
        RegraOrdemAlocacao = declaracao.RegraOrdemAlocacao;
        BaseadoEmEnem = declaracao.BaseadoEmEnem;
        EscalaNotaFinal = declaracao.EscalaNotaFinal;
        ResolucaoPesoAreaEnem = declaracao.ResolucaoNormalizada;
        VincularFilhos(declaracao.RegrasEliminacao, declaracao.QuadroPesoAreaEnem);
    }

    private void VincularFilhos(
        IReadOnlyList<RegraEliminacao> regrasEliminacao,
        IReadOnlyList<GrupoPesoAreaEnemCongelado> quadroPesoAreaEnem)
    {
        foreach (RegraEliminacao regra in regrasEliminacao)
        {
            regra.VincularConfiguracao(Id);
            _regrasEliminacao.Add(regra);
        }

        foreach (GrupoPesoAreaEnemCongelado grupo in quadroPesoAreaEnem)
        {
            grupo.VincularConfiguracao(Id);
            _quadroPesoAreaEnem.Add(grupo);
        }
    }

    /// <summary>
    /// A classificação calcula a nota pela média ponderada das áreas do ENEM: é a única que usa
    /// o quadro de pesos por área, e por isso a única que exige a resolução de Pesos por Área —
    /// e a única em que cabe uma etapa com a nota vinda do ENEM.
    /// </summary>
    public static bool ExigeQuadroPesoAreaEnem(ReferenciaRegra regraCalculo, bool baseadoEmEnem) =>
        ConferenciasDoQuadroDePesosPorArea.Exige(regraCalculo, baseadoEmEnem);

    /// <summary><see cref="ExigeQuadroPesoAreaEnem"/> aplicada a esta classificação.</summary>
    internal bool CalculaPelosPesosPorAreaDoEnem => ConferenciasDoQuadroDePesosPorArea.Exige(RegraCalculo, BaseadoEmEnem);

    /// <summary>O quadro congelado tem ao menos um grupo de área.</summary>
    internal bool TemGrupoNoQuadro => _quadroPesoAreaEnem.Count > 0;

    /// <summary>A classificação calcula pelos pesos por área e congelou o quadro deles.</summary>
    internal bool TemQuadroPesoAreaEnem => CalculaPelosPesosPorAreaDoEnem && TemGrupoNoQuadro;

    /// <summary>
    /// Resolução declarada, não em branco, e quadro congelado com ao menos um grupo. A fábrica
    /// recusa a falta deles, mas o estado é materializável: o EF hidrata a linha sem passar por
    /// ela.
    /// </summary>
    internal bool ResolucaoComQuadroDeclarada => !string.IsNullOrWhiteSpace(ResolucaoPesoAreaEnem) && TemGrupoNoQuadro;

    /// <summary>
    /// Forma da resolução de Pesos por Área informada: cabe na coluna em NFC, não traz caractere
    /// invisível, o nulo inclusive, e pode ser normalizada (<see cref="TextoNormalizavel.TentarNfc"/>). Não depende do catálogo de regras nem do cadastro, e existe separada para o handler
    /// confirmá-la antes de qualquer consulta: o texto vira parâmetro da busca no cadastro, e o
    /// Postgres recusa o caractere nulo com erro de banco. Resolução ausente não é assunto daqui:
    /// se ela é obrigatória ou indevida depende da classificação (<see cref="Criar"/>).
    /// </summary>
    public static List<FieldError> ValidarResolucaoPesoAreaEnem(string? resolucaoPesoAreaEnem) =>
        ConferenciasDoQuadroDePesosPorArea.ConferirForma(resolucaoPesoAreaEnem);

    /// <summary>
    /// As recusas de <see cref="Criar"/> que restam quando a resolução já foi recusada antes,
    /// pela forma ou pelo cadastro. A forma inválida repetida e a falta de quadro congelado são
    /// consequência dessa recusa; qualquer outra violação, inclusive no mesmo campo, continua.
    /// </summary>
    public static IEnumerable<FieldError> SemConsequenciasDaResolucaoRecusada(IEnumerable<FieldError> erros) =>
        ConferenciasDoQuadroDePesosPorArea.SemConsequenciasDaRecusa(erros);

    void IParteDoProcesso.VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;
}
