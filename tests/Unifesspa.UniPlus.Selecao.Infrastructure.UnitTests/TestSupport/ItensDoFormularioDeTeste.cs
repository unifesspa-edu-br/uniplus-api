namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Campos e grupos de teste já ligados ao formulário que os exige, como o documento congelado os lê.</summary>
internal static class ItensDoFormularioDeTeste
{
    internal static FatoColetado Campo(string codigoDoDado, int ordem, FormularioProcesso formulario, string? secao = null) =>
        FatoColetado.Criar(
            codigoDoDado,
            ordem,
            secao,
            new ApresentacaoDoCampo(codigoDoDado, TipoDeCampo.SimOuNao, null, false),
            new RegrasDoCampo(null, Obrigatoriedade.Nunca, [], null),
            ProtecaoDoFatoDeTeste.Copia(),
            formulario).Value!;

    internal static GrupoColetado Grupo(string codigo, int ordem, FormularioProcesso formulario, string? secao = null) =>
        GrupoColetado.Criar(
            codigo,
            ordem,
            secao,
            "Grupo fictício",
            new OcorrenciasDoGrupo(1, 2, false),
            new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Nunca),
            [Campo($"{codigo}_DADO", 0, formulario)]).Value!;
}
