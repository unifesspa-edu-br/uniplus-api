namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Regras.Entradas;

/// <summary>
/// Monta a página do formulário na forma de entrada dos testes: sem dizer nada além do código e da ordem, é uma seção
/// de título igual ao código, sem descrição, sem aviso e que aparece sempre. Cada método devolve um construtor novo, então
/// um ponto de partida compartilhado não vaza para os demais.
/// </summary>
internal sealed record EntradaDaPaginaDoFormularioDeTeste
{
    private string Codigo { get; init; } = string.Empty;

    private int Ordem { get; init; }

    private string Tipo { get; init; } = "SECAO";

    private string? Bloco { get; init; }

    private string? Titulo { get; init; }

    private string? Descricao { get; init; }

    private string? Aviso { get; init; }

    private bool TemApresentacao { get; init; } = true;

    private IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Exibicao { get; init; }

    /// <summary>A seção de código e ordem dados, com o título igual ao código até o teste dar outro.</summary>
    public static EntradaDaPaginaDoFormularioDeTeste Secao(string codigo, int ordem = 0) =>
        new() { Codigo = codigo, Ordem = ordem, Titulo = codigo };

    /// <summary>O bloco de sistema de código e ordem dados, com o título igual ao código até o teste dar outro.</summary>
    public static EntradaDaPaginaDoFormularioDeTeste BlocoDeSistema(string codigo, int ordem, string bloco) =>
        new() { Codigo = codigo, Ordem = ordem, Tipo = "BLOCO", Bloco = bloco, Titulo = codigo };

    public EntradaDaPaginaDoFormularioDeTeste ComTitulo(string? titulo) => this with { Titulo = titulo };

    public EntradaDaPaginaDoFormularioDeTeste ComDescricao(string? descricao) => this with { Descricao = descricao };

    public EntradaDaPaginaDoFormularioDeTeste ComAviso(string? aviso) => this with { Aviso = aviso };

    public EntradaDaPaginaDoFormularioDeTeste ExibidaQuando(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? exibicao) => this with { Exibicao = exibicao };

    /// <summary>A página sem a apresentação, como o pedido que a omite.</summary>
    public EntradaDaPaginaDoFormularioDeTeste SemApresentacao() => this with { TemApresentacao = false };

    public PaginaDoFormularioInput Construir() => new(
        Codigo,
        Ordem,
        Tipo,
        Bloco,
        TemApresentacao ? new ApresentacaoDaPaginaInput(Titulo, Descricao, Aviso) : null,
        Exibicao);
}
