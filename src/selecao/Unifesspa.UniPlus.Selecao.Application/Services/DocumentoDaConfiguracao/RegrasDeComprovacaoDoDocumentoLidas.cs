namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using static LeituraDoJsonDoDocumento;

/// <summary>
/// Copia as regras de comprovação do documento, cada uma com a posição que tinha na lista enviada e, nos itens de um grupo, a
/// posição dentro do grupo pai. A regra de documento cita a exigência pela chave; a de grupo traz os itens, que têm o mesmo formato.
/// </summary>
internal static class RegrasDeComprovacaoDoDocumentoLidas
{
    internal static RegraDeComprovacaoNoDocumento[] De(JsonNode? regras) =>
        [.. Itens(regras).Select(static item => Regra(item.Posicao, item.Item))];

    private static RegraDeComprovacaoNoDocumento Regra(int posicao, JsonObject regra) => new(
        posicao,
        CodigoDoCatalogo<FormaDeComprovacao>(Texto(regra, "forma")),
        Texto(regra, "exigenciaChave"))
    {
        QuantidadeMinima = Valor<int>(regra, "quantidadeMinima"),
        AbrePrazoParaReenvio = Valor<bool>(regra, "abrePrazoParaReenvio"),
        ConsequenciaDoDescumprimento = CodigoDoCatalogo<ConsequenciaDoDescumprimento>(Texto(regra, "consequenciaDoDescumprimento")),
        BasesLegais = ExigenciasDoDocumentoLidas.BasesLegais(regra),
        CriterioDeDistincao = CodigoDoCatalogo<CriterioDeDistincaoDasApresentacoes>(Texto(regra, "criterioDeDistincao")),
        DataReferencia = RequisitosDaExigenciaLidos.Data(regra, "dataReferencia"),
        OcorrenciasEsperadas = Textos(regra, "ocorrenciasEsperadas"),
        GrupoDoFormularioParaRepeticao = Texto(regra, "grupoDoFormularioParaRepeticao"),
        Itens = De(regra["itens"]),
    };
}
