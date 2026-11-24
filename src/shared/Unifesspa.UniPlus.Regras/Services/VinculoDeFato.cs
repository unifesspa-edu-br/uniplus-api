namespace Unifesspa.UniPlus.Regras.Services;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// O vínculo de um dado do catálogo diz como o valor dele é produzido (ADR-0116, ADR-0136), no formato
/// <c>MECANISMO:REFERENCIA</c>: o prefixo é o código de um <see cref="MecanismoDoVinculo"/>, e a referência, o campo, a
/// regra ou o atributo. Um lugar só para montar e ler o vínculo, usado pela Configuração, que o grava, e pela Seleção,
/// que decide por ele.
/// </summary>
public static class VinculoDeFato
{
    private const char Separador = ':';

    /// <summary>O vínculo do mecanismo para a referência dada.</summary>
    public static string De(MecanismoDoVinculo mecanismo, string referencia) => $"{mecanismo.Codigo()}{Separador}{referencia}";

    /// <summary>Se o vínculo é do mecanismo dado e nomeia o que o produz.</summary>
    public static bool Usa(string binding, MecanismoDoVinculo mecanismo)
    {
        ArgumentNullException.ThrowIfNull(binding);
        string prefixo = mecanismo.Codigo();
        return binding.Length > prefixo.Length + 1
            && binding.StartsWith(prefixo, StringComparison.Ordinal)
            && binding[prefixo.Length] == Separador;
    }

    /// <summary>O que o vínculo do mecanismo dado nomeia, ou <see langword="null"/> quando o vínculo é de outro mecanismo.</summary>
    public static string? Nomeado(string binding, MecanismoDoVinculo mecanismo) =>
        Usa(binding, mecanismo) ? binding[(mecanismo.Codigo().Length + 1)..] : null;
}
