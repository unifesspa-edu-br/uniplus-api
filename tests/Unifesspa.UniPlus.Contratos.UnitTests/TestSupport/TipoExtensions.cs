namespace Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;

internal static class TipoExtensions
{
    /// <summary>O nome completo do tipo; falha, citando o tipo, para o que não o tem (parâmetro de tipo genérico).</summary>
    internal static string NomeCompleto(this Type tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        return tipo.FullName ?? throw new InvalidOperationException($"O tipo {tipo.Name} não tem nome completo.");
    }
}
