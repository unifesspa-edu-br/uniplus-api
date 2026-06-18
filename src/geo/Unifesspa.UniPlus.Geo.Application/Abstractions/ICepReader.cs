namespace Unifesspa.UniPlus.Geo.Application.Abstractions;

using Unifesspa.UniPlus.Geo.Application.DTOs;

/// <summary>
/// Leitor read-side da resolução de CEP sobre o <c>GeoDbContext</c>. Aplica a
/// cascata determinística (1) <c>Logradouro</c> → (2) <c>CepGrandeUsuario</c> →
/// (3) faixa (cidade/bairro/distrito) → (4) nada (<see langword="null"/>),
/// projetando direto em <see cref="CepResolvidoDto"/> (sem <c>_links</c>). Só expõe
/// reference data vigente (ADR-0092). A abstração trafega primitivas + DTOs (nunca
/// <c>IQueryable</c>/<c>DbContext</c>), mantendo Application independente de EF Core.
/// </summary>
public interface ICepReader
{
    /// <summary>
    /// Resolve o <paramref name="cep"/> (8 dígitos, zero-padded) pela cascata;
    /// <see langword="null"/> quando nada o cobre. Quando o CEP casa vários
    /// logradouros, retorna o primário nos campos de topo + os demais em
    /// <see cref="CepResolvidoDto.Alternativos"/>, na ordem de desempate estável.
    /// </summary>
    Task<CepResolvidoDto?> ResolverAsync(string cep, CancellationToken cancellationToken);
}
