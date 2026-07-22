namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Configuração de classificação do <see cref="ProcessoSeletivo"/> — o 15º
/// bloco canônico do snapshot de publicação (Story #775, modelagem P-B §2.1):
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

    private readonly List<RegraEliminacao> _regrasEliminacao = [];
    public IReadOnlyCollection<RegraEliminacao> RegrasEliminacao => _regrasEliminacao.AsReadOnly();

    private ConfiguracaoClassificacao() { }

    /// <summary>
    /// Cria a configuração de classificação, validando INV-B8 (coerência
    /// entre <see cref="RegraCalculo"/> e <see cref="RegraArredondamento"/>) e
    /// os limites de <see cref="NOpcoesAlocacao"/>. As invariantes que
    /// dependem de OUTRAS dimensões do agregado (INV-B4: <c>etapa_ref</c> de
    /// eliminação existe no processo; ENEM-only de certos cortes) são
    /// validadas pela raiz, que tem acesso a elas
    /// (<see cref="ProcessoSeletivo.DefinirClassificacao"/>).
    /// </summary>
    public static Result<ConfiguracaoClassificacao> Criar(
        ReferenciaRegra regraCalculo,
        ReferenciaRegra? regraArredondamento,
        int? casasArredondamento,
        ReferenciaRegra regraOrdemAlocacao,
        int nOpcoesAlocacao,
        IReadOnlyList<RegraEliminacao> regrasEliminacao)
    {
        ArgumentNullException.ThrowIfNull(regraCalculo);
        ArgumentNullException.ThrowIfNull(regraOrdemAlocacao);
        ArgumentNullException.ThrowIfNull(regrasEliminacao);

        if (nOpcoesAlocacao is not (1 or 2))
        {
            return Result<ConfiguracaoClassificacao>.Failure(new DomainError(
                "ConfiguracaoClassificacao.NOpcoesInvalido", "O número de opções de curso deve ser 1 ou 2 (RN04)."));
        }

        bool ehImportada = regraCalculo.Codigo == RegraCalculoCodigo.ClassificacaoImportada;

        if (ehImportada)
        {
            // INV-B8: classificação importada (federal) não exige precisão local.
            if (regraArredondamento is not null || casasArredondamento is not null)
            {
                return Result<ConfiguracaoClassificacao>.Failure(new DomainError(
                    "ConfiguracaoClassificacao.ArredondamentoIndevido",
                    "Arredondamento local não se aplica quando a classificação é importada (INV-B8)."));
            }

            // Mesma razão: eliminação por corte de nota pressupõe um cálculo
            // local que a importação substitui — persistir uma regra de
            // eliminação junto de CLASSIFICACAO-IMPORTADA criaria um estado
            // contraditório (o resultado já vem pronto de fora).
            if (regrasEliminacao.Count > 0)
            {
                return Result<ConfiguracaoClassificacao>.Failure(new DomainError(
                    "ConfiguracaoClassificacao.EliminacaoIndevida",
                    "Regras de eliminação não se aplicam quando a classificação é importada (INV-B8)."));
            }
        }
        else
        {
            // Cálculo local exige uma regra de precisão declarada (gaps 1.1: default truncar).
            if (regraArredondamento is null || casasArredondamento is not > 0)
            {
                return Result<ConfiguracaoClassificacao>.Failure(new DomainError(
                    "ConfiguracaoClassificacao.ArredondamentoObrigatorio",
                    "Cálculo local exige regra de arredondamento com casas decimais maior que zero (INV-B8)."));
            }
        }

        ConfiguracaoClassificacao configuracao = new()
        {
            RegraCalculo = regraCalculo,
            RegraArredondamento = regraArredondamento,
            CasasArredondamento = casasArredondamento,
            RegraOrdemAlocacao = regraOrdemAlocacao,
            NOpcoesAlocacao = nOpcoesAlocacao,
        };

        foreach (RegraEliminacao regra in regrasEliminacao)
        {
            regra.VincularConfiguracao(configuracao.Id);
            configuracao._regrasEliminacao.Add(regra);
        }

        return Result<ConfiguracaoClassificacao>.Success(configuracao);
    }

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;
}
