namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;

using System.Text.Json;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

internal static class ConteudosDoModeloDeTeste
{
    public static readonly ConteudoDoModelo Vazio = new(null, [], [], [], [], []);

    public static PredicadoDnf Quando(string fato, Operador operador = Operador.Igual) =>
        Quando(fato, operador, JsonSerializer.SerializeToElement(true));

    public static PredicadoDnf Quando(string fato, Operador operador, JsonElement valor) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, operador, valor).Value!)]).Value!;

    public static PaginaDoModelo Pagina(string codigo = "DADOS", int ordem = 0, PredicadoDnf? exibicao = null) =>
        new(codigo, ordem, TipoPaginaFormulario.Secao, null, "Dados", null, null, exibicao);

    public static ItemDoModelo Item(string fato, int ordem = 0, PredicadoDnf? exibicao = null) =>
        new(fato, ordem, "DADOS", new ApresentacaoDoCampo(fato, TipoDeCampo.SimOuNao), null, new RegrasDoCampo(exibicao, Obrigatoriedade.Sempre, []));

    public static GrupoDoModelo Grupo(string codigo, int ordem = 0, PredicadoDnf? exibicao = null, params ItemDoModelo[] campos) =>
        new(codigo, ordem, "DADOS", "Grupo", new OcorrenciasDoGrupo(1, 10, IncluiCandidato: false), new ExibicaoEObrigatoriedade(exibicao, Obrigatoriedade.Sempre), campos);

    public static TermoDoModelo Termo(string codigo, int ordem = 0, PredicadoDnf? exibicao = null) =>
        new(codigo, ordem, Guid.NewGuid(), Guid.NewGuid(), new ExibicaoEObrigatoriedade(exibicao, Obrigatoriedade.Sempre));

    public static DocumentoExigidoDoModelo Documento(
        string tipo, PredicadoDnf? quando = null, ExigenciaPorOpcao? porOpcao = null, RequisitosDoDocumentoDoModelo? requisitos = null) =>
        new("INSCRICAO", tipo, quando, new IndeferimentoDoDocumentoDoModelo(false, "RETIRA_O_ACRESCIMO", []),
            requisitos ?? RequisitosDoDocumentoDoModelo.AindaNaoEscolhidos, porOpcao);

    public static ConjuntoDeDocumentosDoModelo Conjunto(params DocumentoExigidoDoModelo[] documentos) => new(documentos, []);

    public static ConjuntoDeDocumentosDoModelo ConjuntoComGrupos(params GrupoDeDocumentosDoModelo[] grupos) => new([], grupos);

    public static GrupoDeDocumentosDoModelo GrupoDeDocumentos(int? quantidadeMinima, params DocumentoExigidoDoModelo[] documentos) =>
        new(quantidadeMinima, null, documentos);

    /// <summary>Os requisitos que contam o prazo de emissão e a validade a partir do início de duas fases do cronograma.</summary>
    public static RequisitosDoDocumentoDoModelo RequisitosAPartirDasFases(string fasePrazo, string faseValidade) => new(
        new PrazoDeEmissaoDoModelo(
            EscolhaDoPrazoDeEmissao.ComPrazo, 90, UnidadeDoPrazoDeEmissao.Dias,
            new DataDeReferenciaDoDocumentoDoModelo(TipoDaDataDeReferencia.InicioDaFase, fasePrazo, null), EfeitoNaAnaliseDoDocumento.RecusarDocumento),
        new ValidadeDoDocumentoDoModelo(
            EscolhaDaValidade.ExigeDocumentoValido,
            new DataDeReferenciaDoDocumentoDoModelo(TipoDaDataDeReferencia.FimDaFase, faseValidade, null),
            EfeitoNaAnaliseDoDocumento.AceitarComAviso));
}
