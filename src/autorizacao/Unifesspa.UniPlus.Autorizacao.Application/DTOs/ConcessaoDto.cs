namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

/// <summary>Concessão de acesso como a API a expõe. Traz o nome de exibição de quem age, nunca o CPF.</summary>
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

/// <summary>Pessoa pelo par emissor e <c>sub</c> do token, com o nome de exibição gravado quando ela agiu. Nunca traz CPF.</summary>
/// <param name="Nome">Nome social quando há, senão o civil; nulo no registro que não o gravou.</param>
public sealed record IdentidadeDto(string Emissor, string Sub, string? Nome = null);

public sealed record PerfilResumoDto(Guid Id, string Nome);

public sealed record EscopoDto(string Tipo, Guid? UnidadeId);

public sealed record OrigemDto(string Tipo, Guid Id);

/// <summary>Evento da trilha de uma concessão, em ordem cronológica.</summary>
/// <param name="Tipo"><c>concedida</c>, <c>revogada</c>, <c>revisao_confirmada</c> ou <c>revisao_revogada</c>; as origens acrescentam os seus.</param>
public sealed record EventoDaTrilhaDto(string Tipo, DateTimeOffset Instante, IdentidadeDto Autor, string? Detalhe);

/// <summary>Corpo do <c>POST /api/autorizacao/concessoes/{id}/revogacao</c>.</summary>
/// <param name="Motivo">Obrigatório; fica na trilha.</param>
/// <param name="ConfirmacaoEmail">Obrigatório: o e-mail do beneficiário, conferido sem diferenciar maiúsculas nem espaços nas pontas.</param>
public sealed record RevogarConcessaoRequest(string? Motivo, string? ConfirmacaoEmail);
