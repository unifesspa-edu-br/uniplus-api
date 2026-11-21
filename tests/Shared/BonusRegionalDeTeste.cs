namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Monta o pedido de definição do bônus regional dos testes: sem dizer nada além do processo, o processo não aplica o
/// bônus e nenhum campo da configuração vem. Cada método devolve um construtor novo, então um ponto de partida
/// compartilhado não vaza para os demais.
/// </summary>
internal sealed record BonusRegionalDeTeste
{
    private Guid ProcessoSeletivoId { get; init; }

    private bool Aplica { get; init; }

    private ReferenciaRegraInput? Regra { get; init; }

    private decimal? Fator { get; init; }

    private decimal? Teto { get; init; }

    private string? FatoElegibilidadeCodigo { get; init; }

    private BaseLegalDoBonusInput? BaseLegal { get; init; }

    public static BonusRegionalDeTeste Para(Guid processoSeletivoId) => new() { ProcessoSeletivoId = processoSeletivoId };

    public BonusRegionalDeTeste QueAplica() => this with { Aplica = true };

    public BonusRegionalDeTeste ComRegra(string? codigo, string? versao = "v1") => this with { Regra = new(codigo, versao) };

    public BonusRegionalDeTeste ComFator(decimal? fator) => this with { Fator = fator };

    public BonusRegionalDeTeste ComTeto(decimal? teto) => this with { Teto = teto };

    public BonusRegionalDeTeste ComFatoDeElegibilidade(string? codigo) => this with { FatoElegibilidadeCodigo = codigo };

    public BonusRegionalDeTeste ComBaseLegal(Guid baseLegalId) => this with { BaseLegal = new(baseLegalId) };

    public DefinirBonusRegionalCommand Construir() =>
        new(ProcessoSeletivoId, Aplica, Regra, Fator, Teto, FatoElegibilidadeCodigo, BaseLegal);
}
