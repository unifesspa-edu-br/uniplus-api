namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>Constrói regras informadas válidas para os cenários de teste.</summary>
internal sealed record RegraDeComprovacaoDeTeste
{
    private string Forma { get; init; } = "DOCUMENTO";

    private ItemDocumentoExigidoInput? Documento { get; init; }

    private IReadOnlyList<RegraDeComprovacaoInformada>? Itens { get; init; }

    private int? QuantidadeMinima { get; init; }

    private DistincaoDasApresentacoesInput? Distincao { get; init; }

    private IndeferimentoDoGrupoInput? Indeferimento { get; init; }

    private string? GrupoDoFormularioParaRepeticao { get; init; }

    private bool ComCardinalidade { get; init; }

    public static RegraDeComprovacaoDeTeste DeDocumento(ItemDocumentoExigidoInput documento) => new() { Documento = documento };

    public static RegraDeComprovacaoDeTeste DeDocumento(ItemDocumentoExigidoDeTeste documento) => DeDocumento(documento.Construir());

    /// <summary>A regra de grupo com os itens informados.</summary>
    public static RegraDeComprovacaoDeTeste Grupo(string forma, params RegraDeComprovacaoInformada[] itens) => new() { Forma = forma, Itens = itens };

    public RegraDeComprovacaoDeTeste ComForma(string forma) => this with { Forma = forma };

    public RegraDeComprovacaoDeTeste ComQuantidadeMinima(int? quantidadeMinima) =>
        this with { QuantidadeMinima = quantidadeMinima, ComCardinalidade = true };

    public RegraDeComprovacaoDeTeste ComDistincao(string? chave, DateOnly? dataReferencia = null, IReadOnlyList<string>? ocorrenciasEsperadas = null) =>
        this with { Distincao = new(chave, dataReferencia, ocorrenciasEsperadas), ComCardinalidade = true };

    public RegraDeComprovacaoDeTeste ComIndeferimento(string? consequencia, params BaseLegalInput[] basesLegais) =>
        this with { Indeferimento = new(consequencia, basesLegais) };

    public RegraDeComprovacaoDeTeste RepetidoPor(string grupo) => this with { GrupoDoFormularioParaRepeticao = grupo };

    public RegraDeComprovacaoInformada Construir() =>
        new(
            Forma,
            Documento,
            ComCardinalidade ? new CardinalidadeQualificadaInput(QuantidadeMinima, Distincao) : null,
            Indeferimento,
            Itens,
            GrupoDoFormularioParaRepeticao);
}
