namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.ConteudosDoModeloDeTeste;

/// <summary>
/// O grafo de coleta do conteúdo do modelo: o que as regras citam tem de ser coletado, derivado ou
/// pressuposto, e o campo opcional não alimenta regra. Só é conferido sobre partes bem formadas.
/// </summary>
public sealed class ConferenciaDoConteudoDoModeloGrafoTests
{
    private static readonly Dictionary<string, IReadOnlyCollection<string>> SemDerivacoes = new(StringComparer.Ordinal);

    private static readonly EtapaDoModelo Revisao = new("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null);

    private static ConteudoDoModelo Conteudo(params ItemDoModelo[] itens) => new("Habilitação", [Etapa(), Revisao], itens, [], [], []);

    private static List<FieldError> Conferir(
        ConteudoDoModelo conteudo,
        FinalidadeFormulario finalidade = FinalidadeFormulario.Habilitacao,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? derivacoes = null) =>
        ConferenciaDoConteudoDoModelo.Conferir(finalidade, conteudo, derivacoes ?? SemDerivacoes);

    private static ItemDoModelo Opcional(string fato, int ordem = 0) => Item(fato, ordem).ComObrigatoriedade(Obrigatoriedade.Nunca);

    [Fact(DisplayName = "A regra do item que cita fato que o modelo não coleta é recusada, sem campo, no caminho do conteúdo")]
    public void Item_CitaFatoDesconhecido_Recusa()
    {
        List<FieldError> erros = Conferir(Conteudo(Item("CERTIFICADO", 0, Quando("CONCLUSAO_REGULAR"))));

        erros.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "conteudo",
            Error = new { Code = GrafoFormularioErrorCodes.CitaFatoNaoConhecido },
        });
    }

    [Fact(DisplayName = "O pressuposto é fato conhecido antes do formulário: a regra que o cita passa")]
    public void Item_CitaPressuposto_Aceita() =>
        Conferir(Conteudo(Item("CERTIFICADO", 0, Quando("CONCLUSAO_REGULAR"))) with { Pressupostos = ["CONCLUSAO_REGULAR"] }).Should().BeEmpty();

    [Fact(DisplayName = "O termo só cita o que o modelo coleta")]
    public void Termo_CitaFatoDesconhecido_Recusa()
    {
        ConteudoDoModelo conteudo = Conteudo(Item("CERTIFICADO")) with { Termos = [Termo("LGPD", 0, Quando("OUTRO_FATO"))] };

        Conferir(conteudo).Should().ContainSingle().Which.Error.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
    }

    [Fact(DisplayName = "O documento exigido que cita fato que o modelo não coleta é recusado no índice dele, e o que cita o coletado passa")]
    public void Documento_CitaFatoDesconhecido_RecusaNoIndice()
    {
        ConteudoDoModelo conteudo = Conteudo(Item("CERTIFICADO")) with
        {
            DocumentosExigidos = [Documento("RG", Quando("CERTIFICADO")), Documento("LAUDO_MEDICO", Quando("OUTRO_FATO"))],
        };

        FieldError recusa = Conferir(conteudo).Should().ContainSingle().Subject;

        recusa.Field.Should().Be("conteudo.documentosExigidos[1].quando[0][0].fato");
        recusa.Error.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
        recusa.Error.Message.Should().Be("O documento exigido usa um dado que o modelo não coleta, não calcula e não recebe de outro formulário.");
    }

    [Fact(DisplayName = "A recusa aponta a referência desconhecida entre várias condições")]
    public void Documento_VariasCondicoes_ApontaReferenciaDesconhecida()
    {
        JsonElement valor = JsonSerializer.SerializeToElement(true);
        PredicadoDnf condicao = PredicadoDnf.CriarDeCondicoesAgrupadas(
        [
            (0, CondicaoDnf.Criar("CERTIFICADO", Operador.Igual, valor).Value!),
            (1, CondicaoDnf.Criar("CERTIFICADO", Operador.Igual, valor).Value!),
            (1, CondicaoDnf.Criar("OUTRO_FATO", Operador.Igual, valor).Value!),
        ]).Value!;
        ConteudoDoModelo conteudo = Conteudo(Item("CERTIFICADO")) with { DocumentosExigidos = [Documento("RG", condicao)] };

        FieldError recusa = Conferir(conteudo).Should().ContainSingle().Subject;

        recusa.Field.Should().Be("conteudo.documentosExigidos[0].quando[1][1].fato");
        recusa.Error.Message.Should().NotContain("OUTRO_FATO");
    }

    [Fact(DisplayName = "A citação do termo é recusada antes da do documento exigido: o grafo para na primeira recusa")]
    public void Grafo_ParaNaPrimeiraRecusa()
    {
        ConteudoDoModelo conteudo = Conteudo(Item("CERTIFICADO")) with
        {
            Termos = [Termo("LGPD", 0, Quando("OUTRO_FATO"))],
            DocumentosExigidos = [Documento("LAUDO_MEDICO", Quando("OUTRO_FATO"))],
        };

        Conferir(conteudo).Should().ContainSingle().Which.Error.Message.Should().NotContain("documento exigido");
    }

    [Fact(DisplayName = "O grafo só é conferido depois da estrutura: com o item fora de seção, a citação desconhecida não é lida")]
    public void Grafo_NaoEConferidoComEstruturaRecusada()
    {
        ConteudoDoModelo conteudo = Conteudo(Item("CERTIFICADO", 0, Quando("OUTRO_FATO")) with { EtapaCodigo = null });

        Conferir(conteudo).Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.ItemForaDeSecao);
    }

    [Fact(DisplayName = "O campo opcional que alimenta a negação de uma regra é recusado; obrigatório passa")]
    public void CampoOpcional_AlimentaNegacao_Recusa()
    {
        ItemDoModelo cota = Item("COTA", 1, Quando("MAIOR_IDADE", Operador.Diferente));

        Conferir(Conteudo(Opcional("MAIOR_IDADE"), cota)).Should().ContainSingle()
            .Which.Error.Code.Should().Be(ItemFormularioErrorCodes.OpcionalQueAlimentaRegra);
        Conferir(Conteudo(Item("MAIOR_IDADE"), cota)).Should().BeEmpty();
    }

    [Fact(DisplayName = "O campo opcional que alimenta derivado citado, direta ou por outro derivado, é recusado")]
    public void CampoOpcional_AlimentaDerivadoCitadoTransitivamente_Recusa()
    {
        Dictionary<string, IReadOnlyCollection<string>> derivacoes = new(StringComparer.Ordinal)
        {
            ["EGRESSO_ESCOLA_PUBLICA"] = ["TIPO_ESCOLA"],
            ["COTA_DERIVADA"] = ["EGRESSO_ESCOLA_PUBLICA"],
        };

        List<FieldError> erros = Conferir(
            Conteudo(Opcional("TIPO_ESCOLA"), Item("COTA", 1, Quando("COTA_DERIVADA"))), FinalidadeFormulario.Inscricao, derivacoes);

        erros.Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.OpcionalQueAlimentaRegra);
    }

    [Fact(DisplayName = "O campo opcional que só alimenta derivado que nenhuma regra cita não é recusado")]
    public void CampoOpcional_AlimentaDerivadoNaoCitado_Aceita()
    {
        Dictionary<string, IReadOnlyCollection<string>> derivacoes = new(StringComparer.Ordinal) { ["OUTRO_DERIVADO"] = ["RENDA_DECLARADA"] };

        Conferir(Conteudo(Opcional("RENDA_DECLARADA"), Item("COTA", 1)), FinalidadeFormulario.Inscricao, derivacoes).Should().BeEmpty();
    }

    [Theory(DisplayName = "Derivados que se citam em cadeia e em diamante chegam à mesma base: opcional é recusado uma vez, obrigatório passa")]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void CampoOpcional_AlimentaDerivadosEmCadeiaEDiamante(bool baseOpcional, int recusas)
    {
        Dictionary<string, IReadOnlyCollection<string>> derivacoes = new(StringComparer.Ordinal)
        {
            ["DERIVADO_1"] = ["DERIVADO_2", "DERIVADO_3"],
            ["DERIVADO_2"] = ["DERIVADO_3"],
            ["DERIVADO_3"] = ["BASE"],
        };
        ItemDoModelo @base = baseOpcional ? Opcional("BASE") : Item("BASE");

        List<FieldError> erros = Conferir(Conteudo(@base, Item("COTA", 1, Quando("DERIVADO_1"))), FinalidadeFormulario.Inscricao, derivacoes);

        erros.Select(static e => e.Error.Code).Should().HaveCount(recusas).And.AllBe(ItemFormularioErrorCodes.OpcionalQueAlimentaRegra);
    }

    [Fact(DisplayName = "Fato que se deriva de um derivado que depende dele de volta é expandido uma vez só, e a base opcional continua recusada")]
    public void CampoOpcional_AlimentaDerivadosQueSeCitamDeVolta_RecusaSemLacoInfinito()
    {
        Dictionary<string, IReadOnlyCollection<string>> derivacoes = new(StringComparer.Ordinal)
        {
            ["CICLO_A"] = ["CICLO_B"],
            ["CICLO_B"] = ["CICLO_A", "BASE"],
        };

        List<FieldError> erros = Conferir(
            Conteudo(Item("CICLO_A"), Opcional("BASE", 1), Item("COTA", 2, Quando("CICLO_A"))), FinalidadeFormulario.Inscricao, derivacoes);

        FieldError recusa = erros.Should().ContainSingle().Subject;
        recusa.Error.Code.Should().Be(ItemFormularioErrorCodes.OpcionalQueAlimentaRegra);
        recusa.Error.Message.Should().Contain("'BASE'");
    }
}
