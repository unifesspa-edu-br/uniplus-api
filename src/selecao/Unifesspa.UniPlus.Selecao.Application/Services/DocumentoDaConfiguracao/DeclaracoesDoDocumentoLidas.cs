namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;


using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Copia o documento que passou pela forma para as declarações do domínio: a identificação, as fases, o cadastro de reserva, a
/// manifestação de interesse e a prova presencial, cada parte de lista com a posição que tinha na lista enviada e cada código de catálogo como foi escrito. O esquema garante o tipo de cada valor presente,
/// então aqui só se converte; o que falta fica ausente ou no valor de partida do tipo, e quem o confere é o domínio. Não decide regra.
/// </summary>
internal static class DeclaracoesDoDocumentoLidas
{
    internal static DeclaracoesDoDocumento De(JsonObject documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        return new(
            Identificacao(Objeto(documento, "identificacao")),
            [.. Itens(Objeto(documento, "cronograma"), "fases").Select(static item => Fase(item.Posicao, item.Item))])
        {
            CadastroDeReserva = [.. Itens(documento, "cadastroDeReserva").Select(static item => CadastroDeReserva(item.Item))],
            ManifestacaoDeInteresse = Texto(documento, "manifestacaoDeInteresse"),
            ProvaPresencial = ProvaPresencial(Objeto(documento, "provaPresencial")),
        };
    }

    // O código da participação segue como texto até o domínio: o desconhecido não vira ausente, porque ausente é a chamada sem opção.
    private static CadastroDeReservaInformado CadastroDeReserva(JsonObject chamada) => new(
        Valor<int>(chamada, "numeroDaChamada"),
        Valor<int>(chamada, "pessoasDaReservaPorVaga"),
        Texto(chamada, "participacaoDaReserva"));

    // O nome do município é só de leitura e fica de fora; o código e a UF seguem como foram escritos, e o domínio os confere.
    private static ProvaPresencialNoDocumento? ProvaPresencial(JsonObject? bloco) => bloco is null
        ? null
        : new(
            Valor<bool>(bloco, "temProvaPresencial") ?? false,
            [.. Itens(bloco, "municipios").Select(static item => new MunicipioDeProvaInformado(Texto(item.Item, "codigoIbge"), Texto(item.Item, "uf")))]);

    private static IdentificacaoNoDocumento Identificacao(JsonObject? bloco) => new(
        Texto(bloco, "nome"),
        Texto(bloco, "tipoProcessoCodigo"),
        Texto(bloco, "unidadeAdministradoraCodigo"),
        CodigoDoCatalogo<OrigemCandidatos>(Texto(bloco, "origemCandidatos")),
        Texto(bloco, "localidadeCodigoIbge"),
        Texto(bloco, "identificadorLegivel"),
        Referencia(Objeto(bloco, "algoritmoContagemPrazo")));

    private static FaseNoDocumento Fase(int posicao, JsonObject fase) => new(
        posicao,
        Texto(fase, "chave")!,
        new(Texto(fase, "tipoFaseCodigo")!, Texto(fase, "unidadeResponsavelCodigo")),
        new(Valor<DateTimeOffset>(fase, "inicio"), Valor<DateTimeOffset>(fase, "fim")),
        Publicacoes(fase),
        [.. Itens(fase, "bancasRequeridas").Select(static item => Banca(item.Posicao, item.Item))],
        RegraDeRecurso(Objeto(fase, "regraRecurso")))
    {
        NoMesmoMomentoDaAnterior = Valor<bool>(fase, "noMesmoMomentoDaAnterior") ?? false,
    };

    private static PublicacoesNoDocumento Publicacoes(JsonObject fase) => new(
        [.. Itens(fase, "publicacoesPrevistas").Select(static item => Produto(item.Posicao, item.Item))],
        Valor<bool>(fase, "emiteParecerIndividual") ?? false,
        TiposDeDecisao(fase),
        RegraDeHomologacao(Objeto(fase, "regraDeHomologacao")));

    private static ProdutoNoDocumento Produto(int posicao, JsonObject produto) =>
        new(posicao, Texto(produto, "atoCodigo")!, PapelDoProduto(Texto(produto, "papel")));

    private static PapelDaPublicacaoPrevista? PapelDoProduto(string? codigo) => CodigoDoCatalogo<PapelDaPublicacaoPrevista>(codigo);

    private static string?[]? TiposDeDecisao(JsonObject fase) =>
        fase["tiposDeDecisao"] is JsonArray lista ? [.. lista.Select(static tipo => tipo?.GetValue<string>())] : null;

    private static BancaNoDocumento Banca(int posicao, JsonObject banca) => new(
        posicao,
        Texto(banca, "chave")!,
        Texto(banca, "tipoBancaCodigo")!,
        Textos(banca, "categoriasDocumentoCodigos"),
        Deliberacao(Objeto(banca, "regraDeDeliberacao")),
        Texto(banca, "consequenciaDaAusencia"));

