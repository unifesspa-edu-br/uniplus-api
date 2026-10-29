namespace Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Concessão de perfil gravada e não revogada de uma pessoa, com as permissões que o perfil
/// reúne no instante da leitura. É o que a decisão converte em concessões efetivas
/// (<see cref="ValueObjects.EffectiveGrant"/>).
/// </summary>
/// <param name="ConcessaoId">Identificador da concessão.</param>
/// <param name="PerfilId">Perfil de acesso concedido.</param>
/// <param name="UnidadeId">Unidade do escopo; nula quando o escopo é a instituição.</param>
/// <param name="Institucional">O escopo é a instituição.</param>
/// <param name="ValidaAte">Fim da vigência; nulo quando a concessão não expira. A concessão vencida
/// continua listada, para a decisão recusá-la como expirada e orientar a renovação.</param>
/// <param name="Permissoes">Códigos das permissões do perfil.</param>
public sealed record ConcessaoAtiva(
    Guid ConcessaoId,
    Guid PerfilId,
    Guid? UnidadeId,
    bool Institucional,
    DateTimeOffset? ValidaAte,
    IReadOnlyList<string> Permissoes);
