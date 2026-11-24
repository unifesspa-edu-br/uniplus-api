namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;

/// <summary>
/// Monta o grupo repetível do formulário na forma de entrada dos testes: sem dizer nada além do código, é um grupo
/// de rótulo igual ao código, sem seção, de zero ocorrências no mínimo e sem máximo, sem o candidato entre os membros,
/// que aparece sempre e nunca é obrigatório. Cada método devolve um construtor novo, então um ponto de partida
/// compartilhado não vaza para os demais.
/// </summary>
internal sealed record EntradaDoGrupoColetadoDeTeste
{
    private string Codigo { get; init; } = string.Empty;

    private int Ordem { get; init; }

    private string Rotulo { get; init; } = string.Empty;

    private string? Secao { get; init; }

    private int Minimo { get; init; }

    private int? Maximo { get; init; }

    private bool IncluiCandidato { get; init; }

    private bool TemOcorrencias { get; init; } = true;

    private IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Exibicao { get; init; }

    private string? TipoDaObrigatoriedade { get; init; } = TipoObrigatoriedade.Nunca.Codigo();

    private IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? PredicadoDaObrigatoriedade { get; init; }

    private bool TemRegras { get; init; } = true;

    private IReadOnlyList<FatoColetadoInput> Subitens { get; init; } = [];

    /// <summary>O grupo de código dado, na ordem dada, com o rótulo igual ao código até o teste dar outro.</summary>
    public static EntradaDoGrupoColetadoDeTeste Compondo(string codigo, int ordem = 0) => new() { Codigo = codigo, Ordem = ordem, Rotulo = codigo };

    public EntradaDoGrupoColetadoDeTeste ComRotulo(string rotulo) => this with { Rotulo = rotulo };

    public EntradaDoGrupoColetadoDeTeste NaSecao(string? secao) => this with { Secao = secao };

    /// <summary>O mínimo e o máximo de ocorrências; sem máximo, o grupo aceita qualquer quantidade.</summary>
    public EntradaDoGrupoColetadoDeTeste ComOcorrencias(int minimo, int? maximo = null) => this with { Minimo = minimo, Maximo = maximo };

    public EntradaDoGrupoColetadoDeTeste IncluindoOCandidato(bool inclui = true) => this with { IncluiCandidato = inclui };

    public EntradaDoGrupoColetadoDeTeste ExibidoQuando(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? exibicao) => this with { Exibicao = exibicao };

    /// <summary>O token da obrigatoriedade e, quando é <c>QUANDO</c>, o predicado dela.</summary>
    public EntradaDoGrupoColetadoDeTeste ComObrigatoriedade(
        string? tipo, IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? predicado = null) =>
        this with { TipoDaObrigatoriedade = tipo, PredicadoDaObrigatoriedade = predicado };

    public EntradaDoGrupoColetadoDeTeste ComCampos(params IReadOnlyList<FatoColetadoInput> campos) => this with { Subitens = campos };

    /// <summary>O grupo sem as ocorrências, como o pedido que as omite.</summary>
    public EntradaDoGrupoColetadoDeTeste SemOcorrencias() => this with { TemOcorrencias = false };

    /// <summary>O grupo sem as regras, como o pedido que as omite.</summary>
    public EntradaDoGrupoColetadoDeTeste SemRegras() => this with { TemRegras = false };

    public GrupoColetadoInput Construir() => new(
        Codigo,
        Ordem,
        Rotulo,
        TemOcorrencias ? new OcorrenciasDoGrupoInput(Minimo, IncluiCandidato, Maximo) : null,
        TemRegras ? new ExibicaoEObrigatoriedadeInput(Exibicao, new ObrigatoriedadeInput(TipoDaObrigatoriedade, PredicadoDaObrigatoriedade)) : null,
        Subitens,
        Secao);
}
