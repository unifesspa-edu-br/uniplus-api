namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Monta o pedido de definição do FADA dos testes: sem dizer nada além do processo, o processo não aplica o
/// FADA e nenhum campo da configuração vem. Cada método devolve um construtor novo, então um ponto de partida
/// compartilhado não vaza para os demais.
/// </summary>
internal sealed record FadaDeTeste
{
    private Guid ProcessoSeletivoId { get; init; }

    private bool Aplica { get; init; }

    private ReferenciaRegraInput? Regra { get; init; }

    private decimal? Fator { get; init; }

    private decimal? Teto { get; init; }

    private string? FatoElegibilidadeCodigo { get; init; }

    private BaseLegalDoFadaInput? BaseLegal { get; init; }

    public static FadaDeTeste Para(Guid processoSeletivoId) => new() { ProcessoSeletivoId = processoSeletivoId };

    public FadaDeTeste QueAplica() => this with { Aplica = true };

    public FadaDeTeste ComRegra(string? codigo, string? versao = "v1") => this with { Regra = new(codigo, versao) };

    public FadaDeTeste ComFator(decimal? fator) => this with { Fator = fator };

    public FadaDeTeste ComTeto(decimal? teto) => this with { Teto = teto };

    public FadaDeTeste ComFatoDeElegibilidade(string? codigo) => this with { FatoElegibilidadeCodigo = codigo };

    public FadaDeTeste ComBaseLegal(Guid baseLegalId) => this with { BaseLegal = new(baseLegalId) };

    public DefinirFadaCommand Construir() =>
        new(ProcessoSeletivoId, Aplica, Regra, Fator, Teto, FatoElegibilidadeCodigo, BaseLegal);
}
