namespace Unifesspa.UniPlus.ArchTests.TestSupport.CodigosDeErro;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Reúne os códigos de fio de todos os registros de erro numa lista por código. O mesmo código
/// declarado em mais de um registro com o mesmo status e título é um só; com status ou título
/// diferentes, o catálogo teria duas respostas para o mesmo código, e a agregação falha.
/// </summary>
internal static class AgregadorDoCatalogoDeErros
{
    private sealed record Declaracao(EntradaDoCatalogoDeErros Entrada, string Registro);

    internal static IReadOnlyList<EntradaDoCatalogoDeErros> Agregar(IEnumerable<IDomainErrorRegistration> registros)
    {
        ArgumentNullException.ThrowIfNull(registros);

        IGrouping<string, Declaracao>[] porCodigo =
            [.. registros.SelectMany(DeclaracoesDe).GroupBy(static d => d.Entrada.Codigo, StringComparer.Ordinal)];
        string[] conflitos = [.. porCodigo.Where(static g => g.Select(static d => d.Entrada).Distinct().Count() > 1).Select(DescreverConflito)];

        return conflitos.Length == 0
            ? [.. porCodigo.Select(static g => g.First().Entrada).OrderBy(static e => e.Codigo, StringComparer.Ordinal)]
            : throw new InvalidOperationException(
                "O mesmo código de erro está registrado com status ou título diferentes. Unifique a definição ou use códigos distintos:\n"
                + string.Join('\n', conflitos));
    }

    private static IEnumerable<Declaracao> DeclaracoesDe(IDomainErrorRegistration registro) =>
        registro.GetMappings().Select(par => new Declaracao(
            new EntradaDoCatalogoDeErros(par.Value.Code, par.Value.Status, par.Value.Title),
            registro.GetType().Name));

    private static string DescreverConflito(IGrouping<string, Declaracao> declaracoes) =>
        $"- '{declaracoes.Key}': "
        + string.Join("; ", declaracoes.DistinctBy(static d => d.Entrada).Select(static d => $"{d.Entrada.Descricao} em {d.Registro}"));
}
