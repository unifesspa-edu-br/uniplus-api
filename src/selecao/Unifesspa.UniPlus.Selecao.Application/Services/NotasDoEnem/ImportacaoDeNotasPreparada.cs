namespace Unifesspa.UniPlus.Selecao.Application.Services.NotasDoEnem;

using System.Collections.Immutable;

using Domain.Leiautes;
using Domain.ValueObjects;

/// <summary>O retorno conferido e os índices calculados antes do bloqueio do processo, sem guardar o arquivo.</summary>
public sealed class ImportacaoDeNotasPreparada
{
    public ImportacaoDeNotasPreparada(LeiauteDeRetornoDoInep leiaute, string arquivoHash,
        IReadOnlyList<LinhaDeRetorno> linhas, IReadOnlyDictionary<string, IndiceCegoDoCpf> indices)
    {
        ArgumentNullException.ThrowIfNull(leiaute);
        ArgumentNullException.ThrowIfNull(linhas);
        ArgumentNullException.ThrowIfNull(indices);
        LeiauteCodigo = leiaute.Codigo;
        LeiauteVersao = leiaute.Versao;
        LeiauteHash = leiaute.Hash;
        ArquivoHash = arquivoHash;
        Linhas = linhas.Select(static linha => linha with { Areas = linha.Areas.ToImmutableArray() }).ToImmutableArray();
        IndicePorCpf = indices.ToImmutableDictionary(StringComparer.Ordinal);
    }

    public string LeiauteCodigo { get; }
    public string LeiauteVersao { get; }
    public string LeiauteHash { get; }
    public string ArquivoHash { get; }
    public ImmutableArray<LinhaDeRetorno> Linhas { get; }
    public ImmutableDictionary<string, IndiceCegoDoCpf> IndicePorCpf { get; }
}
