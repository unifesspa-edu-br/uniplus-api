namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Configuração de classificação do <see cref="ProcessoSeletivo"/> — o 15º
/// bloco canônico do snapshot de publicação (Story #775):
/// COMPÕE POR REFERÊNCIA as regras tipadas que amarram o cálculo do
/// resultado — fórmula da nota, precisão, eliminação (lista) e ordem de
/// alocação (RN04).
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
/// (INV-B7) das modalidades da distribuição de vagas — ver
/// <see cref="ProcessoSeletivo.ConcorrenciaDuplaAplicavel"/>, computada sob
/// demanda a partir do estado corrente (nunca um escalar que poderia
/// dessincronizar se a distribuição de vagas mudar depois).
/// </para>
/// <para>
/// <strong>Pesos ENEM (<c>peso_area_enem</c>) são deferidos</strong> desta
/// fatia — estrutura própria (Res. 805, decisão TL) fora do escopo desta
/// entrega; processos baseados em ENEM (PSVR, SiSU) ainda podem ser
/// configurados com <see cref="RegraCalculo"/>/eliminação, apenas sem o
/// quadro de pesos por grupo de curso congelado.
/// </para>
/// </remarks>
public sealed class ConfiguracaoClassificacao : EntityBase
{
    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>Fórmula da nota final (<c>FORMULA-MEDIA-PONDERADA</c> ou <c>CLASSIFICACAO-IMPORTADA</c>).</summary>
    public ReferenciaRegra RegraCalculo { get; private set; } = null!;

    /// <summary>
    /// Precisão da nota — obrigatória quando <see cref="RegraCalculo"/> é
    /// <c>FORMULA-MEDIA-PONDERADA</c> (cálculo local exige uma regra de
    /// arredondamento); ausente quando é <c>CLASSIFICACAO-IMPORTADA</c>
    /// (INV-B8: não exige precisão local).
    /// </summary>
    public ReferenciaRegra? RegraArredondamento { get; private set; }

    /// <summary>Casas decimais da precisão (par de <see cref="RegraArredondamento"/> — default 2, gaps 1.1).</summary>
    public int? CasasArredondamento { get; private set; }

    /// <summary>Ordem de alocação 1ª/2ª opção → remanejamento → lista de espera (<c>ALOCACAO-OPCOES-RN04</c>, RN04).</summary>
    public ReferenciaRegra RegraOrdemAlocacao { get; private set; } = null!;

    /// <summary>Quantas opções de curso o processo aceita (1 ou 2 — RN04).</summary>
    public int NOpcoesAlocacao { get; private set; }

    /// <summary>
    /// A classificação usa a estrutura de pontuação por área do ENEM — o
    /// sinal explícito (Story #850) do qual <c>ELIM-CORTE-REDACAO</c> e
    /// <c>ELIM-ZERO-EM-AREA</c> dependem. Deixou de ser calculado a partir de
    /// <see cref="ProcessoSeletivo.TipoProcesso"/>: o rótulo do processo não decide
    /// comportamento, só a configuração declarada decide.
    /// </summary>
    public bool BaseadoEmEnem { get; private set; }

    private readonly List<RegraEliminacao> _regrasEliminacao = [];
    public IReadOnlyCollection<RegraEliminacao> RegrasEliminacao => _regrasEliminacao.AsReadOnly();

    private ConfiguracaoClassificacao() { }

