namespace Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;

using System.Text.Json;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Handler convention-based de <see cref="ObterConformidadeHistoricaQuery"/>.
/// Lê <c>RegrasJson</c> do <c>EditalGovernanceSnapshot</c> e deserializa
/// para <see cref="ConformidadeDto"/>. Retorna <see langword="null"/> quando
/// o snapshot inexiste — controller traduz para 404 com ProblemDetails
/// <c>uniplus.selecao.conformidade.snapshot_nao_disponivel</c>.
/// </summary>
/// <remarks>
/// O JSON do snapshot é byte-equivalente ao formato canônico emitido pelo
/// <c>HashCanonicalComputer</c> (ADR-0058 + #460): array de regra-objects
/// com <c>id</c>, <c>hash</c>, <c>baseLegal</c>, <c>portariaInternaCodigo</c>,
/// <c>vigenciaInicio/Fim</c>, <c>predicado</c> etc. Todas as regras no
/// snapshot foram <c>Aprovada = true</c> pelo evaluator no momento da
/// publicação — caso contrário <c>Edital.Publicar()</c> teria falhado e o
/// snapshot nunca seria gravado (#462).
/// </remarks>
public static class ObterConformidadeHistoricaQueryHandler
{
    public static async Task<ConformidadeDto?> Handle(
        ObterConformidadeHistoricaQuery query,
        IObrigatoriedadeLegalRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        string? json = await repository
            .ObterSnapshotConformidadeJsonAsync(query.EditalId, cancellationToken)
            .ConfigureAwait(false);
        if (json is null)
        {
            return null;
        }

        SnapshotRegraItem[] itens = JsonSerializer.Deserialize<SnapshotRegraItem[]>(
            json,
            HashCanonicalComputer.CanonicalOptions) ?? [];

        RegraAvaliadaDto[] regras = [.. itens.Select(item => new RegraAvaliadaDto(
            RegraId: item.Id,
            RegraCodigo: item.RegraCodigo,
            Aprovada: true,
            BaseLegal: item.BaseLegal,
            PortariaInternaCodigo: item.PortariaInternaCodigo,
            AtoNormativoUrl: item.AtoNormativoUrl,
            DescricaoHumana: item.DescricaoHumana,
            Hash: item.Hash,
            VigenciaInicio: item.VigenciaInicio,
            VigenciaFim: item.VigenciaFim))];

        return new ConformidadeDto(query.EditalId, regras);
    }

    /// <summary>
    /// Shape de cada item do array <c>RegrasJson</c> persistido pelo
    /// <c>Edital.Publicar()</c> em #462. Sub-conjunto do payload canônico
    /// suficiente para reconstruir <see cref="RegraAvaliadaDto"/>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instanciado por reflection pelo JsonSerializer.Deserialize.")]
    private sealed record SnapshotRegraItem(
        Guid Id,
        string RegraCodigo,
        string BaseLegal,
        string DescricaoHumana,
        string Hash,
        DateOnly VigenciaInicio,
        DateOnly? VigenciaFim,
        string? PortariaInternaCodigo,
        string? AtoNormativoUrl);
}
