namespace Unifesspa.UniPlus.Geo.Application.Abstractions;

using Unifesspa.UniPlus.Geo.Application.DTOs;

/// <summary>
/// Resolve um CEP (8 dígitos, já normalizado no boundary — ADR-0031) em um endereço
/// estruturado, aplicando cache-aside (Redis, TTL longo) sobre o
/// <see cref="ICepReader"/>. Só respostas positivas (200) entram no cache; ausência
/// (404) nunca é cacheada. Quando o Redis está indisponível, degrada para o banco
/// (a resolução não depende do cache estar de pé). Ver ADR-0090.
/// </summary>
public interface ICepResolver
{
    /// <summary>
    /// Resolve o <paramref name="cep"/> (8 dígitos); <see langword="null"/> quando
    /// nada casa (logradouro, grande usuário ou faixa) — o controller traduz para 404.
    /// O DTO retornado não carrega <c>_links</c>: o controller os anexa por requisição.
    /// </summary>
    Task<CepResolvidoDto?> ResolverAsync(string cep, CancellationToken cancellationToken);
}
