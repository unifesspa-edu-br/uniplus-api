namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.AcessoAosDadosDoProcesso;

using System.Globalization;

using Unifesspa.UniPlus.Regras.Entradas;
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
            tipoDocumento = new { id = tipoDocumentoId },
            momento = new { faseId, finalidade = "INSCRICAO" },
            aplicabilidade = new { tipo = "GERAL", condicoes = Array.Empty<object>() },
            indeferimento = new { obrigatorio = false, basesLegais = Array.Empty<object>() },
            requisitos = new { formatosPermitidos = "QUALQUER" },
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

    private static object ItemDoFatoSensivel() =>
        FormularioDeInscricaoHttp.Item(
            FatosDoCenario.Sensivel, PrimeiraOrdem, FatosDoCenario.Sensivel, "BOOLEANO", "NUNCA", etapaCodigo: FormularioDeInscricaoHttp.Secao);

    private static object GrupoDeResponsaveis(string fatoDoGrupo) =>
        FormularioDeInscricaoHttp.Grupo(
            FatosDoCenario.GrupoDeResponsaveis, PrimeiraOrdem + 1, "Responsáveis legais", FormularioDeInscricaoHttp.Secao,
            [RgDoResponsavel(fatoDoGrupo)], new OcorrenciasDoGrupoInput(0, 3));

    private static object RgDoResponsavel(string fatoDoGrupo) =>
        FormularioDeInscricaoHttp.Item(fatoDoGrupo, 0, "RG do responsável", "TEXTO", "NUNCA");
}
