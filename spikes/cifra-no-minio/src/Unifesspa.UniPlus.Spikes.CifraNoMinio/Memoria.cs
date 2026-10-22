namespace Unifesspa.UniPlus.Spikes.CifraNoMinio;

/// <summary>
/// O pico de memória residente do processo (VmHWM) e o heap gerenciado, lidos e zerados entre um
/// cenário de medição e o próximo. Zerar escreve 5 em /proc/self/clear_refs, que reinicia o VmHWM.
/// </summary>
public static class Memoria
{
    public sealed record Leitura(long PicoRssMiB, long RssMiB, long HeapGerenciadoMiB, long TotalAlocadoMiB, int ColetasGen2);

    public static void Mapear(WebApplication app)
    {
        app.MapGet("/memoria", () => Ler());
        app.MapPost("/memoria/zerar", () =>
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            File.WriteAllText("/proc/self/clear_refs", "5");
            return Ler();
        });
    }

    private static Leitura Ler()
    {
        Dictionary<string, long> status = File.ReadAllLines("/proc/self/status")
            .Where(l => l.StartsWith("VmHWM:", StringComparison.Ordinal) || l.StartsWith("VmRSS:", StringComparison.Ordinal))
            .ToDictionary(
                l => l[..l.IndexOf(':', StringComparison.Ordinal)],
                l => long.Parse(l[(l.IndexOf(':', StringComparison.Ordinal) + 1)..].Replace("kB", string.Empty, StringComparison.Ordinal).Trim(), System.Globalization.CultureInfo.InvariantCulture));
        const long MiB = 1024 * 1024;
        return new Leitura(
            status["VmHWM"] / 1024,
            status["VmRSS"] / 1024,
            GC.GetGCMemoryInfo().HeapSizeBytes / MiB,
            GC.GetTotalAllocatedBytes() / MiB,
            GC.CollectionCount(2));
    }
}
