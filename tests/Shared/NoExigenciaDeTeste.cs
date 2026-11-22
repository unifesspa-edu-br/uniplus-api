namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Monta o nó da árvore de exigências que o pedido recebe: folha ou grupo, sem quantidade mínima, indeferimento,
/// distinção nem repetição, salvo o que o teste diz. Cada método devolve um construtor novo.
/// </summary>
internal sealed record NoExigenciaDeTeste
{
    private string Tipo { get; init; } = "FOLHA";

    private ItemDocumentoExigidoInput? Documento { get; init; }

    private IReadOnlyList<RegraDeComprovacaoInformada>? Filhos { get; init; }

    private int? QuantidadeMinima { get; init; }

    private DistincaoDasApresentacoesInput? Distincao { get; init; }

    private IndeferimentoDoGrupoInput? Indeferimento { get; init; }

    private string? RepetePorEntidade { get; init; }

    private bool ComCardinalidade { get; init; }

    public static NoExigenciaDeTeste Folha(ItemDocumentoExigidoInput documento) => new() { Documento = documento };

    public static NoExigenciaDeTeste Folha(ItemDocumentoExigidoDeTeste documento) => Folha(documento.Construir());

    /// <summary>O grupo <c>E</c> ou <c>OU</c> dos filhos.</summary>
    public static NoExigenciaDeTeste Grupo(string tipo, params RegraDeComprovacaoInformada[] filhos) => new() { Tipo = tipo, Filhos = filhos };

    public NoExigenciaDeTeste ComTipo(string tipo) => this with { Tipo = tipo };

    public NoExigenciaDeTeste ComQuantidadeMinima(int? quantidadeMinima) =>
        this with { QuantidadeMinima = quantidadeMinima, ComCardinalidade = true };

    public NoExigenciaDeTeste ComDistincao(string? chave, DateOnly? dataReferencia = null, IReadOnlyList<string>? ocorrenciasEsperadas = null) =>
        this with { Distincao = new(chave, dataReferencia, ocorrenciasEsperadas), ComCardinalidade = true };

    public NoExigenciaDeTeste ComIndeferimento(string? consequencia, params BaseLegalInput[] basesLegais) =>
        this with { Indeferimento = new(consequencia, basesLegais) };

    public NoExigenciaDeTeste RepetidoPor(string grupo) => this with { RepetePorEntidade = grupo };

    public RegraDeComprovacaoInformada Construir() =>
        new(
            Tipo,
            Documento,
            ComCardinalidade ? new CardinalidadeQualificadaInput(QuantidadeMinima, Distincao) : null,
            Indeferimento,
            Filhos,
            RepetePorEntidade);
}
