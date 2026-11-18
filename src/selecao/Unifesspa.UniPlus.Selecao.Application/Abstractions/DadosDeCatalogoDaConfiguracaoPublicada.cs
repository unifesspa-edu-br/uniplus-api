namespace Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Dados do catálogo utilizados na configuração de uma versão publicada.</summary>
public sealed record DadosDeCatalogoDaConfiguracaoPublicada(
    IReadOnlyDictionary<string, MetadadoFatoCongelado>? MetadadosFatosCongelados = null,
    IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>? ValoresSelecionaveisCongelados = null,
    CalendarioDiasUteisCongelado? CalendarioDiasUteis = null,
    IReadOnlyList<DefinicaoAgregado>? AgregadosDosGrupos = null,
    IReadOnlyDictionary<string, ApresentacaoDoFato>? ApresentacaoDosPressupostos = null,
    IReadOnlyDictionary<string, TipoDeAtoCongelado>? TiposDeAtoCongelados = null,
    IReadOnlyDictionary<string, FatoDerivadoCongelado>? FatosDerivadosCongelados = null);
