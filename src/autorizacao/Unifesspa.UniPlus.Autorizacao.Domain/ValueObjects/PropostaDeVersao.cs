namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// A versão que uma pessoa propõe para o perfil e, quando ela retira permissões da versão em vigor, o motivo e a
/// categoria dessa remoção, que vale na hora.
/// </summary>
/// <param name="Conteudo">O que a versão declara, já validado.</param>
/// <param name="Motivo">O motivo da remoção já normalizado (<see cref="MotivosDaVersao"/>); nulo quando o texto informado não vale.</param>
/// <param name="Categoria">A categoria da remoção, quando quem propõe a informa.</param>
/// <param name="Autoria">Quem propõe.</param>
/// <param name="Instante">O instante da proposta, pelo relógio injetado.</param>
public sealed record PropostaDeVersao(
    ConteudoDaVersao Conteudo,
    string? Motivo,
    CategoriaDaRemocao? Categoria,
    AutoriaDaVersao Autoria,
    DateTimeOffset Instante)
{
    /// <summary>A remoção das permissões que a proposta retira da vigente.</summary>
    internal PedidoDeRemocao DaRemocao(IReadOnlyList<string> codigos) => new(codigos, Motivo, Categoria, Autoria, Instante);
}
