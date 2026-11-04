namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que o envelope declara sobre fatos coletados, grupos e derivação, já reidratado e pronto para
/// a conferência de coerência do bloco.
/// </summary>
/// <param name="Fatos">Os fatos coletados fora de grupo.</param>
/// <param name="Grupos">Os grupos de coleta, com seus campos.</param>
/// <param name="RegrasDerivacao">As regras de derivação configuradas por fato.</param>
/// <param name="VersaoInterpretador">A versão da semântica do motor de derivação com que o envelope foi congelado.</param>
internal sealed record BlocoDeFatosEDerivacao(
    IReadOnlyList<FatoColetado> Fatos,
    IReadOnlyList<GrupoColetado> Grupos,
    IReadOnlyList<ConfiguracaoDerivacaoFato> RegrasDerivacao,
    string VersaoInterpretador);
