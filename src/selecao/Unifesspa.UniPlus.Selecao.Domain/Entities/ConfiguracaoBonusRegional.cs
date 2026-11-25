namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Bônus regional do <see cref="ProcessoSeletivo"/> (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015):
/// referencia a regra tipada <c>FADA_MULTIPLICATIVO</c>
/// do <c>regras_do_catalogo</c> e seus parâmetros (<see cref="Fator"/>, <see cref="Teto"/>).
/// </summary>
/// <remarks>
/// <strong>Declaração e configuração (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015):</strong> não existe "BONUS-NENHUM". O processo
/// declara se aplica o bônus (<see cref="ProcessoSeletivo.AplicaBonusRegional"/>): esta entidade
/// só existe quando a declaração é verdadeira, e é a sua presença, com a regra tipada, que
/// habilita o bônus. A ausência da entidade só significa "sem bônus" quando a declaração é falsa;
/// com a declaração nula, significa "por declarar" e bloqueia a publicação. O bônus se aplica
/// sobre a nota final, após os pesos (decisão do P.O.: multiplicativo, ex. ×1,20, sem teto).
/// <para>
/// <strong>Elegibilidade (ADR-0142):</strong> quem recebe o Fator de Atenuação das Desigualdades
/// de Acesso é decidido pelo fato booleano que a configuração cita
/// (<see cref="FatoElegibilidadeCodigo"/>): o motor aplica o acréscimo ao candidato em quem esse
/// fato resolve verdadeiro. O fato é do processo — coletado no formulário ou derivado por regra —,
/// nunca um código fixo no sistema. Com inscrição própria, a citação é exigida para publicar; com
/// resultado importado, a classificação já vem pronta e não há o que citar.
/// </para>
/// </remarks>
public sealed class ConfiguracaoBonusRegional : EntityBase, IParteDoProcesso
{
    private const int FatorPrecisao = 6;
    private const int FatorEscala = 4;
    private const int TetoPrecisao = 6;
    private const int TetoEscala = 4;

    private readonly List<ConfiguracaoBonusRegionalMunicipio> _municipios = [];

    public Guid ProcessoSeletivoId { get; private set; }
    public ReferenciaRegra Regra { get; private set; } = null!;
    public decimal Fator { get; private set; }
    public decimal? Teto { get; private set; }

    /// <summary>O fato booleano do processo que decide quem recebe o acréscimo (ADR-0142).</summary>
    public string? FatoElegibilidadeCodigo { get; private set; }

    /// <summary>Id do cadastro de Base Legal de Bônus Regional (Configuração) referenciado.</summary>
    public Guid BaseLegalBonusRegionalId { get; private set; }

    /// <summary>Snapshot congelado do código estável da Base Legal, o que o documento de configuração usa para referenciá-la.</summary>
    public string BaseLegalBonusRegionalCodigo { get; private set; } = string.Empty;

    /// <summary>Snapshot congelado no momento da gravação — código canônico UPPER_SNAKE.</summary>
    public string TipoInstrumento { get; private set; } = string.Empty;

    /// <summary>Snapshot congelado da identificação da Base Legal (ex.: "Portaria Unifesspa nº 2514/2023").</summary>
    public string Identificacao { get; private set; } = string.Empty;

    /// <summary>Snapshot congelado da descrição da Base Legal.</summary>
    public string Descricao { get; private set; } = string.Empty;

    /// <summary>Snapshot congelado dos municípios abrangidos pela Base Legal no momento da gravação.</summary>
    public IReadOnlyList<ConfiguracaoBonusRegionalMunicipio> Municipios => _municipios.AsReadOnly();

    private ConfiguracaoBonusRegional() { }

    /// <summary>
    /// Acumula toda violação independente em vez de retornar na primeira (ADR-0125) — o array
    /// <c>errors[]</c> do contrato público (ADR-0023) precisa de todas as regras violadas no
    /// mesmo lote. A <paramref name="baseLegal"/> não é revalidada aqui: chega já congelada de um
    /// <c>BaseLegalBonusRegionalView</c> resolvido e validado pelo handler (ou lida do envelope) — revalidar duplicaria a fonte
    /// de verdade do cadastro de origem.
    /// </summary>
    public static Result<ConfiguracaoBonusRegional> Criar(
        ReferenciaRegra regra,
        decimal fator,
        decimal? teto,
        string? fatoElegibilidadeCodigo,
        BaseLegalCongelada baseLegal)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(baseLegal);

        string? fatoElegibilidade = fatoElegibilidadeCodigo;
        FieldError[] erros = [.. Recusas(regra, fator, teto, fatoElegibilidade, baseLegal).OfType<FieldError>()];

