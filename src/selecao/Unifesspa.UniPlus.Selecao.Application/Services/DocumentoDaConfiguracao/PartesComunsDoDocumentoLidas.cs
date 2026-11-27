namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static LeituraDoJsonDoDocumento;

/// <summary>
/// As partes do documento que a fase e a etapa escrevem do mesmo jeito: a banca, a regra de deliberação, os parâmetros do
/// recurso, o procedimento de recurso e o alvo dele. Copia como foi escrito; quem confere é o domínio.
/// </summary>
internal static class PartesComunsDoDocumentoLidas
{
    internal static BancaNoDocumento Banca(int posicao, JsonObject banca) => new(
        posicao,
        Texto(banca, "chave")!,
        Texto(banca, "tipoBancaCodigo")!,
        Textos(banca, "categoriasDocumentoCodigos"),
        Deliberacao(Objeto(banca, "regraDeDeliberacao")),
        Texto(banca, "consequenciaDaAusencia"));

    internal static DeclaracaoDaDeliberacao? Deliberacao(JsonObject? bloco) => bloco is null
        ? null
        : new(
            Texto(bloco, "modo"),
            Valor<int>(bloco, "integrantes") ?? 0,
            Valor<int>(bloco, "suplentes") ?? 0,
            new(Valor<int>(bloco, "quorum"),
            Texto(bloco, "criterio"),
            Texto(bloco, "desempate"),
            Texto(bloco, "encerramento"),
            Texto(bloco, "semUnanimidade")));

    // Os códigos seguem como texto até o domínio, que recusa o desconhecido junto das demais causas; o esquema já fecha as opções.
    internal static ParametrosDeclaradosDoRecurso ParametrosDoRecurso(JsonObject regra) => new(
        new PrazoDeclarado(Valor<decimal>(regra, "prazoValor"), Texto(regra, "prazoUnidade")),
        Valor<bool>(regra, "admiteInstanciaSuperior") ?? false,
        new PrazoDeclarado(Valor<decimal>(regra, "prazoSegundaInstanciaValor"), Texto(regra, "prazoSegundaInstanciaUnidade")),
        Texto(regra, "efeitoSuspensivo"),
        Texto(regra, "guardaDaVaga"));

    internal static ProcedimentoNoDocumento Procedimento(int posicao, JsonObject procedimento) => new(
        posicao,
        CodigoDoCatalogo<TipoMotivoDeDecisao>(Texto(procedimento, "tipoDeDecisao")),
        Alvo(Objeto(procedimento, "alvo")),
        ReferenciaOuVazia(procedimento, "regra"));

    internal static AlvoNoDocumento? Alvo(JsonObject? alvo) =>
        alvo is null ? null : new(Texto(alvo, "itemDeAvaliacaoChave"), Texto(alvo, "areaDoEnem"));

    internal static ProdutoNoDocumento Produto(int posicao, JsonObject produto) => new(
        posicao,
        Texto(produto, "atoCodigo")!,
        CodigoDoCatalogo<PapelDaPublicacaoPrevista>(Texto(produto, "papel")));

    internal static string?[]? TiposDeDecisao(JsonObject pai) =>
        pai["tiposDeDecisao"] is JsonArray lista ? [.. lista.Select(static tipo => tipo?.GetValue<string>())] : null;

    internal static RegraDeRecursoNoDocumento RegraDeRecurso(JsonObject regra) => new(
        new(Texto(regra, "regraCodigo"), Texto(regra, "regraVersao")),
        Texto(regra, "publicacaoDeReferenciaAtoCodigo"),
        ParametrosDoRecurso(regra),
        [.. Itens(regra, "procedimentosDeRecurso").Select(static item => Procedimento(item.Posicao, item.Item))]);
}
