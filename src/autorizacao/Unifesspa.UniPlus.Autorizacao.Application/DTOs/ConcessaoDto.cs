namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

/// <summary>Concessão de acesso como a API a expõe. Nunca traz nome nem CPF.</summary>
/// <param name="RevogadaPor">Quem revogou; nulo enquanto a concessão não foi revogada.</param>
/// <param name="ValidaAte">Fim da vigência, quando a concessão é por prazo.</param>
/// <param name="Ativa">Derivada da revogação e da vigência no momento da leitura.</param>
public sealed record ConcessaoDto(
    Guid Id,
    IdentidadeDto Beneficiario,
    PerfilResumoDto Perfil,
    EscopoDto Escopo,
    OrigemDto Origem,
    DateTimeOffset ConcedidaEm,
    DateTimeOffset? ValidaAte,
    DateTimeOffset? RevogadaEm,
    IdentidadeDto? RevogadaPor,
    string? MotivoRevogacao,
    bool Ativa);

/// <summary>Pessoa pelo par emissor e <c>sub</c> do token.</summary>
public sealed record IdentidadeDto(string Emissor, string Sub);

public sealed record PerfilResumoDto(Guid Id, string Nome);

public sealed record EscopoDto(string Tipo, Guid? UnidadeId);

public sealed record OrigemDto(string Tipo, Guid Id);

/// <summary>Evento da trilha de uma concessão, em ordem cronológica.</summary>
/// <param name="Tipo"><c>concedida</c> ou <c>revogada</c>; as origens acrescentam os seus.</param>
public sealed record EventoDaTrilhaDto(string Tipo, DateTimeOffset Instante, IdentidadeDto Autor, string? Detalhe);

/// <summary>Corpo do <c>POST /api/autorizacao/concessoes/{id}/revogacao</c>.</summary>
public sealed record RevogarConcessaoRequest(string? Motivo);
