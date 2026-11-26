namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Fator de Atenuação das Desigualdades de Acesso (FADA, UNI-REQ-0015) do <see cref="ProcessoSeletivo"/>:
/// referencia a regra tipada <c>FADA_MULTIPLICATIVO</c>
/// do <c>regras_do_catalogo</c> e seus parâmetros (<see cref="Fator"/>, <see cref="Teto"/>).
/// </summary>
/// <remarks>
/// <strong>Declaração e configuração (UNI-REQ-0015):</strong> não existe regra "sem FADA". O processo
/// declara se aplica o FADA (<see cref="ProcessoSeletivo.AplicaFada"/>): esta entidade
/// só existe quando a declaração é verdadeira, e é a sua presença, com a regra tipada, que
/// habilita o FADA. A ausência da entidade só significa "sem FADA" quando a declaração é falsa;
/// com a declaração nula, significa "por declarar" e bloqueia a publicação. O FADA se aplica
/// sobre a nota final, após os pesos (decisão do P.O.: multiplicativo, ex. ×1,20, sem teto).
/// <para>
/// <strong>Elegibilidade (ADR-0142):</strong> quem recebe o Fator de Atenuação das Desigualdades
/// de Acesso é decidido pelo fato booleano que a configuração cita
/// (<see cref="FatoElegibilidadeCodigo"/>): o acréscimo vale para o candidato em quem esse
/// fato resolve verdadeiro. O fato é do processo — coletado no formulário ou derivado por regra —,
/// nunca um código fixo no sistema. Com inscrição própria, a citação é exigida para publicar; com
/// resultado importado, a classificação já vem pronta e não há o que citar.
/// </para>
/// </remarks>
public sealed class ConfiguracaoDoFada : EntityBase, IParteDoProcesso
{
    public const int FatoElegibilidadeCodigoMaxLength = FatoDoCatalogo.CodigoMaxLength;

    private const int FatorPrecisao = 6;
    private const int FatorEscala = 4;
    private const int TetoPrecisao = 6;
    private const int TetoEscala = 4;

    private readonly List<MunicipioDoFada> _municipios = [];

    public Guid ProcessoSeletivoId { get; private set; }
    public ReferenciaRegra Regra { get; private set; } = null!;
    public decimal Fator { get; private set; }
    public decimal? Teto { get; private set; }

    /// <summary>O fato booleano do processo que decide quem recebe o acréscimo (ADR-0142).</summary>
    public string? FatoElegibilidadeCodigo { get; private set; }

    /// <summary>Id do cadastro de Base Legal do FADA (Configuração) referenciado.</summary>
    public Guid BaseLegalId { get; private set; }

    /// <summary>Snapshot congelado do código estável da Base Legal, o que o documento de configuração usa para referenciá-la.</summary>
    public string BaseLegalCodigo { get; private set; } = string.Empty;

    /// <summary>Snapshot congelado no momento da gravação — código canônico UPPER_SNAKE.</summary>
    public string TipoInstrumento { get; private set; } = string.Empty;

    /// <summary>Snapshot congelado da identificação da Base Legal (ex.: "Portaria Unifesspa nº 2514/2023").</summary>
    public string Identificacao { get; private set; } = string.Empty;

    /// <summary>Snapshot congelado da descrição da Base Legal.</summary>
    public string Descricao { get; private set; } = string.Empty;

    /// <summary>Snapshot congelado dos municípios abrangidos pela Base Legal no momento da gravação.</summary>
    public IReadOnlyList<MunicipioDoFada> Municipios => _municipios.AsReadOnly();

    private ConfiguracaoDoFada() { }

    /// <summary>
    /// Acumula toda violação independente em vez de retornar na primeira (ADR-0125) — o array
    /// <c>errors[]</c> do contrato público (ADR-0023) precisa de todas as regras violadas no
    /// mesmo lote. A <paramref name="baseLegal"/> não é revalidada aqui: chega já congelada de um
    /// <c>BaseLegalDoFadaView</c> resolvido e validado pelo handler (ou lida do envelope) — revalidar duplicaria a fonte
    /// de verdade do cadastro de origem.
    /// </summary>
    public static Result<ConfiguracaoDoFada> Criar(
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
            ? Result<ConfiguracaoDoFada>.ValidationFailure(erros)
            : Result<ConfiguracaoDoFada>.Success(Montar(regra, fator, teto, fatoElegibilidade, baseLegal));
    }

