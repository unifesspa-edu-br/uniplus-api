namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// O ato que o comando de publicação aceitou. O registro no Publicações acontece depois, por mensagem
/// durável: o ato aparece como pendente na consulta dos atos do processo até o registro se confirmar.
/// </summary>
/// <param name="AtoId">Identificador do ato, o mesmo que a consulta dos atos do processo devolve.</param>
public sealed record AtoPublicadoDto(Guid AtoId);
