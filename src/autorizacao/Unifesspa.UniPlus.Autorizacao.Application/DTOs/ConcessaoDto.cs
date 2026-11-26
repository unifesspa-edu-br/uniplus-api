namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

/// <summary>Concessão de acesso como a API a expõe. Traz o nome de exibição de quem age, nunca o CPF.</summary>
/// <param name="Versao">Número da versão do perfil a que a concessão está presa, e não o da em vigor; nulo no perfil de governança.</param>
/// <param name="RevogadaPor">Quem revogou; nulo enquanto a concessão não foi revogada e na revogação pela revisão das concessões, que é do sistema.</param>
/// <param name="RevogadaPelaRevisaoId">A revisão das concessões que revogou a concessão; nula quando a revogação foi de uma pessoa.</param>
/// <param name="ValidaAte">Fim da vigência, quando a concessão é por prazo.</param>
/// <param name="Ativa">Derivada da revogação e da vigência no momento da leitura.</param>
public sealed record ConcessaoDto(
    Guid Id,
    IdentidadeDto Beneficiario,
    PerfilResumoDto Perfil,
    int? Versao,
    EscopoDto Escopo,
    OrigemDto Origem,
    DateTimeOffset ConcedidaEm,
    DateTimeOffset? ValidaAte,
    DateTimeOffset? RevogadaEm,
    IdentidadeDto? RevogadaPor,
    Guid? RevogadaPelaRevisaoId,
    string? MotivoRevogacao,
    bool Ativa);

/// <summary>Pessoa pelo par emissor e <c>contaId</c> do token, com o nome de exibição gravado quando ela agiu. Nunca traz CPF.</summary>
/// <param name="Nome">Nome social quando há, senão o civil; nulo no registro que não o gravou.</param>
public sealed record IdentidadeDto(string Emissor, string ContaId, string? Nome = null);

public sealed record PerfilResumoDto(Guid Id, string Nome);

public sealed record EscopoDto(string Tipo, Guid? UnidadeId);

public sealed record OrigemDto(string Tipo, Guid Id);

/// <summary>Corpo do <c>POST /api/autorizacao/concessoes/{id}/revogacao</c>.</summary>
/// <param name="Motivo">Obrigatório; fica na trilha.</param>
/// <param name="ConfirmacaoEmail">Obrigatório: o e-mail do beneficiário, conferido sem diferenciar maiúsculas nem espaços nas pontas.</param>
public sealed record RevogarConcessaoRequest(string? Motivo, string? ConfirmacaoEmail);
