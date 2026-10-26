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

    /// <summary>Uma persona do portal, com os dados que um arquivo do INEP traz de cada candidato.</summary>
    public sealed record PessoaFicticia(string Nome, string Cpf, string Sexo, string DataDeNascimento, string Cidade, string Uf);

    /// <summary>As personas candidatas usadas onde um teste precisa de várias identidades distintas.</summary>
    public static IReadOnlyList<PessoaFicticia> Candidatas { get; } =
    [
        new("Fernanda Laura Nicole Drumond", "69959836371", "Feminino", "02/02/1970", "Campo Grande", "MS"),
        new("Kevin Anderson Carvalho", "33257350376", "Masculino", "17/03/1984", "Rio Branco", "AC"),
        new("Helena Bianca Sueli Rezende", "16328793014", "Feminino", "12/05/1954", "Porto Alegre", "RS"),
        new("Erick Isaac Ramos", "33434225927", "Masculino", "13/02/1969", "Salvador", "BA"),
        new("Leandro Caleb Viana", "03784991300", "Masculino", "16/05/1947", "Paragominas", "PA"),
        new("Breno Lucca Samuel Novaes", "04428488700", "Masculino", "21/04/1960", "São Bernardo do Campo", "SP"),
        new("Marlene Márcia Emily Martins", "97150972438", "Feminino", "06/01/1949", "Aracruz", "ES"),
    ];
}
