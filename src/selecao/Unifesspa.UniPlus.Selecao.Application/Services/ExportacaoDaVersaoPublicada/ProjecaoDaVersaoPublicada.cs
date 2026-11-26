namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

public sealed class ProjecaoDaVersaoPublicada(IEsquemaDoDocumentoDaConfiguracao esquema) : IProjecaoDaVersaoPublicada
{
    private static readonly (string Nome, Func<JsonObject, FormaDoNo, ChavesDoDocumentoCongelado, JsonNode?> Projetar)[] Partes =
    [
        ("cronograma", ProjecaoDeCronograma.Projetar),
        ("etapas", ProjecaoDeEtapas.Projetar),
        ("documentosExigidos", ProjecaoDeDocumentosExigidos.Projetar),
        ("formularios", ProjecaoDeFormularios.Projetar),
        ("motivosDeDecisao", ProjecaoDeMotivosDeDecisao.Projetar),
        ("identificacao", ProjecaoDeIdentificacao.Projetar),
        ("coleta", ProjecaoDeColeta.Projetar),
        ("opcoesDeCurso", ProjecaoDeOpcoesDeCurso.Projetar),
        ("distribuicaoVagas", ProjecaoDeDistribuicaoVagas.Projetar),
        ("ordemDeRemanejamento", ProjecaoDeCascataRemanejamento.Projetar),
        ("taxaDeInscricao", ProjecaoDeTaxaDeInscricao.Projetar),
        ("divulgacao", ProjecaoDeDivulgacao.Projetar),
        ("tiposDeAto", ProjecaoDeTiposDeAto.Projetar),
        ("classificacao", ProjecaoDeClassificacao.Projetar),
        ("atendimento", ProjecaoDeAtendimento.Projetar),
        ("fada", ProjecaoDeFada.Projetar),
        ("provaPresencial", ProjecaoDeProvaPresencial.Projetar),
        ("cadastroDeReserva", ProjecaoDeCadastroDeReserva.Projetar),
    ];

    public JsonObject Projetar(VersaoPublicada versao)
    {
        ArgumentNullException.ThrowIfNull(versao);
        JsonObject congelado = JsonNode.Parse(versao.Conteudo) as JsonObject
            ?? throw new InvalidOperationException("O documento congelado não é um objeto JSON.");
        ChavesDoDocumentoCongelado chaves = new(congelado);
        JsonObject documento = CriarRaiz(versao);
        foreach ((string nome, Func<JsonObject, FormaDoNo, ChavesDoDocumentoCongelado, JsonNode?> projetar) in Partes)
        {
            if (projetar(congelado, esquema.Forma.Propriedades[nome], chaves) is { } parte)
            {
                documento[nome] = parte;
            }
        }
        return documento;
    }

    private static JsonObject CriarRaiz(VersaoPublicada versao) => new()
    {
        ["formato"] = "uniplus.selecao.configuracao",
        ["versaoFormato"] = 1,
        ["id"] = versao.ProcessoSeletivoId.ToString(),
        ["versao"] = versao.Numero,
        ["publicadoEm"] = versao.PublicadaEm.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", System.Globalization.CultureInfo.InvariantCulture),
    };
}