        return erros.Length > 0
            ? Result<ConfiguracaoBonusRegional>.ValidationFailure(erros)
            : Result<ConfiguracaoBonusRegional>.Success(Montar(regra, fator, teto, fatoElegibilidade, baseLegal));
    }

    private static FieldError?[] Recusas(ReferenciaRegra regra, decimal fator, decimal? teto, string? fato, BaseLegalCongelada baseLegal) =>
    [
        RecusaDaRegra(regra),
        RecusaDoFator(fator),
        PrecisaoDecimal.Recusa(fator, FatorPrecisao, FatorEscala, "fator", "O fator do bônus deve ter no máximo 2 dígitos inteiros e 4 casas decimais."),
        RecusaDoTeto(teto),
        PrecisaoDecimal.Recusa(teto, TetoPrecisao, TetoEscala, "teto", "O teto do bônus deve ter no máximo 2 dígitos inteiros e 4 casas decimais."),
        RecusaDoFato(fato), RecusaDosMunicipios(baseLegal),
    ];

    private static FieldError? RecusaDaRegra(ReferenciaRegra regra) =>
        regra.Codigo == RegraDeBonus.Multiplicativo.Codigo()
            ? null
            : new("regra.codigo", new DomainError(
                "ConfiguracaoBonusRegional.RegraInvalida",
                $"A regra do bônus precisa ser do código {RegraDeBonus.Multiplicativo.Codigo()}."));

    private static FieldError? RecusaDoFator(decimal fator) =>
        fator > 0
            ? null
            : new("fator", new DomainError("ConfiguracaoBonusRegional.FatorInvalido", "O fator do bônus deve ser maior que zero."));

    private static FieldError? RecusaDoTeto(decimal? teto) =>
        teto is <= 0
            ? new("teto", new DomainError("ConfiguracaoBonusRegional.TetoInvalido", "O teto do bônus, quando informado, deve ser maior que zero."))
            : null;

    private static FieldError? RecusaDoFato(string? fatoElegibilidade) =>
        fatoElegibilidade is not null && string.IsNullOrWhiteSpace(fatoElegibilidade) || fatoElegibilidade?.Length > FormaDoItem.FatoCodigoMaxLength
            ? new("fatoElegibilidadeCodigo", new DomainError(
                "ConfiguracaoBonusRegional.FatoElegibilidadeInvalido",
                $"O código do fato que decide quem recebe o acréscimo, quando citado, tem de 1 a {FormaDoItem.FatoCodigoMaxLength} caracteres."))
            : null;

    // Defesa em profundidade: o handler já resolve os municípios a partir de um BaseLegalBonusRegionalView não-vazio, mas o
    // decoder do envelope reconstrói a configuração a partir de bytes e precisa da MESMA invariante para recusar um envelope
    // adulterado com municipios:[] em vez de aceitá-lo como bônus válido.
    private static FieldError? RecusaDosMunicipios(BaseLegalCongelada baseLegal) =>
        baseLegal.Municipios.Count > 0
            ? null
            : new(null, new DomainError(
                "ConfiguracaoBonusRegional.SemMunicipios",
                "A base legal do bônus regional deve abranger pelo menos um município."));

    private static ConfiguracaoBonusRegional Montar(
        ReferenciaRegra regra, decimal fator, decimal? teto, string? fatoElegibilidade, BaseLegalCongelada baseLegal)
    {
        ConfiguracaoBonusRegional bonus = new() { Regra = regra, Fator = fator, Teto = teto, FatoElegibilidadeCodigo = fatoElegibilidade };
        bonus.CongelarBaseLegal(baseLegal);

        return bonus;
    }

    private void CongelarBaseLegal(BaseLegalCongelada baseLegal)
    {
        BaseLegalBonusRegionalId = baseLegal.Id;
        BaseLegalBonusRegionalCodigo = baseLegal.Codigo;
        TipoInstrumento = baseLegal.TipoInstrumento;
        Identificacao = baseLegal.Identificacao;
        Descricao = baseLegal.Descricao;
        CongelarMunicipios(baseLegal.Municipios);
    }

    private void CongelarMunicipios(IEnumerable<(string CodigoIbge, string Nome, string Uf)> municipios)
    {
        foreach ((string codigoIbge, string nome, string uf) in municipios)
        {
            ConfiguracaoBonusRegionalMunicipio municipio = ConfiguracaoBonusRegionalMunicipio.Criar(codigoIbge, nome, uf);
            municipio.VincularConfiguracao(Id);
            _municipios.Add(municipio);
        }
    }

    void IParteDoProcesso.VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;
}
