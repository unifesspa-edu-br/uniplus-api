namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

/// <summary>Corpo da aprovação de uma etapa: <c>gestao_de_acessos</c> ou <c>protecao_de_dados</c>.</summary>
public sealed record AprovarEtapaDaVersaoRequest(string? Etapa);

/// <summary>Corpo da recusa de uma etapa, com o motivo obrigatório.</summary>
public sealed record RecusarEtapaDaVersaoRequest(string? Etapa, string? Motivo);

/// <summary>Corpo do cancelamento da versão pendente, com o motivo obrigatório.</summary>
public sealed record CancelarVersaoDoPerfilRequest(string? Motivo);

/// <summary>
/// Corpo da remoção de permissões do perfil: as permissões a retirar da versão em vigor, o motivo obrigatório e a
/// categoria, que é opcional.
/// </summary>
public sealed record RemoverPermissoesRequest(IReadOnlyList<string>? Permissoes, string? Motivo, string? Categoria);
