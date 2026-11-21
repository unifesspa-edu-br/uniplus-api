namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

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
        new(
            fato,
            ordem,
            etapa,
            new ApresentacaoDoCampo(fato, TipoRenderizacao.Booleano),
            null,
            new RegrasDoCampo(exibicao, obrigatoriedade ?? Obrigatoriedade.Sempre, []));

    private static ConteudoDoModelo Conteudo(IReadOnlyList<ItemDoModelo> itens, params string[] pressupostos) =>
        new("Habilitação", [Dados, Revisao], itens, [], pressupostos, []);

    private static Result<ModeloFormulario> Criar(
        FinalidadeFormulario finalidade, ConteudoDoModelo conteudo, IReadOnlyDictionary<string, IReadOnlyCollection<string>>? derivacoes = null) =>
        ModeloFormulario.Criar("HABILITACAO_MEDICINA", "Habilitação Medicina 2027", null, finalidade, "PSR", conteudo, derivacoes ?? SemDerivacoes);

    private static Result<ModeloFormulario> CriarInscricao(ConteudoDoModelo conteudo, IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes) =>
        ModeloFormulario.Criar("INSCRICAO_MEDICINA", "Inscrição Medicina 2027", null, FinalidadeFormulario.Inscricao, null, conteudo, derivacoes);

    private static GrupoDoModelo Composicao(params ItemDoModelo[] campos) =>
        new("COMPOSICAO_FAMILIAR", 1, "DADOS", "Composição familiar", 1, 10, null, Obrigatoriedade.Sempre, campos);

    private static ItemDoModelo Campo(string fato, int ordem, Obrigatoriedade? obrigatoriedade = null, PredicadoDnf? exibicao = null) =>
        Item(fato, ordem, obrigatoriedade, exibicao, etapa: null);

    [Fact(DisplayName = "O grupo repetível entra no modelo, e o campo do grupo cita o campo anterior da mesma ocorrência")]
    public void Grupo_CampoCitaCampoAnteriorDaOcorrencia_Aceita()
    {
        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao,
            Conteudo([Item("RENDA_FAMILIAR", 0)]) with
            {
                Grupos = [Composicao(Campo("MAIOR_IDADE", 0), Campo("SEM_RENDA", 1, exibicao: Quando("MAIOR_IDADE")))],
            });

        modelo.IsSuccess.Should().BeTrue(modelo.Error?.Message);
        modelo.Value!.Conteudo.Grupos.Should().ContainSingle().Which.Subitens.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Item que cita campo do grupo é recusado: fora da ocorrência, o membro não tem valor único")]
    public void Grupo_ItemCitaCampoDoGrupo_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao,
            Conteudo([Item("RENDA_FAMILIAR", 2, exibicao: Quando("MAIOR_IDADE"))]) with { Grupos = [Composicao(Campo("MAIOR_IDADE", 0))] });

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoDeMembroForaDoGrupo);
    }

    [Fact(DisplayName = "Campo do grupo opcional citado por negação na ocorrência é recusado, como o item")]
    public void Grupo_CampoOpcionalQueAlimentaNegacao_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao,
            Conteudo([]) with
            {
                Grupos = [Composicao(Campo("MAIOR_IDADE", 0, Obrigatoriedade.Nunca), Campo("SEM_RENDA", 1, exibicao: Quando("MAIOR_IDADE", Operador.Diferente)))],
            });

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.OpcionalQueAlimentaRegra);
    }

    [Fact(DisplayName = "Item opcional que alimenta derivado citado por campo do grupo é recusado")]
    public void Grupo_CampoCitaDerivadoDeItemOpcional_Recusa()
    {
        Dictionary<string, IReadOnlyCollection<string>> derivacoes = new(StringComparer.Ordinal) { ["BAIXA_RENDA"] = ["RENDA_DECLARADA"] };

        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao,
            Conteudo([Item("RENDA_DECLARADA", 0, Obrigatoriedade.Nunca)]) with
            {
                Grupos = [Composicao(Campo("SEM_RENDA", 0, exibicao: Quando("BAIXA_RENDA")))],
            },
            derivacoes);

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.OpcionalQueAlimentaRegra);
    }

    [Fact(DisplayName = "O teto do formulário conta cada grupo e cada campo de grupo")]
    public void Grupo_TetoContaGrupoECampos_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao,
            Conteudo([.. Enumerable.Range(0, FormaDoItem.MaximoDeItens - 2).Select(static i => Item($"ITEM_{i}", i + 1))]) with
            {
                Grupos = [Composicao(Campo("MAIOR_IDADE", 0), Campo("SEM_RENDA", 1)) with { Ordem = 0 }],
            });

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.ItensEmExcesso);
    }

    [Fact(DisplayName = "Modelo de habilitação cita fato pressuposto da inscrição")]
    public void Habilitacao_CitaPressuposto_Aceita()
    {
        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO_EMITIDO", 0, exibicao: Quando("CONCLUSAO_REGULAR"))], "CONCLUSAO_REGULAR"));

        modelo.IsSuccess.Should().BeTrue(modelo.Error?.Message);
    }

    [Fact(DisplayName = "O modelo nasce desativado, fora da escolha de processos novos, e é ativado quando pronto")]
    public void Criar_NasceDesativado_AtivacaoLiberaParaAEscolha()
    {
        ModeloFormulario modelo = Criar(FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO", 0)])).Value!;

        modelo.Ativo.Should().BeFalse();
        modelo.Ativar().IsSuccess.Should().BeTrue();
        modelo.Ativo.Should().BeTrue();
    }

    [Fact(DisplayName = "Sem o pressuposto, a regra que cita fato da inscrição é recusada")]
    public void Habilitacao_CitaFatoSemPressuposto_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO_EMITIDO", 0, exibicao: Quando("CONCLUSAO_REGULAR"))]));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
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

    [Fact(DisplayName = "Campo opcional que alimenta um derivado citado pelo modelo é recusado")]
    public void CampoOpcionalQueAlimentaDerivadoCitado_Recusa()
    {
        Dictionary<string, IReadOnlyCollection<string>> derivacoes = new(StringComparer.Ordinal)
        {
            ["EGRESSO_ESCOLA_PUBLICA"] = ["TIPO_ESCOLA"],
            ["OUTRO_DERIVADO"] = ["RENDA_DECLARADA"],
        };

        Result<ModeloFormulario> citado = CriarInscricao(
            Conteudo([Item("TIPO_ESCOLA", 0, Obrigatoriedade.Nunca), Item("COTA", 1, exibicao: Quando("EGRESSO_ESCOLA_PUBLICA"))]),
            derivacoes);

        citado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.OpcionalQueAlimentaRegra);
    }

    [Fact(DisplayName = "Campo opcional que alimenta um derivado não citado pelo modelo é aceito")]
    public void CampoOpcionalQueAlimentaDerivadoNaoCitado_Aceita()
    {
        Dictionary<string, IReadOnlyCollection<string>> derivacoes = new(StringComparer.Ordinal)
        {
            ["EGRESSO_ESCOLA_PUBLICA"] = [],
            ["OUTRO_DERIVADO"] = ["RENDA_DECLARADA"],
        };

        Result<ModeloFormulario> naoCitado = CriarInscricao(
            Conteudo([Item("RENDA_DECLARADA", 0, Obrigatoriedade.Nunca), Item("COTA", 1, exibicao: Quando("EGRESSO_ESCOLA_PUBLICA"))]),
            derivacoes);

        naoCitado.IsSuccess.Should().BeTrue(naoCitado.Error?.Message);
    }

    [Fact(DisplayName = "Todo item do modelo está numa seção")]
    public void ItemSemSecao_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO_EMITIDO", 0, etapa: null)]));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.ItemForaDeSecao);
    }

    [Fact(DisplayName = "O item com impedimento fora do modelo de inscrição é recusado")]
    public void ImpedimentoNaHabilitacao_Recusa()
    {
        ItemDoModelo comImpedimento = Item("CERTIFICADO_EMITIDO", 0)
            .ComImpedimento(new Impedimento(Quando("CERTIFICADO_EMITIDO"), "Quem já tem o certificado não se habilita por este processo."));

        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Habilitacao, Conteudo([comImpedimento]));

        modelo.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "conteudo.itens[0].regras.impedimento",
            Error = new { Code = ItemFormularioErrorCodes.ImpedimentoForaDaInscricao },
        });
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
            FinalidadeFormulario.Habilitacao, new ConteudoDoModelo("Habilitação", [Dados, outraDados, Revisao with { Ordem = 3 }], [Item("CERTIFICADO", 0)], [], [], []));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.EtapaCodigoDuplicado);
    }

    [Fact(DisplayName = "Pressuposto que só difere do item por espaço é o próprio item")]
    public void PressupostoQueEItemPorEspaco_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Habilitacao, Conteudo([Item("CONCLUSAO_REGULAR", 0)], "CONCLUSAO_REGULAR "));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.PressupostoTambemItem);
    }

    [Theory(DisplayName = "Documento do conjunto padrão que cita fato que o modelo não coleta é recusado, e o que cita fato coletado passa")]
    [InlineData("CERTIFICADO", true)]
    [InlineData("OUTRO_FATO", false)]
    public void DocumentoCitaFatoQueOModeloNaoColeta_Recusa(string fatoCitado, bool aceito)
    {
        PredicadoDnf quando = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar(fatoCitado, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;
        ConteudoDoModelo conteudo = Conteudo([Item("CERTIFICADO", 0)]) with
        {
            DocumentosExigidos = [new("INSCRICAO", "LAUDO_MEDICO", false, quando, "REMOVE_VANTAGEM", [])],
        };

        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Habilitacao, conteudo);

        modelo.IsSuccess.Should().Be(aceito, string.Join("; ", modelo.Errors.Select(static e => e.Error.Message)));
    }

    [Fact(DisplayName = "Pressuposto em branco é recusado")]
    public void PressupostoEmBranco_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO", 0)], "  "));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.PressupostoEmBranco);
    }

    private static void DeveRecusarTextoNaoGravavelEm(Result<ModeloFormulario> modelo, string campo)
    {
        FieldError recusa = modelo.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be(campo);
        recusa.Error.Code.Should().Be(ModeloFormularioErrorCodes.TextoNaoGravavel);
    }

    private static PredicadoDnf QuandoOValorE(Operador operador, JsonElement valor) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("CONCLUSAO_REGULAR", operador, valor).Value!)]).Value!;

    [Fact(DisplayName = "Texto com caractere nulo na descrição é recusado antes de chegar ao banco, com o código de texto não gravável")]
    public void TextoComCaractereNulo_NaDescricao_Recusa()
    {
        Result<ModeloFormulario> modelo = ModeloFormulario.Criar(
            "HABILITACAO_MEDICINA", "Habilitação", "texto\0", FinalidadeFormulario.Habilitacao, null, Conteudo([Item("CERTIFICADO", 0)]), SemDerivacoes);

        DeveRecusarTextoNaoGravavelEm(modelo, "descricao");
    }

    [Fact(DisplayName = "Texto com caractere nulo no rótulo do item é recusado no caminho do conteúdo")]
    public void TextoComCaractereNulo_NoRotuloDoItem_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO", 0).ComRotulo("Certificado\0")]));

        DeveRecusarTextoNaoGravavelEm(modelo, "conteudo.itens[0].apresentacao.rotulo");
    }

    [Fact(DisplayName = "Caractere nulo num valor citado por regra do item é recusado antes de chegar ao banco")]
    public void TextoComCaractereNulo_NoValorDaRegra_Recusa()
    {
        PredicadoDnf valorNulo = QuandoOValorE(Operador.Igual, JsonSerializer.SerializeToElement("A\0"));

        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO", 0, exibicao: valorNulo)], "CONCLUSAO_REGULAR"));

        DeveRecusarTextoNaoGravavelEm(modelo, "conteudo.itens[0].regras.exibicao");
    }

    [Fact(DisplayName = "Caractere nulo num valor citado pela obrigatoriedade do item é recusado no predicado de obrigatoriedade")]
    public void TextoComCaractereNulo_NoValorDaObrigatoriedade_Recusa()
    {
        PredicadoDnf valorNulo = QuandoOValorE(Operador.Igual, JsonSerializer.SerializeToElement("A\0"));

        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO", 0, Obrigatoriedade.Quando(valorNulo))], "CONCLUSAO_REGULAR"));

        DeveRecusarTextoNaoGravavelEm(modelo, "conteudo.itens[0].regras.obrigatoriedade.predicado");
    }

    [Fact(DisplayName = "Caractere nulo no nome de propriedade de um valor JSON citado por regra é recusado")]
    public void TextoComCaractereNulo_NoNomeDePropriedadeDoValor_Recusa()
    {
        JsonElement objetoNulo = JsonSerializer.SerializeToElement(new[] { new Dictionary<string, string> { ["a\0"] = "b" } });

        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO", 0, exibicao: QuandoOValorE(Operador.Em, objetoNulo))], "CONCLUSAO_REGULAR"));

        DeveRecusarTextoNaoGravavelEm(modelo, "conteudo.itens[0].regras.exibicao");
    }

    private static Result<ModeloFormulario> CriarComDocumento(string tipo, string valor, string observacao)
    {
        PredicadoDnf quando = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("CERTIFICADO", Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!)]).Value!;
        DocumentoExigidoDoModelo documento = new(
            "INSCRICAO", tipo, false, quando, "REMOVE_VANTAGEM", [new("Edital", "INTERNA_EDITAL", "PENDENTE", observacao)]);

        return Criar(FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO", 0)]) with { DocumentosExigidos = [documento] });
    }

    [Fact(DisplayName = "Caractere nulo no código do tipo do documento exigido do conjunto padrão é recusado")]
    public void DocumentoComCaractereNulo_NoCodigoDoTipo_Recusa() =>
        DeveRecusarTextoNaoGravavelEm(CriarComDocumento("LAUDO\0", "A", "x"), "conteudo.documentosExigidos[0].tipoDocumentoCodigo");

    [Fact(DisplayName = "Caractere nulo no valor da condição do documento exigido é recusado")]
    public void DocumentoComCaractereNulo_NaCondicao_Recusa() =>
        DeveRecusarTextoNaoGravavelEm(CriarComDocumento("LAUDO", "A\0", "x"), "conteudo.documentosExigidos[0].quando");

    [Fact(DisplayName = "Caractere nulo na base legal do documento exigido é recusado")]
    public void DocumentoComCaractereNulo_NaBaseLegal_Recusa() =>
        DeveRecusarTextoNaoGravavelEm(CriarComDocumento("LAUDO", "A", "x\0"), "conteudo.documentosExigidos[0].basesLegais[0].observacao");

    [Fact(DisplayName = "Código com surrogate sem par é recusado, sem exceção na normalização")]
    public void CodigoComSurrogateSemPar_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO\uD800", 0).ComRotulo("Certificado")]));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.TextoNaoGravavel);
    }

    [Fact(DisplayName = "Código de termo com surrogate sem par é recusado, sem exceção na conferência de unicidade")]
    public void CodigoDeTermoComSurrogateSemPar_Recusa()
    {
        TermoDoModelo termo = new("LGPD\uD800", 0, Guid.NewGuid(), Guid.NewGuid(), null, Obrigatoriedade.Sempre);

        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao, new ConteudoDoModelo("Habilitação", [Dados, Revisao], [Item("CERTIFICADO", 0)], [termo], [], []));

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
        ItemDoModelo item = Item("IDADE", 0)
            .ComTipoDeCampo(TipoRenderizacao.Numero)
            .ComRestricoes([new TamanhoTexto(1, 10), new FaixaNumerica(0, 10)]);

        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Habilitacao, Conteudo([item]));

        FieldError recusa = modelo.Errors.Should().ContainSingle().Subject;
        recusa.Error.Code.Should().Be(ItemFormularioErrorCodes.RestricaoIncoerente);
        recusa.Field.Should().Be("conteudo.itens[0].regras.restricoes[0]");
    }

    [Fact(DisplayName = "Tipo de etapa fora do vocabulário é recusado")]
    public void TipoDeEtapaForaDoVocabulario_Recusa()
    {
        Result<ModeloFormulario> etapa = Criar(
            FinalidadeFormulario.Habilitacao,
            new ConteudoDoModelo("Habilitação", [Dados with { Tipo = (TipoEtapaFormulario)99 }, Revisao], [Item("CERTIFICADO", 0)], [], [], []));

        etapa.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.TipoDeEtapaObrigatorio);
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
        modelo.Ativar().IsSuccess.Should().BeTrue();

        modelo.Ativar().Error!.Code.Should().Be(ModeloFormularioErrorCodes.JaAtivo);
        modelo.Desativar().IsSuccess.Should().BeTrue();
        modelo.Ativo.Should().BeFalse();
        modelo.Desativar().Error!.Code.Should().Be(ModeloFormularioErrorCodes.JaDesativado);
        modelo.Ativar().IsSuccess.Should().BeTrue();
        modelo.Ativo.Should().BeTrue();
    }
}
