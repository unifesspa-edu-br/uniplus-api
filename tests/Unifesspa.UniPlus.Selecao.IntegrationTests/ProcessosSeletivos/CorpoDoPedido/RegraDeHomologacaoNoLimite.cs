namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpoDoPedido;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

/// <summary>
/// A regra de homologação com uma alternativa por tipo de decisão do catálogo, cada uma com todos os tipos de condição,
/// o motivo no tamanho do código e o enquadramento com três condições. Não há teto de alternativas no cadastro.
/// </summary>
internal static class RegraDeHomologacaoNoLimite
{
    private const int TamanhoDoCodigo = 60;

    public static RegraDeHomologacaoInput Regra(string atoDoResultado) => new(
        atoDoResultado,
        [.. CatalogoDe<TipoMotivoDeDecisao>.Todos().Select(static _ => new AlternativaDeCondicoesInput(Condicoes()))]);

    private static List<CondicaoDeHomologacaoInput> Condicoes() =>
    [
        .. CatalogoDe<TipoDeCondicaoDeHomologacao>.Todos().Select(static tipo => new CondicaoDeHomologacaoInput(
            tipo.Codigo, 2100, new string('A', TamanhoDoCodigo), Enquadramento())),
    ];

    private static List<IReadOnlyList<CondicaoPrecondicaoInput>> Enquadramento() =>
    [
        [.. Enumerable.Range(0, 3).Select(static _ => new CondicaoPrecondicaoInput(
            new string('A', TamanhoDoCodigo), Operador.Igual.Codigo(), JsonSerializer.SerializeToElement(new string('A', TamanhoDoCodigo))))],
    ];
}
