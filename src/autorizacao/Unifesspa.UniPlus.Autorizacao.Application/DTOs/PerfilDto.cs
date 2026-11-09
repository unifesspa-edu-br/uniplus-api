namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

/// <summary>Perfil de acesso como a API o expõe.</summary>
/// <param name="Nome">O nome corrente: o da versão vigente ou, sem vigente, o da pendente.</param>
/// <param name="Governanca">Papel de governança do perfil de sistema; nulo nos perfis comuns.</param>
/// <param name="Sensibilidade">A mais alta entre as permissões da composição em vigor (sem vigente, da pendente), derivada do catálogo.</param>
/// <param name="ComposicaoEmVigor">O que decide acesso hoje; nulo no perfil comum sem versão vigente.</param>
/// <param name="VersaoPendente">A versão que espera o documento ser verificado; nula quando não há.</param>
/// <param name="UltimaVersaoRecusada">Preenchida quando a versão mais recente do perfil foi recusada na entrada em vigor.</param>
public sealed record PerfilDto(
    Guid Id,
    string Nome,
    string Descricao,
    bool Ativo,
    string? Governanca,
    string Sensibilidade,
    ComposicaoEmVigorDto? ComposicaoEmVigor,
    VersaoPendenteDto? VersaoPendente,
    UltimaVersaoRecusadaDto? UltimaVersaoRecusada);

/// <summary>Perfil de acesso como a listagem o resume.</summary>
/// <param name="TemVersaoVigente">O perfil comum tem composição em vigor; o de governança dispensa a versão e a tem sempre.</param>
/// <param name="TemVersaoPendente">Há versão esperando o documento ser verificado.</param>
public sealed record PerfilListadoDto(
    Guid Id,
    string Nome,
    string Descricao,
    bool Ativo,
    string? Governanca,
    string Sensibilidade,
    bool TemVersaoVigente,
    bool TemVersaoPendente);

/// <summary>A composição que decide acesso agora. Os campos da versão são nulos no perfil de governança, que não é versionado.</summary>
public sealed record ComposicaoEmVigorDto(
    Guid? VersaoId,
    int? Numero,
    IReadOnlyList<PermissaoDoPerfilDto> Permissoes,
    string? Protocolo,
    DateTimeOffset? DocumentoVerificadoEm,
    DateTimeOffset? Desde);

/// <summary>Versão pendente: o que ela passará a valer e a situação do documento que a institui.</summary>
public sealed record VersaoPendenteDto(
    Guid VersaoId,
    int Numero,
    string Nome,
    string Descricao,
    IReadOnlyList<PermissaoDoPerfilDto> Permissoes,
    string Protocolo,
    Guid DocumentoId,
    string SituacaoDoDocumento,
    DateTimeOffset CriadaEm);

/// <summary>A versão mais recente do perfil, quando foi recusada, e por quê.</summary>
public sealed record UltimaVersaoRecusadaDto(int Numero, string Motivo, DateTimeOffset RecusadaEm);

/// <summary>Permissão reunida por um perfil, com nome e sensibilidade lidos do catálogo.</summary>
public sealed record PermissaoDoPerfilDto(string Codigo, string Nome, string Sensibilidade);
