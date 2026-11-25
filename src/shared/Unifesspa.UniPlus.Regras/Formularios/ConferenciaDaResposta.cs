namespace Unifesspa.UniPlus.Regras.Formularios;

internal sealed record ConferenciaDaResposta(IReadOnlyList<RestricaoValor> Violadas, bool Indeterminada);
