namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// As personas fictícias do portal de documentação (<c>uniplus-developers/src/data/fake-people.json</c>)
/// — único CPF/nome admitido em teste e exemplo, nunca inventado. Duas aqui bastam para os cenários
/// de "mesmo CPF" e "CPF diferente" da inscrição do candidato (issue #1858); outras entram quando
/// um teste precisar de uma terceira identidade.
/// </summary>
internal static class PersonasDeTeste
{
    /// <summary>Fernanda Laura Nicole Drumond — nome social "Ariel Drumond", RG 12.868.927-4.</summary>
    public static Cpf FernandaDrumond => Cpf.Criar("699.598.363-71").Value!;

    /// <summary>O nome completo da Fernanda, como o login o traz (claim name).</summary>
    public const string NomeFernandaDrumond = "Fernanda Laura Nicole Drumond";

    /// <summary>O nome social da Fernanda, como a conta o traz (claim nomeSocial).</summary>
    public const string NomeSocialFernandaDrumond = "Ariel Drumond";

    /// <summary>O nome completo do Kevin, como o login o traz (claim name).</summary>
    public const string NomeKevinCarvalho = "Kevin Anderson Carvalho";

    /// <summary>Kevin Anderson Carvalho — RG 25.062.156-3.</summary>
    public static Cpf KevinCarvalho => Cpf.Criar("332.573.503-76").Value!;
}
