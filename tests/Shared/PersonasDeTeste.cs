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

    /// <summary>Kevin Anderson Carvalho — RG 25.062.156-3.</summary>
    public static Cpf KevinCarvalho => Cpf.Criar("332.573.503-76").Value!;
}
