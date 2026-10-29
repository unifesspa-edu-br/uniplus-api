namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

/// <summary>Perfil de acesso como a API o expõe.</summary>
/// <param name="Governanca">Papel de governança do perfil de sistema; nulo nos perfis comuns.</param>
/// <param name="Sensibilidade">A mais alta entre as permissões do perfil, derivada do catálogo.</param>
public sealed record PerfilDto(
    Guid Id,
    string Nome,
    string Descricao,
    bool Ativo,
    string? Governanca,
    string Sensibilidade,
    IReadOnlyList<PermissaoDoPerfilDto> Permissoes);

/// <summary>Permissão reunida por um perfil, com nome e sensibilidade lidos do catálogo.</summary>
public sealed record PermissaoDoPerfilDto(string Codigo, string Nome, string Sensibilidade);
