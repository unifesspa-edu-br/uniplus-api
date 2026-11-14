namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.AcessoAosDadosDoProcesso;

using System.Globalization;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// Os corpos das requisições administrativas que montam o processo do cenário: os itens do formulário de inscrição,
/// cada exigência de documento e o ato que publica o processo.
/// </summary>
internal static class CorposDasRequisicoesDoProcesso
{
    private static readonly int PrimeiraOrdem = ConjuntoBasicoDaInscricao.Itens.Count;

    public static object ItensDoFormulario(string fatoDoGrupo) => new
    {
        itens = new object[] { ItemDoFatoSensivel() },
        grupos = new object[] { GrupoDeResponsaveis(fatoDoGrupo) },
    };

    public static object Folha(Guid faseId, Guid tipoDocumentoId, string[] comprova) => new
    {
        tipo = "FOLHA",
        documento = new
        {
            exigidoNaFaseId = faseId,
            finalidade = "INSCRICAO",
            tipoDocumentoId,
            aplicabilidade = "GERAL",
            obrigatorio = false,
            condicoes = Array.Empty<object>(),
            basesLegais = Array.Empty<object>(),
            formatosPermitidos = "QUALQUER",
            fatosComprovados = comprova,
        },
    };

    public static object CorpoDoAto(Guid documentoId) => new
    {
        numero = "001/2026",
        documentoEditalId = documentoId,
        ato = new
        {
            orgao = "CEPS",
            serie = "EDITAL",
            ano = 2026,
            dataDoAto = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            assinante = "Diretor do CEPS",
            tipoAtoCodigo = "EDITAL_ABERTURA",
        },
    };

    private static object ItemDoFatoSensivel() => new
    {
        fatoCodigo = FatosDoCenario.Sensivel,
        ordem = PrimeiraOrdem,
        rotulo = FatosDoCenario.Sensivel,
        tipoRenderizacao = "BOOLEANO",
        obrigatoriedade = "NUNCA",
        precondicao = (object?)null,
        etapaCodigo = FormularioDeInscricaoHttp.Secao,
    };

    private static object GrupoDeResponsaveis(string fatoDoGrupo) => new
    {
        codigo = FatosDoCenario.GrupoDeResponsaveis,
        ordem = PrimeiraOrdem + 1,
        rotulo = "Responsáveis legais",
        etapaCodigo = FormularioDeInscricaoHttp.Secao,
        minimo = 0,
        maximo = 3,
        exibicao = (object?)null,
        obrigatoriedade = "NUNCA",
        predicadoObrigatoriedade = (object?)null,
        subitens = new object[] { RgDoResponsavel(fatoDoGrupo) },
    };

    private static object RgDoResponsavel(string fatoDoGrupo) => new
    {
        fatoCodigo = fatoDoGrupo,
        ordem = 0,
        rotulo = "RG do responsável",
        tipoRenderizacao = "TEXTO",
        obrigatoriedade = "NUNCA",
    };
}
