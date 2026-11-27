namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

/// <summary>Reúne os códigos que o documento cita, sem repetição e na ordem em que aparecem; a citação ausente não conta.</summary>
internal static class CodigosDistintos
{
    internal static string[] De(IEnumerable<string?> codigos) =>
        [.. codigos.OfType<string>().Distinct(StringComparer.Ordinal)];

    // A regra é citada por código e versão, e a citação sem um dos dois não consulta nada.
    internal static (string Codigo, string Versao)[] DasRegras(IEnumerable<ReferenciaDeRegraNoDocumento?> referencias) =>
        [.. referencias
            .OfType<ReferenciaDeRegraNoDocumento>()
            .Where(static referencia => referencia.Codigo is not null && referencia.Versao is not null)
            .Select(static referencia => (referencia.Codigo!, referencia.Versao!))
            .Distinct()];
}
