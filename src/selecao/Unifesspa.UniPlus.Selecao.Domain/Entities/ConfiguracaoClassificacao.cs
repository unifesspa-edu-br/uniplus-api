namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Extensions;
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
/// <strong>Quadro de pesos por área do ENEM.</strong> A classificação baseada em ENEM
/// com cálculo local declara a resolução de Pesos por Área que usa
/// (<see cref="ResolucaoPesoAreaEnem"/>) e congela por cópia, para cada grupo de área, o
/// peso e o corte de cada área e a base legal (<see cref="QuadroPesoAreaEnem"/>). O vínculo
/// com o cadastro é pelo valor da resolução, nunca pelo Id das linhas, e nada no processo
/// consulta área fora dessa cópia.
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
    /// sinal explícito (Story #850) do qual as eliminações do ENEM (<c>ELIM-CORTE-REDACAO</c>,
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

    private const int TamanhoMaximoDoRotuloEcoado = 40;

    private const string ErroResolucaoPesoAreaEnemInvalida = "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida";
    private const string ErroQuadroPesoAreaEnemVazio = "ConfiguracaoClassificacao.QuadroPesoAreaEnemVazio";

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
    /// Cria a configuração de classificação, validando INV-B8 (coerência
    /// entre <see cref="RegraCalculo"/> e <see cref="RegraArredondamento"/>),
    /// os limites de <see cref="NOpcoesAlocacao"/>, que as eliminações que só têm sentido
    /// sobre os dados do ENEM do candidato (<see cref="ArgsRegraEliminacao.ExigeEnem"/>) só
    /// entrem quando <paramref name="baseadoEmEnem"/> é <see langword="true"/>, que a falta em
    /// dia de prova do ENEM seja declarada no máximo uma vez, cada repetição recusada no
    /// próprio item (salvo na classificação importada, que já recusa toda eliminação), e o
    /// quadro de Pesos por Área, que o mesmo sinal também exige ou recusa.
    /// Por ser a mesma construção que o decoder do envelope usa, essas recusas valem também
    /// na restauração. A invariante que depende de
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
        bool baseadoEmEnem,
        string? resolucaoPesoAreaEnem,
        IReadOnlyList<GrupoPesoAreaEnemCongelado> quadroPesoAreaEnem)
    {
        ArgumentNullException.ThrowIfNull(regraCalculo);
        ArgumentNullException.ThrowIfNull(regraOrdemAlocacao);
        ArgumentNullException.ThrowIfNull(regrasEliminacao);
        ArgumentNullException.ThrowIfNull(quadroPesoAreaEnem);

        List<FieldError> erros = ValidarNOpcoesAlocacao(nOpcoesAlocacao);

        bool ehImportada = regraCalculo.Codigo == RegraCalculoCodigo.ClassificacaoImportada;

        if (ehImportada)
        {
            // INV-B8: classificação importada (federal) não exige precisão local. As duas
            // metades são checadas e atribuídas ao field que cada uma carrega — informar só
            // casasArredondamento (regraArredondamento nulo) não pode apontar o field de regra
            // (achado de revisão, mesmo caso já corrigido no ramo de cálculo local).
            if (regraArredondamento is not null)
            {
                erros.Add(new("regraArredondamentoCodigo", new DomainError(
                    "ConfiguracaoClassificacao.ArredondamentoIndevido",
                    "Arredondamento local não se aplica quando a classificação é importada (INV-B8).")));
            }

            if (casasArredondamento is not null)
            {
                erros.Add(new("casasArredondamento", new DomainError(
                    "ConfiguracaoClassificacao.CasasArredondamentoIndevido",
                    "Casas de arredondamento não se aplicam quando a classificação é importada (INV-B8).")));
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

        // Eixo ortogonal a RegraCalculo (que só distingue cálculo local de classificação
        // importada, INV-B8): a regra que só tem sentido sobre os dados do ENEM do candidato
        // exige a classificação baseada em ENEM.
        if (regrasEliminacao.Any(static regra => regra.Args.ExigeEnem()) && !baseadoEmEnem)
        {
            erros.Add(new("regrasEliminacao", new DomainError(
                "ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem",
                "Uma ou mais regras de eliminação só se aplicam quando a classificação está configurada como baseada em ENEM.")));
        }

        // Sem args, uma segunda declaração da falta em dia de prova repete a primeira e não
        // elimina ninguém a mais: cada repetição é recusada no próprio item. Na classificação
        // importada toda eliminação tem de sair, e a recusa de um item seria só consequência. A
        // recusa do ENEM desmarcado não suprime: o operador pode corrigi-la marcando o ENEM, e
        // a repetição continua sendo violação independente (ADR-0125).
        if (!ehImportada)
        {
            bool faltaJaDeclarada = false;
            for (int indice = 0; indice < regrasEliminacao.Count; indice++)
            {
                if (regrasEliminacao[indice].Args is not ArgsElimFaltaEmDiaDeProvaEnem)
                {
                    continue;
                }

                if (faltaJaDeclarada)
                {
                    erros.Add(new($"regrasEliminacao[{indice}]", new DomainError(
                        "ConfiguracaoClassificacao.FaltaEmDiaDeProvaEnemRepetida",
                        "A eliminação por falta em dia de prova do ENEM só pode ser declarada uma vez.")));
                }

                faltaJaDeclarada = true;
            }
        }

        string? resolucao = string.IsNullOrWhiteSpace(resolucaoPesoAreaEnem) ? null : resolucaoPesoAreaEnem.Trim();
        erros.AddRange(ValidarQuadroPesoAreaEnem(
            ExigeQuadroPesoAreaEnem(regraCalculo, baseadoEmEnem), resolucao, quadroPesoAreaEnem));

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
            // NFC, como o cadastro de Pesos por Área a grava e o envelope canônico a emite: o
            // vínculo com o cadastro é pelo valor, e a forma não muda num ciclo de retificação.
            // A normalização vem depois da validação, que recusa o que ela não conseguiria tratar.
            ResolucaoPesoAreaEnem = resolucao is null ? null : TextoCongelado.Normalizar(resolucao)!,
        };

        foreach (RegraEliminacao regra in regrasEliminacao)
        {
            regra.VincularConfiguracao(configuracao.Id);
            configuracao._regrasEliminacao.Add(regra);
        }

        foreach (GrupoPesoAreaEnemCongelado grupo in quadroPesoAreaEnem)
        {
            grupo.VincularConfiguracao(configuracao.Id);
            configuracao._quadroPesoAreaEnem.Add(grupo);
        }

        return Result<ConfiguracaoClassificacao>.Success(configuracao);
    }

    /// <summary>
    /// O número de opções de curso não depende do catálogo de regras — ao contrário das
    /// demais checagens de <see cref="Criar"/>, que só fazem sentido depois de a regra de
    /// cálculo já ter sido resolvida (é ela que distingue classificação local de importada,
    /// INV-B8). Existe separada para o handler poder confirmá-la ANTES de consultar o
    /// <c>rol_de_regras</c>.
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

    /// <summary>
    /// A classificação calcula a nota pela média ponderada das áreas do ENEM: é a única que usa
    /// o quadro de pesos por área, e por isso a única que exige a resolução de Pesos por Área —
    /// e a única em que cabe uma etapa com a nota vinda do ENEM.
    /// </summary>
    public static bool ExigeQuadroPesoAreaEnem(ReferenciaRegra regraCalculo, bool baseadoEmEnem)
    {
        ArgumentNullException.ThrowIfNull(regraCalculo);

        return baseadoEmEnem && regraCalculo.Codigo == RegraCalculoCodigo.FormulaMediaPonderada;
    }

    /// <summary>
    /// Forma da resolução de Pesos por Área informada: cabe na coluna em NFC, não traz caractere
    /// invisível, o nulo inclusive, e pode ser normalizada (<see cref="TextoNormalizavel.TentarNfc"/>). Não depende do catálogo de regras nem do cadastro, e existe separada para o handler
    /// confirmá-la antes de qualquer consulta: o texto vira parâmetro da busca no cadastro, e o
    /// Postgres recusa o caractere nulo com erro de banco. Resolução ausente não é assunto daqui:
    /// se ela é obrigatória ou indevida depende da classificação (<see cref="Criar"/>).
    /// </summary>
    public static List<FieldError> ValidarResolucaoPesoAreaEnem(string? resolucaoPesoAreaEnem)
    {
        List<FieldError> erros = [];
        if (string.IsNullOrWhiteSpace(resolucaoPesoAreaEnem))
        {
            return erros;
        }

        // A resolução volta nas mensagens de recusa e nos logs: além de caber na coluna, não pode
        // trazer quebra de linha nem inversão de direção de leitura.
        if (TextoNormalizavel.TentarNfc(resolucaoPesoAreaEnem.Trim(), ResolucaoPesoAreaEnemMaxLength, out _)
            != SituacaoDoTexto.Valido)
        {
            erros.Add(new(CampoResolucaoPesoAreaEnem, new DomainError(
                ErroResolucaoPesoAreaEnemInvalida,
                $"A resolução de Pesos por Área deve ter até {ResolucaoPesoAreaEnemMaxLength} caracteres, sem caracteres de controle, de formatação ou de quebra de linha.")));
        }

        return erros;
    }

    /// <summary>
    /// As recusas de <see cref="Criar"/> que restam quando a resolução já foi recusada antes,
    /// pela forma ou pelo cadastro. A forma inválida repetida e a falta de quadro congelado são
    /// consequência dessa recusa; qualquer outra violação, inclusive no mesmo campo, continua.
    /// </summary>
    public static IEnumerable<FieldError> SemConsequenciasDaResolucaoRecusada(IEnumerable<FieldError> erros)
    {
        ArgumentNullException.ThrowIfNull(erros);

        return erros.Where(static erro =>
            erro.Error.Code is not (ErroResolucaoPesoAreaEnemInvalida or ErroQuadroPesoAreaEnemVazio));
    }

    /// <summary>
    /// A resolução de Pesos por Área acompanha a classificação baseada em ENEM com cálculo
    /// local, e só ela. A completude dos grupos é conferida por quem resolve a resolução no
    /// cadastro, que conhece os grupos; aqui fica o que a própria cópia consegue provar: há
    /// grupo congelado, e nenhum se repete.
    /// </summary>
    private static List<FieldError> ValidarQuadroPesoAreaEnem(
        bool exigeQuadro,
        string? resolucao,
        IReadOnlyList<GrupoPesoAreaEnemCongelado> quadro)
    {
        List<FieldError> erros = [];

        if (!exigeQuadro)
        {
            if (resolucao is not null || quadro.Count > 0)
            {
                erros.Add(new(CampoResolucaoPesoAreaEnem, new DomainError(
                    "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemIndevida",
                    "A resolução de Pesos por Área só se aplica à classificação baseada em ENEM com cálculo local da nota.")));
            }

            return erros;
        }

        if (resolucao is null)
        {
            erros.Add(new(CampoResolucaoPesoAreaEnem, new DomainError(
                "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemObrigatoria",
                "A classificação baseada em ENEM com cálculo local da nota exige a resolução de Pesos por Área.")));
            return erros;
        }

        erros.AddRange(ValidarResolucaoPesoAreaEnem(resolucao));

        if (quadro.Count == 0)
        {
            erros.Add(new(CampoResolucaoPesoAreaEnem, new DomainError(
                ErroQuadroPesoAreaEnemVazio,
                "A resolução de Pesos por Área declarada não tem nenhum grupo de área congelado.")));
        }

        foreach (IGrouping<string, GrupoPesoAreaEnemCongelado> repetido in quadro
            .GroupBy(static grupo => grupo.GrupoAreaEnem.Codigo, StringComparer.Ordinal)
            .Where(static grupos => grupos.Count() > 1))
        {
            erros.Add(new(CampoResolucaoPesoAreaEnem, new DomainError(
                "ConfiguracaoClassificacao.QuadroPesoAreaEnemGrupoRepetido",
                $"O quadro de pesos por área repete o grupo {CaracteresInvisiveis.ParaEco(repetido.First().GrupoAreaEnem.Rotulo, TamanhoMaximoDoRotuloEcoado)}.")));
        }

        return erros;
    }

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;
}
