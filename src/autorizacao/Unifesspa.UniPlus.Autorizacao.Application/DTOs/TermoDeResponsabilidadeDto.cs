namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

/// <summary>
/// Uma versão publicada do termo de responsabilidade. O resumo é o SHA-256, em hexadecimal minúsculo, dos bytes UTF-8 do
/// texto devolvido, que já vem normalizado: quem recebe recalcula o resumo sem normalizar de novo. O autor da publicação
/// fica gravado para auditoria e não é exposto.
/// </summary>
/// <param name="Vigente">A versão é a vigente agora.</param>
public sealed record VersaoDoTermoDto(Guid Id, int Numero, string Texto, string Resumo, string Ato, DateTimeOffset PublicadaEm, bool Vigente);

/// <summary>Uma versão do termo como aparece no histórico, sem o texto.</summary>
public sealed record VersaoDoTermoResumoDto(Guid Id, int Numero, string Resumo, string Ato, DateTimeOffset PublicadaEm, bool Vigente);

/// <summary>Corpo do <c>POST /api/autorizacao/termos-de-responsabilidade/versoes</c>.</summary>
/// <param name="Texto">O texto da versão, com 200 a 20 000 caracteres depois de normalizado.</param>
/// <param name="Ato">O ato institucional que aprova a versão, com até 500 caracteres.</param>
/// <param name="VersaoVigenteEsperadaId">
/// A versão vigente que a tela carregou; nula se não havia nenhuma. Se outra foi publicada depois, a publicação é recusada
/// com conflito.
/// </param>
public sealed record PublicarVersaoDoTermoRequest(string? Texto, string? Ato, Guid? VersaoVigenteEsperadaId);
