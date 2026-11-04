namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os fatos coletados, os grupos, a derivação e os blocos congelados de apresentação, atos e fatos derivados.
/// </summary>
internal sealed record BlocosDeColeta
{
    public required IReadOnlyList<FatoColetado> FatosColetados { get; init; }

    public required Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> ValoresSelecionaveis { get; init; }

    public required IReadOnlyList<GrupoColetado> GruposColetados { get; init; }

    public required List<DefinicaoAgregado> AgregadosDosGrupos { get; init; }

    public required IReadOnlyList<ConfiguracaoDerivacaoFato> RegrasDerivacao { get; init; }

    public required string VersaoInterpretador { get; init; }

    public required Dictionary<string, ApresentacaoDoFato> ApresentacaoDosPressupostos { get; init; }

    public required Dictionary<string, TipoDeAtoCongelado> TiposDeAtoCongelados { get; init; }

    public required Dictionary<string, FatoDerivadoCongelado> FatosDerivadosCongelados { get; init; }
}
