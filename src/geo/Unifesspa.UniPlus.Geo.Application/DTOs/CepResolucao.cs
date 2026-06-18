namespace Unifesspa.UniPlus.Geo.Application.DTOs;

/// <summary>
/// Vocabulário canônico de <see cref="CepResolvidoDto.NivelResolucao"/> (até onde a
/// resolução chegou) e <see cref="CepResolvidoDto.Origem"/> (qual estratégia
/// resolveu). Centraliza as strings para que reader, testes e contrato OpenAPI não
/// divirjam por literais soltos.
/// </summary>
public static class CepResolucao
{
    // NivelResolucao — granularidade territorial alcançada.
    public const string NivelLogradouro = "logradouro";
    public const string NivelBairro = "bairro";
    public const string NivelDistrito = "distrito";
    public const string NivelCidade = "cidade";

    // Origem — ramo da cascata que resolveu (logradouro > grande usuário > faixa).
    public const string OrigemLogradouro = "logradouro";
    public const string OrigemGrandeUsuario = "grande-usuario";
    public const string OrigemFaixaCidade = "faixa-cidade";
    public const string OrigemFaixaBairro = "faixa-bairro";
    public const string OrigemFaixaDistrito = "faixa-distrito";
}
