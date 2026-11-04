namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os blocos do envelope já lidos, antes da conferência de coerência entre eles. É o resultado da
/// leitura e o insumo da montagem do <see cref="EnvelopeReidratado"/>.
/// </summary>
internal sealed class BlocosDoEnvelope
{
    public DadosEdital? Dados { get; set; }

    public string HashDocumento { get; set; } = string.Empty;

    public IReadOnlyList<EtapaProcesso> Etapas { get; set; } = [];

    public IReadOnlyList<ConfiguracaoDistribuicaoVagas> Distribuicao { get; set; } = [];

    public OfertaAtendimentoEspecializado? Atendimento { get; set; }

    public ConfiguracaoBonusRegional? Bonus { get; set; }

    public ConfiguracaoCascataRemanejamento? Cascata { get; set; }

    public IReadOnlyList<CriterioDesempate> Desempate { get; set; } = [];

    public ConfiguracaoClassificacao? Classificacao { get; set; }

    public IReadOnlyList<FaseCronograma> CronogramaFases { get; set; } = [];

    public ResultadoConformidade? Conformidade { get; set; }

    public IReadOnlyList<DocumentoExigido> DocumentosExigidos { get; set; } = [];

    public ReferenciaTemporalFatos? ReferenciaTemporalFatos { get; set; }

    public IReadOnlyDictionary<string, MetadadoFatoCongelado>? MetadadosFatosCongelados { get; set; }

    public IReadOnlyList<NoExigencia> Raizes { get; set; } = [];

    public IReadOnlyList<FormularioProcesso> Formularios { get; set; } = [];

    public IReadOnlyList<TermoExigidoFormulario> TermosExigidos { get; set; } = [];

    public ConfiguracaoDivulgacao? Divulgacao { get; set; }

    public ConfiguracaoTaxaInscricao? TaxaInscricao { get; set; }

    public LocalidadeRegente? Localidade { get; set; }

    public string? FusoHorario { get; set; }

    public int? NumeroDeOpcoesDeCurso { get; set; }

    public ReferenciaRegra? AlgoritmoContagemPrazo { get; set; }

    public CalendarioDiasUteisCongelado? CalendarioDiasUteis { get; set; }

    public IdentificadorLegivel? IdentificadorLegivel { get; set; }

    public RetificacaoInfo? Retificacao { get; set; }

    public IReadOnlyList<FatoColetado> FatosColetados { get; set; } = [];

    public Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> ValoresSelecionaveis { get; set; } = [];

    public IReadOnlyList<GrupoColetado> GruposColetados { get; set; } = [];

    public List<DefinicaoAgregado> AgregadosDosGrupos { get; } = [];

    public IReadOnlyList<ConfiguracaoDerivacaoFato> RegrasDerivacao { get; set; } = [];

    public string VersaoInterpretador { get; set; } = string.Empty;

    public Dictionary<string, ApresentacaoDoFato> ApresentacaoDosPressupostos { get; set; } = [];

    public Dictionary<string, TipoDeAtoCongelado> TiposDeAtoCongelados { get; set; } = [];

    public Dictionary<string, FatoDerivadoCongelado> FatosDerivadosCongelados { get; set; } = [];
}
