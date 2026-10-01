namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// As regras próprias do modelo de formulário (UNI-REQ-0144, ADR-0137): os pressupostos, o grafo de
/// coleta sobre eles e as derivações do catálogo, e a seção obrigatória para todo item. As regras de
/// forma compartilhadas com o processo são as de <c>Unifesspa.UniPlus.Regras</c>.
/// </summary>
public sealed class ModeloFormularioTests
{
    private static readonly Dictionary<string, IReadOnlyCollection<string>> SemDerivacoes = new(StringComparer.Ordinal);

    private static readonly EtapaDoModelo Dados = new("DADOS", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null, null);
    private static readonly EtapaDoModelo Revisao = new("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null);

    private static PredicadoDnf Quando(string fato, Operador operador = Operador.Igual) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, operador, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;

    private static ItemDoModelo Item(string fato, int ordem, Obrigatoriedade? obrigatoriedade = null, PredicadoDnf? exibicao = null, string? etapa = "DADOS") =>
        new(fato, ordem, etapa, fato, TipoRenderizacao.Booleano, null, null, obrigatoriedade ?? Obrigatoriedade.Sempre, exibicao, [], false);

    private static ConteudoDoModelo Conteudo(IReadOnlyList<ItemDoModelo> itens, params string[] pressupostos) =>
        new("Habilitação", [Dados, Revisao], itens, [], pressupostos);

    private static Result<ModeloFormulario> Criar(
        FinalidadeFormulario finalidade, ConteudoDoModelo conteudo, IReadOnlyDictionary<string, IReadOnlyCollection<string>>? derivacoes = null) =>
        ModeloFormulario.Criar("HABILITACAO_MEDICINA", "Habilitação Medicina 2027", null, finalidade, "PSR", conteudo, derivacoes ?? SemDerivacoes);

    [Fact(DisplayName = "Modelo de habilitação cita fato pressuposto da inscrição e nasce ativo")]
    public void Habilitacao_CitaPressuposto_Aceita()
    {
        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO_EMITIDO", 0, exibicao: Quando("CONCLUSAO_REGULAR"))], "CONCLUSAO_REGULAR"));

        modelo.IsSuccess.Should().BeTrue(modelo.Error?.Message);
        modelo.Value!.Ativo.Should().BeTrue();
    }

    [Fact(DisplayName = "Sem o pressuposto, a regra que cita fato da inscrição é recusada")]
    public void Habilitacao_CitaFatoSemPressuposto_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO_EMITIDO", 0, exibicao: Quando("CONCLUSAO_REGULAR"))]));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
    }

