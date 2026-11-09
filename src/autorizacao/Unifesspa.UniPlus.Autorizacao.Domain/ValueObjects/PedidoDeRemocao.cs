namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>A retirada de permissões da versão em vigor do perfil, que vale na hora.</summary>
/// <param name="Codigos">As permissões a retirar.</param>
/// <param name="Motivo">O motivo já normalizado (<see cref="MotivosDaVersao"/>); nulo quando o texto informado não vale.</param>
/// <param name="Categoria">A categoria, quando quem remove a informa.</param>
/// <param name="Autoria">Quem remove.</param>
/// <param name="Instante">O instante da remoção, pelo relógio injetado.</param>
public sealed record PedidoDeRemocao(
    IReadOnlyList<string> Codigos,
    string? Motivo,
    CategoriaDaRemocao? Categoria,
    AutoriaDaVersao Autoria,
    DateTimeOffset Instante);