    private static DeclaracaoDaDeliberacao? Deliberacao(JsonObject? bloco) => bloco is null
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

    private static RegraDeRecursoNoDocumento? RegraDeRecurso(JsonObject? regra) => regra is null
        ? null
        : new(
            new(Texto(regra, "regraCodigo"), Texto(regra, "regraVersao")),
            Texto(regra, "publicacaoDeReferenciaAtoCodigo"),
            ParametrosDoRecurso(regra),
            [.. Itens(regra, "procedimentosDeRecurso").Select(static item => Procedimento(item.Posicao, item.Item))]);

    // Os códigos seguem como texto até o domínio, que recusa o desconhecido junto das demais causas; o esquema já fecha as opções.
    private static ParametrosDeclaradosDoRecurso ParametrosDoRecurso(JsonObject regra) => new(
        new PrazoDeclarado(Valor<decimal>(regra, "prazoValor"), Texto(regra, "prazoUnidade")),
        Valor<bool>(regra, "admiteInstanciaSuperior") ?? false,
        new PrazoDeclarado(Valor<decimal>(regra, "prazoSegundaInstanciaValor"), Texto(regra, "prazoSegundaInstanciaUnidade")),
        Texto(regra, "efeitoSuspensivo"),
        Texto(regra, "guardaDaVaga"));

    private static ProcedimentoNoDocumento Procedimento(int posicao, JsonObject procedimento) => new(
        posicao,
        TipoDeDecisao(Texto(procedimento, "tipoDeDecisao")),
        Alvo(Objeto(procedimento, "alvo")),
        Referencia(Objeto(procedimento, "regra")) ?? new ReferenciaDeRegraNoDocumento(null, null));

    private static TipoMotivoDeDecisao? TipoDeDecisao(string? codigo) => CodigoDoCatalogo<TipoMotivoDeDecisao>(codigo);

    private static AlvoNoDocumento? Alvo(JsonObject? alvo) =>
        alvo is null ? null : new(Texto(alvo, "itemDeAvaliacaoChave"), Texto(alvo, "areaDoEnem"));

    private static RegraDeHomologacaoNoDocumento? RegraDeHomologacao(JsonObject? regra) => regra is null
        ? null
        : new(
            Texto(regra, "resultadoAtoCodigo"),
            [.. Itens(regra, "alternativas").Select(static item => Alternativa(item.Item))]);

    private static AlternativaNoDocumento Alternativa(JsonObject alternativa) =>
        new([.. Itens(alternativa, "condicoes").Select(static item => Condicao(item.Item))]);

    private static CondicaoNoDocumento Condicao(JsonObject condicao) => new(
        TipoDaCondicao(Texto(condicao, "tipo")),
        Valor<int>(condicao, "edicaoDoExame"),
        Texto(condicao, "motivoDesfavoravelCodigo"),
        condicao["enquadramento"] is { } enquadramento ? JsonSerializer.SerializeToElement(enquadramento) : null);

    // O esquema fecha o tipo em uma enumeração; o código que o domínio não conhece fica ausente, e o domínio o recusa.
    private static TipoDeCondicaoDeHomologacao? TipoDaCondicao(string? codigo) =>
        CatalogoDe<TipoDeCondicaoDeHomologacao>.TentarObter(codigo, out TipoDeCondicaoDeHomologacao tipo) ? tipo : null;

    private static ReferenciaDeRegraNoDocumento? Referencia(JsonObject? referencia) =>
        referencia is null ? null : new(Texto(referencia, "codigo"), Texto(referencia, "versao"));

    private static JsonObject? Objeto(JsonObject? pai, string propriedade) => pai?[propriedade] as JsonObject;

    private static string? Texto(JsonObject? pai, string propriedade) =>
        pai?[propriedade] is JsonValue valor ? valor.GetValue<string>() : null;

    private static string[] Textos(JsonObject pai, string propriedade) =>
        pai[propriedade] is JsonArray lista ? [.. lista.Select(static texto => texto!.GetValue<string>())] : [];

    // O valor que o tipo não comporta (número fora do inteiro de 32 bits, data mal escrita) fica ausente, e o domínio o recusa.
    private static T? Valor<T>(JsonObject? pai, string propriedade)
        where T : struct =>
        pai?[propriedade] is JsonValue valor && valor.TryGetValue(out T convertido) ? convertido : null;

    // O esquema só deixa passar um dos códigos do catálogo; o que o domínio não conhece fica ausente, e o domínio o recusa.
    private static T? CodigoDoCatalogo<T>(string? codigo)
        where T : struct, Enum =>
        CatalogoDe<T>.TentarObter(codigo, out T valor) ? valor : null;

    private static IEnumerable<(int Posicao, JsonObject Item)> Itens(JsonObject? pai, string propriedade) =>
        pai?[propriedade] is JsonArray lista ? lista.Select(static (item, posicao) => (posicao, item!.AsObject())) : [];
}
