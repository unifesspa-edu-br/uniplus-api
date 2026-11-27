namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using static LeituraDoJsonDoDocumento;

/// <summary>
/// Copia as vagas do documento que passou pela forma: as opções de curso, a distribuição de cada oferta, a cascata de remanejamento,
/// o Fator de Atenuação das Desigualdades de Acesso e o atendimento especializado, cada item de lista com a posição que tinha na
/// lista enviada e cada código como foi escrito. O que só é de leitura (o quadro calculado, os nomes) não é lido. O bloco ausente fica ausente, e quem o confere é o domínio.
/// </summary>
internal static class VagasDoDocumentoLidas
{
    internal static VagasNoDocumento De(JsonObject documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        return new(
            Valor<int>(Objeto(documento, "opcoesDeCurso"), "numero"),
            [.. Itens(documento, "distribuicaoVagas").Select(static item => Distribuicao(item.Posicao, item.Item))])
        {
            Cascata = Cascata(Objeto(documento, "ordemDeRemanejamento")),
            Fada = Fada(Objeto(documento, "fada")),
            Atendimento = Atendimento(Objeto(documento, "atendimento")),
        };
    }

    private static DistribuicaoNoDocumento Distribuicao(int posicao, JsonObject distribuicao) => new(
        posicao,
        Texto(distribuicao, "ofertaCursoCodigo"),
        Valor<int>(distribuicao, "vagasOfertadasBase"),
        [.. Itens(distribuicao, "modalidades").Select(static item => Modalidade(item.Posicao, item.Item))])
    {
        PercentualDaReservaLegal = Valor<decimal>(distribuicao, "percentualDaReservaLegal"),
        RegraDeDistribuicao = Referencia(Objeto(distribuicao, "regraDistribuicao")),
        RegraDeAjuste = Referencia(Objeto(distribuicao, "regraAjuste")),
        AjusteDoQuadro = Ajuste(Objeto(distribuicao, "ajusteDoQuadro")),
        ReferenciaDemograficaCodigo = Texto(distribuicao, "referenciaDemograficaCodigo"),
    };

    private static ModalidadeNoDocumento Modalidade(int posicao, JsonObject modalidade) =>
        new(posicao, Texto(modalidade, "codigo"), Valor<int>(modalidade, "quantidadeDeclarada"));

    private static AjusteDoQuadroNoDocumento? Ajuste(JsonObject? ajuste) => ajuste is null
        ? null
        : new(Texto(ajuste, "$tipo"), Texto(ajuste, "modalidadeCodigo"), Textos(ajuste, "modalidadeCodigos"));

    private static CascataNoDocumento? Cascata(JsonObject? cascata) => cascata is null
        ? null
        : new(
            Referencia(Objeto(cascata, "regra")),
            Texto(cascata, "destinoFinalCodigo"),
            [.. Itens(cascata, "destinos").Select(static item => Destino(item.Posicao, item.Item))]);

    private static DestinoDaCascataNoDocumento Destino(int posicao, JsonObject destino) =>
        new(posicao, Texto(destino, "modalidadeOrigemCodigo"), Texto(destino, "modalidadeDestinoCodigo"));

    private static FadaNoDocumento? Fada(JsonObject? fada) => fada is null
        ? null
        : new(
            Valor<bool>(fada, "aplica") ?? false,
            Referencia(Objeto(fada, "regra")),
            Valor<decimal>(fada, "fator"),
            Valor<decimal>(fada, "teto"),
            Texto(fada, "codigoDoDadoDeElegibilidade"),
            Texto(fada, "baseLegalCodigo"));

    private static AtendimentoNoDocumento? Atendimento(JsonObject? atendimento) => atendimento is null
        ? null
        : new(
            Codigos(atendimento, "condicoes", "condicaoCodigo"),
            Codigos(atendimento, "recursos", "recursoCodigo"),
            Codigos(atendimento, "tiposDeficiencia", "tipoDeficienciaCodigo"));

    private static CodigoNoDocumento[] Codigos(JsonObject atendimento, string lista, string campo) =>
        [.. Itens(atendimento, lista).Select(item => new CodigoNoDocumento(item.Posicao, Texto(item.Item, campo)))];
}
