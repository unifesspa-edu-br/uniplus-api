namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using System.Reflection;
using System.Text;

/// <summary>
/// Os scripts SQL que um assembly embute em <c>Persistence/Scripts</c>: os de <c>Pre</c>, que rodam antes das migrations
/// do EF, e os demais, que rodam depois delas. Cada grupo vem na ordem ordinal do nome do recurso. Um assembly sem
/// script devolve os dois grupos vazios.
/// </summary>
internal sealed record ScriptsSqlDoModulo(IReadOnlyList<ScriptSql> Pre, IReadOnlyList<ScriptSql> Pos)
{
    private const string PastaDosScripts = ".Persistence.Scripts.";
    private const string PastaDosPrerrequisitos = ".Persistence.Scripts.Pre.";
    private const string Extensao = ".sql";

    public static async Task<ScriptsSqlDoModulo> DescobrirAsync(Assembly assembly, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        string[] recursos = [.. assembly.GetManifestResourceNames()
            .Where(n => n.EndsWith(Extensao, StringComparison.Ordinal)
                && n.Contains(PastaDosScripts, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];
        ScriptSql[] scripts = await LerAsync(assembly, recursos, cancellationToken).ConfigureAwait(false);

        return new ScriptsSqlDoModulo(
            [.. scripts.Where(s => EhPrerrequisito(s.Nome))],
            [.. scripts.Where(s => !EhPrerrequisito(s.Nome))]);
    }

    private static bool EhPrerrequisito(string recurso) =>
        recurso.Contains(PastaDosPrerrequisitos, StringComparison.Ordinal);

    private static async Task<ScriptSql[]> LerAsync(
        Assembly assembly,
        string[] recursos,
        CancellationToken cancellationToken)
    {
        List<ScriptSql> scripts = [];
        foreach (string recurso in recursos)
        {
            Stream fluxo = assembly.GetManifestResourceStream(recurso)!;
            await using (fluxo.ConfigureAwait(false))
            {
                using StreamReader leitor = new(fluxo, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                string texto = await leitor.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                scripts.Add(new ScriptSql(recurso, texto));
            }
        }

        return [.. scripts];
    }
}