    private static FieldError?[] Recusas(ReferenciaRegra regra, decimal fator, decimal? teto, string? fato, BaseLegalCongelada baseLegal) =>
    [
        RecusaDaRegra(regra),
        RecusaDoFator(fator),
        PrecisaoDecimal.Recusa(fator, FatorPrecisao, FatorEscala, "fator", "O multiplicador do Fator de Atenuação das Desigualdades de Acesso (FADA) deve ter no máximo 2 dígitos inteiros e 4 casas decimais."),
        RecusaDoTeto(teto),
        PrecisaoDecimal.Recusa(teto, TetoPrecisao, TetoEscala, "teto", "O teto do Fator de Atenuação das Desigualdades de Acesso (FADA) deve ter no máximo 2 dígitos inteiros e 4 casas decimais."),
        RecusaDoFato(fato), RecusaDosMunicipios(baseLegal),
    ];

    private static FieldError? RecusaDaRegra(ReferenciaRegra regra) =>
        regra.Codigo == RegraDoFada.Multiplicativo.Codigo()
            ? null
            : new("regra.codigo", new DomainError(
                "ConfiguracaoDoFada.RegraInvalida",
                $"A regra do Fator de Atenuação das Desigualdades de Acesso (FADA) precisa ser do código {RegraDoFada.Multiplicativo.Codigo()}."));

    private static FieldError? RecusaDoFator(decimal fator) =>
        fator > 0
            ? null
            : new("fator", new DomainError("ConfiguracaoDoFada.FatorInvalido", "O multiplicador do Fator de Atenuação das Desigualdades de Acesso (FADA) deve ser maior que zero."));

    private static FieldError? RecusaDoTeto(decimal? teto) =>
        teto is <= 0
            ? new("teto", new DomainError("ConfiguracaoDoFada.TetoInvalido", "O teto do Fator de Atenuação das Desigualdades de Acesso (FADA), quando informado, deve ser maior que zero."))
            : null;

    private static FieldError? RecusaDoFato(string? fatoElegibilidade) =>
        fatoElegibilidade is not null && (fatoElegibilidade.Length > FatoElegibilidadeCodigoMaxLength || !FormatoDoCodigo.TemFormatoValido(fatoElegibilidade))
            ? new("fatoElegibilidadeCodigo", new DomainError(
                "ConfiguracaoDoFada.FatoElegibilidadeInvalido",
                $"O código do fato que decide quem recebe o acréscimo deve começar com letra maiúscula, conter apenas letras maiúsculas, dígitos ou sublinhados separados e ter até {FatoElegibilidadeCodigoMaxLength} caracteres."))
            : null;

    // Defesa em profundidade: o handler já resolve os municípios a partir de um BaseLegalDoFadaView não-vazio, mas o
    // decoder do envelope reconstrói a configuração a partir de bytes e precisa da MESMA invariante para recusar um envelope
    // adulterado com municipios:[] em vez de aceitá-lo como FADA válido.
    private static FieldError? RecusaDosMunicipios(BaseLegalCongelada baseLegal) =>
        baseLegal.Municipios.Count > 0
            ? null
            : new(null, new DomainError(
                "ConfiguracaoDoFada.SemMunicipios",
                "A base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) deve abranger pelo menos um município."));

    private static ConfiguracaoDoFada Montar(
        ReferenciaRegra regra, decimal fator, decimal? teto, string? fatoElegibilidade, BaseLegalCongelada baseLegal)
    {
        ConfiguracaoDoFada fada = new() { Regra = regra, Fator = fator, Teto = teto, FatoElegibilidadeCodigo = fatoElegibilidade };
        fada.CongelarBaseLegal(baseLegal);

        return fada;
    }

    private void CongelarBaseLegal(BaseLegalCongelada baseLegal)
    {
        BaseLegalId = baseLegal.Id;
        BaseLegalCodigo = baseLegal.Codigo;
        TipoInstrumento = baseLegal.TipoInstrumento;
        Identificacao = baseLegal.Identificacao;
        Descricao = baseLegal.Descricao;
        CongelarMunicipios(baseLegal.Municipios);
    }

    private void CongelarMunicipios(IEnumerable<(string CodigoIbge, string Nome, string Uf)> municipios)
    {
        foreach ((string codigoIbge, string nome, string uf) in municipios)
        {
            MunicipioDoFada municipio = MunicipioDoFada.Criar(codigoIbge, nome, uf);
            municipio.VincularConfiguracao(Id);
            _municipios.Add(municipio);
        }
    }

    void IParteDoProcesso.VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;
}
