namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>A classificação congelada dos tipos de ato que o cronograma do teste declara.</summary>
internal static class TiposDeAtoCongeladosDeTeste
{
    /// <summary>
    /// Uma entrada por tipo declarado, coerente com a regra do catálogo: o tipo que a fase ou a etapa
    /// declara com papel é resultado, e o declarado uma só vez e sem papel é único. Nenhum congela.
    /// </summary>
    public static IReadOnlyDictionary<string, TipoDeAtoCongelado> Dos(ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(processo);

        return processo.DeclaracoesDeAtos()
            .GroupBy(static d => d.AtoCodigo, StringComparer.Ordinal)
            .ToDictionary(
                static g => g.Key,
                static g => new TipoDeAtoCongelado(
                    g.Key,
                    UnicoPorObjeto: g.Count() == 1 && g.All(static d => d.Papel is null),
                    EhResultado: g.Any(static d => d.Papel is not null),
                    CongelaConfiguracao: false),
                StringComparer.Ordinal);
    }
}