    [Fact(DisplayName = "Modelo de inscrição não tem pressupostos")]
    public void Inscricao_ComPressuposto_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Inscricao, Conteudo([Item("COR_RACA", 0)], "SEXO"));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.PressupostoNaInscricao);
    }

    [Fact(DisplayName = "Fato não é pressuposto e item do mesmo modelo")]
    public void PressupostoTambemItem_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Habilitacao, Conteudo([Item("CONCLUSAO_REGULAR", 0)], "CONCLUSAO_REGULAR"));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.PressupostoTambemItem);
    }

    [Fact(DisplayName = "Derivado do catálogo cujas dependências são pressupostas pode ser citado")]
    public void DerivadoDosPressupostos_Aceita()
    {
        Dictionary<string, IReadOnlyCollection<string>> derivacoes = new(StringComparer.Ordinal) { ["EGRESSO_ESCOLA_PUBLICA"] = ["TIPO_ESCOLA"] };

        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao,
            Conteudo([Item("DECLARACAO_ESCOLAR", 0, exibicao: Quando("EGRESSO_ESCOLA_PUBLICA"))], "TIPO_ESCOLA"),
            derivacoes);

        modelo.IsSuccess.Should().BeTrue(modelo.Error?.Message);
    }

    [Fact(DisplayName = "Campo opcional que alimenta um derivado citado pelo modelo é recusado; o que alimenta derivado não citado, não")]
    public void CampoOpcionalQueAlimentaDerivadoCitado_Recusa()
    {
        Dictionary<string, IReadOnlyCollection<string>> derivacoes = new(StringComparer.Ordinal)
        {
            ["EGRESSO_ESCOLA_PUBLICA"] = ["TIPO_ESCOLA"],
            ["OUTRO_DERIVADO"] = ["RENDA_DECLARADA"],
        };

        Result<ModeloFormulario> citado = ModeloFormulario.Criar(
            "INSCRICAO_MEDICINA", "Inscrição Medicina 2027", null, FinalidadeFormulario.Inscricao, null,
            Conteudo([Item("TIPO_ESCOLA", 0, Obrigatoriedade.Nunca), Item("COTA", 1, exibicao: Quando("EGRESSO_ESCOLA_PUBLICA"))]),
            derivacoes);
        Result<ModeloFormulario> naoCitado = ModeloFormulario.Criar(
            "INSCRICAO_MEDICINA", "Inscrição Medicina 2027", null, FinalidadeFormulario.Inscricao, null,
            Conteudo([Item("RENDA_DECLARADA", 0, Obrigatoriedade.Nunca), Item("COTA", 1, exibicao: Quando("EGRESSO_ESCOLA_PUBLICA"))]),
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal) { ["EGRESSO_ESCOLA_PUBLICA"] = [], ["OUTRO_DERIVADO"] = ["RENDA_DECLARADA"] });

        citado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.OpcionalQueAlimentaRegra);
        naoCitado.IsSuccess.Should().BeTrue(naoCitado.Error?.Message);
    }

    [Fact(DisplayName = "Todo item do modelo está numa seção")]
    public void ItemSemSecao_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO_EMITIDO", 0, etapa: null)]));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.ItemForaDeSecao);
    }

    [Fact(DisplayName = "O teto de itens do formulário vale para o modelo")]
    public void ItensAcimaDoTeto_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao, Conteudo([.. Enumerable.Range(0, FormaDoItem.MaximoDeItens + 1).Select(static i => Item($"FATO_{i}", i))]));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.ItensEmExcesso);
    }

    [Fact(DisplayName = "Fato que só difere por espaço é o mesmo fato: aparecer duas vezes é recusado")]
    public void FatoRepetidoPorEspaco_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO", 0), Item("CERTIFICADO ", 1)]));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(GrafoFormularioErrorCodes.FatoDuplicado);
    }

    [Fact(DisplayName = "Etapa cujo código só difere por espaço repete o código")]
    public void EtapaRepetidaPorEspaco_Recusa()
    {
        EtapaDoModelo outraDados = Dados with { Codigo = " DADOS", Ordem = 2 };

        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao, new ConteudoDoModelo("Habilitação", [Dados, outraDados, Revisao with { Ordem = 3 }], [Item("CERTIFICADO", 0)], [], []));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.EtapaCodigoDuplicado);
    }

    [Fact(DisplayName = "Pressuposto que só difere do item por espaço é o próprio item")]
    public void PressupostoQueEItemPorEspaco_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Habilitacao, Conteudo([Item("CONCLUSAO_REGULAR", 0)], "CONCLUSAO_REGULAR "));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.PressupostoTambemItem);
    }

    [Fact(DisplayName = "Pressuposto em branco é recusado")]
    public void PressupostoEmBranco_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO", 0)], "  "));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.PressupostoEmBranco);
    }

    [Fact(DisplayName = "Texto com caractere nulo, no descritivo, no item ou num valor citado por regra, é recusado antes de chegar ao banco")]
    public void TextoComCaractereNulo_Recusa()
    {
        Result<ModeloFormulario> descricao = ModeloFormulario.Criar(
            "HABILITACAO_MEDICINA", "Habilitação", "texto\0", FinalidadeFormulario.Habilitacao, null, Conteudo([Item("CERTIFICADO", 0)]), SemDerivacoes);
        Result<ModeloFormulario> rotulo = Criar(FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO", 0) with { Rotulo = "Certificado\0" }]));
        PredicadoDnf valorNulo = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("CONCLUSAO_REGULAR", Operador.Igual, JsonSerializer.SerializeToElement("A\0")).Value!)]).Value!;
        Result<ModeloFormulario> valor = Criar(
            FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO", 0, exibicao: valorNulo)], "CONCLUSAO_REGULAR"));

        PredicadoDnf objetoNulo = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("CONCLUSAO_REGULAR", Operador.Em, JsonSerializer.SerializeToElement(new[] { new Dictionary<string, string> { ["a\0"] = "b" } })).Value!)]).Value!;
        Result<ModeloFormulario> objeto = Criar(
            FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO", 0, exibicao: objetoNulo)], "CONCLUSAO_REGULAR"));

        descricao.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.TextoNaoGravavel);
        objeto.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.TextoNaoGravavel);
        rotulo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.TextoNaoGravavel);
        valor.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.TextoNaoGravavel);
    }

    [Fact(DisplayName = "Código com surrogate sem par é recusado, sem exceção na normalização")]
    public void CodigoComSurrogateSemPar_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO\uD800", 0) with { Rotulo = "Certificado" }]));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.TextoNaoGravavel);
    }

    [Fact(DisplayName = "Código de termo com surrogate sem par é recusado, sem exceção na conferência de unicidade")]
    public void CodigoDeTermoComSurrogateSemPar_Recusa()
    {
        TermoDoModelo termo = new("LGPD\uD800", 0, Guid.NewGuid(), Guid.NewGuid(), null, Obrigatoriedade.Sempre);

        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao, new ConteudoDoModelo("Habilitação", [Dados, Revisao], [Item("CERTIFICADO", 0)], [termo], []));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.TextoNaoGravavel);
    }

    [Fact(DisplayName = "O tamanho do código do modelo é o do código normalizado, que é o gravado")]
    public void CodigoQueCresceNaNormalizacao_Recusa()
    {
        string codigo = string.Concat(Enumerable.Repeat("\u0958", ModeloFormulario.CodigoMaxLength));

        Result<ModeloFormulario> modelo = ModeloFormulario.Criar(
            codigo, "Habilitação", null, FinalidadeFormulario.Habilitacao, null, Conteudo([Item("CERTIFICADO", 0)]), SemDerivacoes);

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.CodigoTamanho);
    }

    [Fact(DisplayName = "A recusa de restrição aponta a posição em que ela veio")]
    public void RestricaoIncoerente_ApontaAPosicaoEnviada()
    {
        ItemDoModelo item = Item("IDADE", 0) with
        {
            TipoRenderizacao = TipoRenderizacao.Numero,
            Restricoes = [new TamanhoTexto(1, 10), new FaixaNumerica(0, 10)],
        };

        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Habilitacao, Conteudo([item]));

        FieldError recusa = modelo.Errors.Should().ContainSingle().Subject;
        recusa.Error.Code.Should().Be(ItemFormularioErrorCodes.RestricaoIncoerente);
        recusa.Field.Should().Be("conteudo.itens[0].restricoes[0]");
    }

    [Fact(DisplayName = "Tipo de etapa e tipo de campo fora do vocabulário são recusados")]
    public void EnumsForaDoVocabulario_Recusa()
    {
        Result<ModeloFormulario> etapa = Criar(
            FinalidadeFormulario.Habilitacao,
            new ConteudoDoModelo("Habilitação", [Dados with { Tipo = (TipoEtapaFormulario)99 }, Revisao], [Item("CERTIFICADO", 0)], [], []));
        Result<ModeloFormulario> campo = Criar(
            FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO", 0) with { TipoRenderizacao = (TipoRenderizacao)99 }]));

        etapa.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.TipoDeEtapaObrigatorio);
        campo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.TipoRenderizacaoObrigatorio);
    }

    [Fact(DisplayName = "Tipo de processo só com espaços é recusado, em vez de virar o modelo de todos os tipos")]
    public void TipoProcessoEmBranco_Recusa()
    {
        Result<ModeloFormulario> modelo = ModeloFormulario.Criar(
            "HABILITACAO_MEDICINA", "Habilitação", null, FinalidadeFormulario.Habilitacao, "   ", Conteudo([Item("CERTIFICADO", 0)]), SemDerivacoes);

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.TipoProcessoCodigoEmBranco);
    }

    [Fact(DisplayName = "Finalidade inválida é recusada no cadastro, não no conteúdo, e a estrutura que depende dela não é conferida")]
    public void FinalidadeInvalida_RecusaNoCadastro()
    {
        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Nenhuma, Conteudo([Item("CERTIFICADO", 0)]));

        modelo.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new FieldError("finalidade", new DomainError(EstruturaFormularioErrorCodes.FinalidadeInvalida, string.Empty)),
            static o => o.Excluding(static e => e.Error.Message));
    }

    [Fact(DisplayName = "Desativar e reativar alternam o modelo; repetir o estado atual é recusado")]
    public void AtivarDesativar_AlternaERecusaORepetido()
    {
        ModeloFormulario modelo = Criar(FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO", 0)])).Value!;

        modelo.Ativar().Error!.Code.Should().Be(ModeloFormularioErrorCodes.JaAtivo);
        modelo.Desativar().IsSuccess.Should().BeTrue();
        modelo.Ativo.Should().BeFalse();
        modelo.Desativar().Error!.Code.Should().Be(ModeloFormularioErrorCodes.JaDesativado);
        modelo.Ativar().IsSuccess.Should().BeTrue();
        modelo.Ativo.Should().BeTrue();
    }
}
