namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os nomes de tipo de ato que as conferências de mensagem recebem nos testes.</summary>
internal static class NomesDosTiposDeAtoDeTeste
{
    /// <summary>Nenhum tipo com nome: as mensagens saem na forma sem nome.</summary>
    public static NomesDosTiposDeAto Nenhum { get; } = Com();

    public static NomesDosTiposDeAto Com(params (string Codigo, string Nome)[] tipos) =>
        new(tipos.ToDictionary(static tipo => tipo.Codigo, static tipo => tipo.Nome, StringComparer.Ordinal));
}
