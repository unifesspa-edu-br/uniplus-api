namespace Unifesspa.UniPlus.Testes.Compartilhado;

using System.Text.Json;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Monta a exigência de documento que o pedido recebe: geral, obrigatória, com qualquer formato e sem
/// condições, bases legais, prazo de emissão, validade, modelo nem tamanho, salvo o que o teste diz. Cada método devolve um construtor
/// novo, então um ponto de partida compartilhado não vaza para os demais.
/// </summary>
internal sealed record ItemDocumentoExigidoDeTeste
{
    private static readonly JsonElement QualquerFormato = JsonSerializer.SerializeToElement("QUALQUER");

    private Guid FaseId { get; init; }

    private Guid TipoDocumentoId { get; init; }

    private Guid? EtapaId { get; init; }

    private string? FormularioCodigo { get; init; }

    private string? Aplicabilidade { get; init; } = "GERAL";

    private IReadOnlyList<CondicaoGatilhoInput> Condicoes { get; init; } = [];

    private bool? Obrigatorio { get; init; } = true;

    private bool? AbrePrazoParaReenvio { get; init; } = false;

    private string? Consequencia { get; init; }

    private IReadOnlyList<BaseLegalInput> BasesLegais { get; init; } = [];

    private JsonElement? FormatosPermitidos { get; init; } = QualquerFormato;

    private int? TamanhoMaximoBytes { get; init; }

    private PrazoDeEmissaoInput? PrazoDeEmissao { get; init; }

    private ValidadeDoDocumentoInput? Validade { get; init; }

    private Guid? ModeloId { get; init; }

    private IReadOnlyList<string>? FatosComprovados { get; init; }

    private bool ComRequisitos { get; init; } = true;

    /// <summary>A exigência geral e obrigatória do tipo de documento, na fase.</summary>
    public static ItemDocumentoExigidoDeTeste Geral(Guid faseId, Guid tipoDocumentoId) =>
        new() { FaseId = faseId, TipoDocumentoId = tipoDocumentoId };

    public ItemDocumentoExigidoDeTeste Condicional(params CondicaoGatilhoInput[] condicoes) =>
        this with { Aplicabilidade = "CONDICIONAL", Condicoes = condicoes };

    public ItemDocumentoExigidoDeTeste ComAplicabilidade(string? aplicabilidade) => this with { Aplicabilidade = aplicabilidade };

    public ItemDocumentoExigidoDeTeste Facultativa() => this with { Obrigatorio = false };

    /// <summary>A obrigatoriedade como o pedido a traz; nula, o pedido a omite.</summary>
    public ItemDocumentoExigidoDeTeste ComObrigatorio(bool? obrigatorio) => this with { Obrigatorio = obrigatorio };

    /// <summary>A escolha de prazo para reenvio como o pedido a traz; nula, o pedido a omite.</summary>
    public ItemDocumentoExigidoDeTeste ComPrazoParaReenvio(bool? abrePrazoParaReenvio) => this with { AbrePrazoParaReenvio = abrePrazoParaReenvio };

    public ItemDocumentoExigidoDeTeste ComConsequencia(string? consequencia) => this with { Consequencia = consequencia };

    public ItemDocumentoExigidoDeTeste ComBasesLegais(params BaseLegalInput[] basesLegais) => this with { BasesLegais = basesLegais };

    public ItemDocumentoExigidoDeTeste NoFormulario(string? formularioCodigo) => this with { FormularioCodigo = formularioCodigo };

    public ItemDocumentoExigidoDeTeste NaEtapa(Guid etapaId) => this with { EtapaId = etapaId };

    public ItemDocumentoExigidoDeTeste ComFormatos(JsonElement? formatos) => this with { FormatosPermitidos = formatos };

    public ItemDocumentoExigidoDeTeste ComTamanhoMaximo(int? tamanhoMaximoBytes) => this with { TamanhoMaximoBytes = tamanhoMaximoBytes };

    public ItemDocumentoExigidoDeTeste ComPrazoDeEmissao(PrazoDeEmissaoInput? prazo) => this with { PrazoDeEmissao = prazo };

    public ItemDocumentoExigidoDeTeste ComValidade(ValidadeDoDocumentoInput? validade) => this with { Validade = validade };

    public ItemDocumentoExigidoDeTeste ComModelo(Guid modeloId) => this with { ModeloId = modeloId };

    public ItemDocumentoExigidoDeTeste ComFatosComprovados(params string[] fatos) => this with { FatosComprovados = fatos };

    /// <summary>Sem o objeto dos requisitos: nem formatos, nem tamanho, nem prazo, nem validade, nem modelo.</summary>
    public ItemDocumentoExigidoDeTeste SemRequisitos() => this with { ComRequisitos = false };

    public ItemDocumentoExigidoInput Construir() =>
        new(
            new TipoDocumentoDaExigenciaInput(TipoDocumentoId),
            new MomentoDaExigenciaInput(FaseId, EtapaId, FormularioCodigo),
            new AplicabilidadeDaExigenciaInput(Aplicabilidade, Condicoes),
            new IndeferimentoDoDocumentoInput(Obrigatorio, Consequencia, BasesLegais, AbrePrazoParaReenvio),
            ComRequisitos
                ? new RequisitosDoDocumentoInput(
                    FormatosPermitidos, TamanhoMaximoBytes, PrazoDeEmissao, Validade, ModeloId is { } modelo ? new ModeloDaExigenciaInput(modelo) : null)
                : null,
            FatosComprovados);
}
