namespace Unifesspa.UniPlus.Kernel.Domain.Enderecos;

using System.Collections.Frozen;

/// <summary>
/// Vocabulário canônico do nível de resolução de um endereço estruturado
/// (ADR-0096): até onde a resolução do CEP chegou na cascata do DNE
/// (logradouro → bairro → distrito → cidade). Espelha os valores de
/// <c>CepResolvidoDto.NivelResolucao</c> do módulo Geo — duplicado de propósito
/// aqui no Kernel para não acoplar consumidores (Campus, LocalOferta,
/// Instituicao) ao bounded context Geo (ADR-0056). A coerência de valores é
/// garantida por teste, não por dependência de assembly.
/// </summary>
public static class NivelResolucaoEndereco
{
    /// <summary>Resolução completa até o logradouro.</summary>
    public const string Logradouro = "logradouro";

    /// <summary>Resolução até o bairro (CEP de faixa de bairro).</summary>
    public const string Bairro = "bairro";

    /// <summary>Resolução até o distrito (CEP de faixa de distrito).</summary>
    public const string Distrito = "distrito";

    /// <summary>Resolução só até a cidade (CEP de faixa geral do município).</summary>
    public const string Cidade = "cidade";

    /// <summary>Comprimento máximo do rótulo de nível persistido.</summary>
    public const int MaxLength = 20;

    private static readonly FrozenSet<string> Validos =
        new[] { Logradouro, Bairro, Distrito, Cidade }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Indica se <paramref name="valor"/> é um nível de resolução conhecido.</summary>
    public static bool EhValido(string? valor) => valor is not null && Validos.Contains(valor);
}
