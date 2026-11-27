namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using static LeituraDoJsonDoDocumento;

/// <summary>
/// Copia as exigências de documentos do documento que passou pela forma, cada uma com a posição que tinha na lista enviada, a chave
/// que o autor escolheu e os códigos de cadastro como foram escritos. O que o cadastro do tipo de documento dá e os rótulos são só de
/// leitura e não são lidos. O bloco ausente é nulo, e quem confere o que falta é o domínio.
/// </summary>
internal static class ExigenciasDoDocumentoLidas
{
    internal static ExigenciasNoDocumento? De(JsonNode? bloco) => bloco is JsonObject exigencias
        ? new(
            [.. Itens(exigencias, "exigencias").Select(static item => Exigencia(item.Posicao, item.Item))],
            RegrasDeComprovacaoDoDocumentoLidas.De(exigencias["regrasDeComprovacao"]))
        : null;

    private static ExigenciaNoDocumento Exigencia(int posicao, JsonObject exigencia) => ComRequisitos(new ExigenciaNoDocumento(
        posicao,
        Texto(exigencia, "chave")!,
        Texto(exigencia, "tipoDocumentoCodigo"),
        Texto(exigencia, "faseChave"),
        Texto(exigencia, "etapaChave"),
        Texto(exigencia, "formularioCodigo"))
    {
        Aplicabilidade = CodigoDoCatalogo<Aplicabilidade>(Texto(exigencia, "aplicabilidade")),
        Obrigatorio = Valor<bool>(exigencia, "obrigatorio"),
        AbrePrazoParaReenvio = Valor<bool>(exigencia, "abrePrazoParaReenvio") ?? false,
        ConsequenciaDoDescumprimento = CodigoDoCatalogo<ConsequenciaDoDescumprimento>(Texto(exigencia, "consequenciaDoDescumprimento")),
        Condicoes = Condicoes(exigencia["condicoes"]),
        BasesLegais = BasesLegais(exigencia),
        DadosComprovados = [.. Textos(exigencia, "dadosComprovados")],
    }, exigencia);

    private static ExigenciaNoDocumento ComRequisitos(ExigenciaNoDocumento exigencia, JsonObject bloco) => exigencia with
    {
        FormatosPermitidos = RequisitosDaExigenciaLidos.Formatos(Objeto(bloco, "formatosPermitidos")),
        TamanhoMaximoBytes = Valor<int>(bloco, "tamanhoMaximoBytes"),
        ModeloId = Valor<Guid>(bloco, "modeloId"),
        PrazoDeEmissao = RequisitosDaExigenciaLidos.PrazoDeEmissao(Objeto(bloco, "prazoDeEmissao")),
        Validade = RequisitosDaExigenciaLidos.Validade(Objeto(bloco, "validade")),
    };

    internal static BaseLegalNoDocumento[] BasesLegais(JsonObject pai) =>
        [.. Itens(pai, "basesLegais").Select(static item => BaseLegal(item.Posicao, item.Item))];

    private static BaseLegalNoDocumento BaseLegal(int posicao, JsonObject baseLegal) => new(
        posicao,
        Texto(baseLegal, "referencia"),
        CodigoDoCatalogo<TipoAbrangencia>(Texto(baseLegal, "abrangencia")),
        CodigoDoCatalogo<StatusBaseLegal>(Texto(baseLegal, "situacao")),
        Texto(baseLegal, "observacao"));

    // Cada alternativa é uma lista de condições; o item que não é lista ou condição fica de fora, e o esquema já o recusou.
    private static CondicaoSobreDadoNoDocumento[][] Condicoes(JsonNode? alternativas) => alternativas is JsonArray lista
        ? [.. lista.OfType<JsonArray>().Select(static alternativa => alternativa.OfType<JsonObject>().Select(Condicao).ToArray())]
        : [];

    // O valor ausente fica nulo; o "valor": null escrito no documento é um valor presente, que o esquema aceita nos operadores escalares.
    private static CondicaoSobreDadoNoDocumento Condicao(JsonObject condicao) => new(
        Texto(condicao, "codigoDoDado"),
        Texto(condicao, "operador"),
        condicao.TryGetPropertyValue("valor", out JsonNode? valor) ? JsonSerializer.SerializeToElement(valor) : null);
}
