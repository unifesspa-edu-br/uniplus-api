namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// A definição de um tipo de conceito que as regras aceitaram: só <see cref="DefinicaoInformada.Validar"/> a
/// produz. Textos já normalizados; <see cref="Escala"/> só para o numérico e <see cref="Valores"/> só para o categórico.
/// </summary>
public sealed class DefinicaoValidada
{
    internal DefinicaoValidada(
        string nome, string? descricao, NaturezaConceito natureza, EscalaNumerica? escala, IReadOnlyList<ValorCategorico> valores)
    {
        Nome = nome;
        Descricao = descricao;
        Natureza = natureza;
        Escala = escala;
        Valores = valores;
    }

    public string Nome { get; }
    public string? Descricao { get; }
    public NaturezaConceito Natureza { get; }
    public EscalaNumerica? Escala { get; }
    public IReadOnlyList<ValorCategorico> Valores { get; }
}
