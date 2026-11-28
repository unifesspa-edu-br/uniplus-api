namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

internal static class ApoioDosTestesDeFaseCanonica
{
    internal static Result<FaseCanonica> Criar(
        string codigo = "INSCRICAO",
        string? nome = "Inscrição",
        string? descricao = null,
        string? baseLegal = null,
        string? origemData = "NO_EDITAL",
        SinalizadoresInformados? sinalizadores = null) =>
        FaseCanonica.Criar(
            codigo, nome, descricao, baseLegal, origemData,
            // A marca acompanha o código por definição: os testes só a informam quando querem
            // exercitar a divergência.
            sinalizadores ?? Sinalizadores(
                coletaSolicitacaoIsencao: codigo == FaseCanonicaCatalogo.CodigoSolicitacaoIsencao));

    /// <summary>Os cinco sinalizadores informados: falsos, salvo o que o teste liga ou deixa ausente (<see langword="null"/>).</summary>
    internal static SinalizadoresInformados Sinalizadores(
        bool? agrupaEtapas = false,
        bool? permiteComplementacao = false,
        bool? coletaInscricao = false,
        bool? coletaSolicitacaoIsencao = false,
        bool? organizadaEmChamadas = false) =>
        new(agrupaEtapas, permiteComplementacao, coletaInscricao, coletaSolicitacaoIsencao, organizadaEmChamadas);
}
