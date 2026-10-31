namespace Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Concessão de perfil gravada e não revogada de uma pessoa, com as permissões que o perfil
/// reúne no instante da leitura. É o que a decisão converte em concessões efetivas
/// (<see cref="ValueObjects.EffectiveGrant"/>).
/// </summary>
/// <param name="ConcessaoId">Identificador da concessão.</param>
/// <param name="PerfilId">Perfil de acesso concedido.</param>
/// <param name="PerfilNome">Nome do perfil no instante da leitura.</param>
/// <param name="UnidadeId">Unidade do escopo; nula quando o escopo é a instituição.</param>
/// <param name="Institucional">O escopo é a instituição.</param>
/// <param name="ValidaAte">Fim da vigência; nulo quando a concessão não expira. A concessão vencida
/// continua listada, para a decisão recusá-la como expirada e orientar a renovação.</param>
/// <param name="Permissoes">Códigos das permissões do perfil.</param>
public sealed record ConcessaoAtiva(
    Guid ConcessaoId,
    Guid PerfilId,
    string PerfilNome,
    Guid? UnidadeId,
    bool Institucional,
    DateTimeOffset? ValidaAte,
    IReadOnlyList<string> Permissoes)
{
    /// <summary>A concessão está vigente em <paramref name="agora"/>: sem prazo, ou com prazo ainda por vencer.</summary>
    public bool VigenteEm(DateTimeOffset agora) => VigenteEm(ValidaAte, agora);

    /// <summary>
    /// Regra única de vigência: a concessão com <paramref name="validaAte"/> igual ou anterior a
    /// <paramref name="agora"/> está vencida. Serve também a quem só enxerga o prazo, como a
    /// concessão efetiva da decisão.
    /// </summary>
    public static bool VigenteEm(DateTimeOffset? validaAte, DateTimeOffset agora) =>
        validaAte is not { } validade || validade > agora;
}
