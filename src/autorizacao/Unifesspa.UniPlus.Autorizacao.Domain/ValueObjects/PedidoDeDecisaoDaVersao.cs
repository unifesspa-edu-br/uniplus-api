namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>A decisão que uma pessoa pede sobre uma etapa da aprovação de uma versão do perfil.</summary>
/// <param name="VersaoId">A versão decidida.</param>
/// <param name="Etapa">A etapa que a pessoa decide.</param>
/// <param name="Resultado">Aprovada ou recusada.</param>
/// <param name="Motivo">O motivo já normalizado (<see cref="MotivosDaVersao"/>); obrigatório na recusa e ignorado na aprovação.</param>
/// <param name="Autoria">Quem decide.</param>
/// <param name="Instante">O instante da decisão, pelo relógio injetado.</param>
public sealed record PedidoDeDecisaoDaVersao(
    Guid VersaoId,
    EtapaDaVersao Etapa,
    ResultadoDaDecisao Resultado,
    string? Motivo,
    AutoriaDaVersao Autoria,
    DateTimeOffset Instante);
