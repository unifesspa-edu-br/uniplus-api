namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Uma parte identificada pelo tipo e pelo código na configuração.</summary>
public sealed record ParteDaConfiguracao(TipoDeParteDaConfiguracao Tipo, string Codigo)
{
    internal static readonly Comparer<ParteDaConfiguracao> PorTipoECodigo = Comparer<ParteDaConfiguracao>.Create(
        static (a, b) => (int)a.Tipo != (int)b.Tipo ? ((int)a.Tipo).CompareTo((int)b.Tipo) : string.CompareOrdinal(a.Codigo, b.Codigo));

    /// <summary>Identidade da parte na configuração congelada.</summary>
    public string Identidade => $"{Tipo.Codigo()}/{Codigo}";

    /// <summary>Identificação legível em mensagens de recusa.</summary>
    public string Rotulo => $"{Tipo.Rotulo()} {Codigo}";
}
