namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// As <b>seis dimensões</b> da configuração de um <see cref="ProcessoSeletivo"/>,
/// reconstruídas a partir de uma <see cref="VersaoConfiguracao"/> congelada
/// (ADR-0110 D1/D2) e repostas no agregado por
/// <see cref="ProcessoSeletivo.RestaurarConfiguracaoCongelada"/>.
/// </summary>
/// <remarks>
/// <para>
/// É <b>classe</b>, e não <c>record</c>, deliberadamente: carrega entidades mutáveis
/// e listas, cuja igualdade de <c>record</c> seria <b>referencial</b>. Prometer
/// semântica de valor onde ela não existe convidaria a comparar dois grafos com
/// <c>==</c> e a concluir que diferem quando são o mesmo conteúdo — num caminho cuja
/// única prova de correção é justamente a igualdade (o round-trip byte-a-byte, que
/// se faz sobre os <b>bytes canônicos</b>, nunca sobre este objeto).
/// </para>
/// <para>
/// Vive no <b>Domain</b> porque a raiz o recebe. O envelope completo — este grafo
/// mais os <c>DadosEdital</c>, o hash do documento e a <c>RetificacaoInfo</c> — é da
/// Application, que é quem conhece o codec (ADR-0042).
/// </para>
/// </remarks>
public sealed class GrafoConfiguracao
{
    public GrafoConfiguracao(
        IReadOnlyList<EtapaProcesso> etapas,
        OfertaAtendimentoEspecializado ofertaAtendimento,
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicaoVagas,
        ConfiguracaoBonusRegional? bonusRegional,
        IReadOnlyList<CriterioDesempate> criteriosDesempate,
        ConfiguracaoClassificacao classificacao,
        IReadOnlyList<FaseCronograma> cronogramaFases,
        IReadOnlyList<DocumentoExigido> documentosExigidos,
        IReadOnlyList<NoExigencia> nosExigencia,
        ReferenciaTemporalFatos? referenciaTemporalFatos)
    {
        ArgumentNullException.ThrowIfNull(etapas);
        ArgumentNullException.ThrowIfNull(ofertaAtendimento);
        ArgumentNullException.ThrowIfNull(distribuicaoVagas);
        ArgumentNullException.ThrowIfNull(criteriosDesempate);
        ArgumentNullException.ThrowIfNull(classificacao);
        ArgumentNullException.ThrowIfNull(cronogramaFases);
        ArgumentNullException.ThrowIfNull(documentosExigidos);
        ArgumentNullException.ThrowIfNull(nosExigencia);

        // Cópias defensivas: o grafo é a fronteira entre os bytes congelados e o
        // agregado vivo. Guardar a referência do caller deixaria uma janela em que
        // mutá-la, depois da validação e antes da reposição, reporia configuração
        // que nunca foi validada.
        Etapas = [.. etapas];
        OfertaAtendimento = ofertaAtendimento;
        DistribuicaoVagas = [.. distribuicaoVagas];
        BonusRegional = bonusRegional;
        CriteriosDesempate = [.. criteriosDesempate];
        Classificacao = classificacao;
        CronogramaFases = [.. cronogramaFases];
        DocumentosExigidos = [.. documentosExigidos];
        NosExigencia = [.. nosExigencia];
        ReferenciaTemporalFatos = referenciaTemporalFatos;
    }

    public IReadOnlyList<EtapaProcesso> Etapas { get; }

    public OfertaAtendimentoEspecializado OfertaAtendimento { get; }

    public IReadOnlyList<ConfiguracaoDistribuicaoVagas> DistribuicaoVagas { get; }

    /// <summary>Ausência = sem bônus (RN05, INV-B5) — não existe "BONUS-NENHUM".</summary>
    public ConfiguracaoBonusRegional? BonusRegional { get; }

    public IReadOnlyList<CriterioDesempate> CriteriosDesempate { get; }

    public ConfiguracaoClassificacao Classificacao { get; }

    /// <summary>O cronograma de fases (Story #851) — 7ª dimensão do grafo, 1..*.</summary>
    public IReadOnlyList<FaseCronograma> CronogramaFases { get; }

    /// <summary>Documentos exigidos (Story #554, PR #903) — 8ª dimensão do grafo, 0..* (as folhas, com sua config por-exigência).</summary>
    public IReadOnlyList<DocumentoExigido> DocumentosExigidos { get; }

    /// <summary>Árvore de satisfação (Story #920) — 9ª dimensão do grafo, 0..* (TODOS os nós, planos — raízes têm <c>NoPaiId == null</c>).</summary>
    public IReadOnlyList<NoExigencia> NosExigencia { get; }

    /// <summary>
    /// Política de <see cref="ValueObjects.ReferenciaTemporalFatos"/> (Story #554, PR #903,
    /// B-03) — o INSUMO que <see cref="Entities.ProcessoSeletivo.ResolverDataReferenciaFatos"/>
    /// resolve para a <c>dataReferenciaFatos</c> congelada. Ausência = nenhuma política
    /// configurada (estado válido enquanto não existir gatilho por <c>FAIXA_ETARIA</c>).
    /// </summary>
    public ReferenciaTemporalFatos? ReferenciaTemporalFatos { get; }
}