    /// <summary>
    /// Cria a configuração de classificação, validando INV-B8 (coerência
    /// entre <see cref="RegraCalculo"/> e <see cref="RegraArredondamento"/>),
    /// os limites de <see cref="NOpcoesAlocacao"/> e que
    /// <c>ELIM-CORTE-REDACAO</c>/<c>ELIM-ZERO-EM-AREA</c> só entrem quando
    /// <paramref name="baseadoEmEnem"/> é <see langword="true"/> — a única
    /// dependência da estrutura de pontuação por área do ENEM, e por isso a
    /// única invariante que precisa do sinal. A invariante que depende de
    /// OUTRA dimensão do agregado (INV-B4: <c>etapa_ref</c> de eliminação
    /// existe no processo) continua validada pela raiz, que tem acesso a ela
    /// (<see cref="ProcessoSeletivo.DefinirClassificacao"/>).
    /// </summary>
    /// <summary>
    /// Acumula toda violação independente em vez de retornar na primeira (ADR-0125) — a
    /// mensagem de eliminação ENEM fora de processo ENEM não ecoa mais o código da regra
    /// (ADR-0023).
    /// </summary>
    public static Result<ConfiguracaoClassificacao> Criar(
        ReferenciaRegra regraCalculo,
        ReferenciaRegra? regraArredondamento,
        int? casasArredondamento,
        ReferenciaRegra regraOrdemAlocacao,
        int nOpcoesAlocacao,
        IReadOnlyList<RegraEliminacao> regrasEliminacao,
        bool baseadoEmEnem)
    {
        ArgumentNullException.ThrowIfNull(regraCalculo);
        ArgumentNullException.ThrowIfNull(regraOrdemAlocacao);
        ArgumentNullException.ThrowIfNull(regrasEliminacao);

        List<FieldError> erros = ValidarNOpcoesAlocacao(nOpcoesAlocacao);

        bool ehImportada = regraCalculo.Codigo == RegraCalculoCodigo.ClassificacaoImportada;

        if (ehImportada)
        {
            // INV-B8: classificação importada (federal) não exige precisão local.
            if (regraArredondamento is not null || casasArredondamento is not null)
            {
                erros.Add(new("regraArredondamentoCodigo", new DomainError(
                    "ConfiguracaoClassificacao.ArredondamentoIndevido",
                    "Arredondamento local não se aplica quando a classificação é importada (INV-B8).")));
            }

            // Mesma razão: eliminação por corte de nota pressupõe um cálculo
            // local que a importação substitui — persistir uma regra de
            // eliminação junto de CLASSIFICACAO-IMPORTADA criaria um estado
            // contraditório (o resultado já vem pronto de fora).
            if (regrasEliminacao.Count > 0)
            {
                erros.Add(new("regrasEliminacao", new DomainError(
                    "ConfiguracaoClassificacao.EliminacaoIndevida",
                    "Regras de eliminação não se aplicam quando a classificação é importada (INV-B8).")));
            }
        }
        else
        {
            // Cálculo local exige uma regra de precisão declarada (gaps 1.1: default truncar).
            // As duas metades são checadas e atribuídas ao field que cada uma corrige — um
            // regraArredondamentoCodigo válido acompanhado de casasArredondamento inválido não
            // pode apontar o campo errado (achado de revisão).
            if (regraArredondamento is null)
            {
                erros.Add(new("regraArredondamentoCodigo", new DomainError(
                    "ConfiguracaoClassificacao.ArredondamentoObrigatorio",
                    "Cálculo local exige regra de arredondamento (INV-B8).")));
            }

            if (casasArredondamento is not > 0)
            {
                erros.Add(new("casasArredondamento", new DomainError(
                    "ConfiguracaoClassificacao.CasasArredondamentoObrigatorio",
                    "Cálculo local exige casas decimais de arredondamento maior que zero (INV-B8).")));
            }
        }

        // A única dependência real da estrutura de pontuação por área do ENEM —
        // eixo ortogonal a RegraCalculo (que só distingue cálculo local de
        // classificação importada, INV-B8). Quando a classificação é importada, o
        // gate acima já recusou qualquer regrasEliminacao não-vazia, então esta
        // checagem só encontra itens no caminho de cálculo local.
        if (regrasEliminacao.Any(static regra => regra.Args is ArgsElimCorteRedacao or ArgsElimZeroEmArea) && !baseadoEmEnem)
        {
            erros.Add(new("regrasEliminacao", new DomainError(
                "ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem",
                "Uma ou mais regras de eliminação só se aplicam quando a classificação está configurada como baseada em ENEM.")));
        }

        if (erros.Count > 0)
        {
            return Result<ConfiguracaoClassificacao>.ValidationFailure(erros);
        }

        ConfiguracaoClassificacao configuracao = new()
        {
            RegraCalculo = regraCalculo,
            RegraArredondamento = regraArredondamento,
            CasasArredondamento = casasArredondamento,
            RegraOrdemAlocacao = regraOrdemAlocacao,
            NOpcoesAlocacao = nOpcoesAlocacao,
            BaseadoEmEnem = baseadoEmEnem,
        };

        foreach (RegraEliminacao regra in regrasEliminacao)
        {
            regra.VincularConfiguracao(configuracao.Id);
            configuracao._regrasEliminacao.Add(regra);
        }

        return Result<ConfiguracaoClassificacao>.Success(configuracao);
    }

    /// <summary>
    /// O número de opções de curso não depende do catálogo de regras — ao contrário das
    /// demais checagens de <see cref="Criar"/>, que só fazem sentido depois de a regra de
    /// cálculo já ter sido resolvida (é ela que distingue classificação local de importada,
    /// INV-B8). Existe separada para o handler poder confirmá-la ANTES de consultar o
    /// <c>rol_de_regras</c> (mesmo padrão de <c>CriterioDesempate.ValidarOrdem</c>, PR #1216).
    /// </summary>
    public static List<FieldError> ValidarNOpcoesAlocacao(int nOpcoesAlocacao)
    {
        List<FieldError> erros = [];

        if (nOpcoesAlocacao is not (1 or 2))
        {
            erros.Add(new("nOpcoesAlocacao", new DomainError(
                "ConfiguracaoClassificacao.NOpcoesInvalido", "O número de opções de curso deve ser 1 ou 2 (RN04).")));
        }

        return erros;
    }

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;
}
