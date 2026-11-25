namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O que a regra de aplicabilidade da modalidade pode citar no catálogo de fatos (ADR-0144): fato
/// citável e ativo, conhecido até a inscrição, com operador e valor do domínio dele, e sem depender
/// da própria modalidade.
/// </summary>
public sealed class ConferenciaDaRegraDeAplicabilidadeTests
{
    private const string Finalidade = "Enquadramento na reserva de vagas.";
    private const HipoteseLegalTratamento Hipotese = HipoteseLegalTratamento.CumprimentoObrigacaoLegal;

    [Fact(DisplayName = "Regra que cita fato citável, ativo e conhecido na inscrição é aceita")]
    public void Conferir_RegraValida_Aceita() =>
        ConferenciaDaRegraDeAplicabilidade.Conferir(Quando(("PCD", true)), vigente: null, Catalogo(Declarado("PCD")))
            .Should().BeEmpty();

    [Fact(DisplayName = "Texto não entra na regra de aplicabilidade")]
    public void Conferir_CitaTexto_Recusa()
    {
        FatoCandidato nome = Declarado("NOME", TipoDoDado.Texto, formato: FormatoTexto.NomePessoa);

        Codigos(ConferenciaDaRegraDeAplicabilidade.Conferir(Quando(("NOME", "MARIA")), vigente: null, Catalogo(nome)))
            .Should().Equal(ModalidadeErrorCodes.RegraCitaFatoNaoCitavel);
    }

    [Fact]
    public void Conferir_FatoNaoCitavel_NaoRepeteCodigo()
    {
        FatoCandidato fato = Declarado("FATO_DE_TESTE", TipoDoDado.Texto, formato: FormatoTexto.NomePessoa);
        FieldError erro = ConferenciaDaRegraDeAplicabilidade.Conferir(
            Quando(("FATO_DE_TESTE", "valor")), vigente: null, Catalogo(fato)).Single();

        erro.Field.Should().Be("regraDeAplicabilidade[0][0].fato");
        erro.Error.Code.Should().Be(ModalidadeErrorCodes.RegraCitaFatoNaoCitavel);
        erro.Error.Message.Should().Be("O fato não pode ser citado em regra: texto, data, endereço, fato de membro de grupo e categórico sem valores não entram em predicado.");
        erro.Origem.Should().Be("FATO_DE_TESTE");
    }

    [Fact]
    public void Conferir_FatoDesativado_NaoRepeteCodigo()
    {
        FatoCandidato fato = Declarado("FATO_DE_TESTE");
        fato.Desativar();
        FieldError erro = ConferenciaDaRegraDeAplicabilidade.Conferir(
            Quando(("FATO_DE_TESTE", true)), vigente: null, Catalogo(fato)).Single();

        erro.Field.Should().Be("regraDeAplicabilidade[0][0].fato");
        erro.Error.Code.Should().Be(ModalidadeErrorCodes.RegraCitaFatoDesativado);
        erro.Error.Message.Should().Be("O fato está desativado e não aceita citação nova.");
        erro.Origem.Should().Be("FATO_DE_TESTE");
    }

    [Fact]
    public void Conferir_FatoPosteriorAInscricao_NaoRepeteCodigo()
    {
        FatoCandidato fato = Declarado("FATO_DE_TESTE", ponto: "RESULTADO_FINAL");
        FieldError erro = ConferenciaDaRegraDeAplicabilidade.Conferir(
            Quando(("FATO_DE_TESTE", true)), vigente: null, Catalogo(fato)).Single();

        erro.Field.Should().Be("regraDeAplicabilidade[0][0].fato");
        erro.Error.Code.Should().Be(ModalidadeErrorCodes.RegraCitaFatoPosteriorAInscricao);
        erro.Error.Message.Should().Be("O fato só é conhecido em RESULTADO_FINAL, que não antecede a inscrição, em que a modalidade se decide.");
        erro.Origem.Should().Be("FATO_DE_TESTE");
    }

    [Fact(DisplayName = "Fato desativado recusa citação nova e mantém a que a regra vigente já fazia")]
    public void Conferir_FatoDesativado_SoRecusaCitacaoNova()
    {
        FatoCandidato pcd = Declarado("PCD");
        pcd.Desativar();
        CatalogoDeFatos catalogo = Catalogo(pcd);

        Codigos(ConferenciaDaRegraDeAplicabilidade.Conferir(Quando(("PCD", true)), vigente: null, catalogo))
            .Should().Equal(ModalidadeErrorCodes.RegraCitaFatoDesativado);
        ConferenciaDaRegraDeAplicabilidade.Conferir(Quando(("PCD", true)), vigente: Quando(("PCD", true)), catalogo)
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "Valor desativado recusa citação nova e mantém a que a regra vigente já fazia")]
    public void Conferir_ValorDesativado_SoRecusaCitacaoNova()
    {
        FatoCandidato corRaca = Declarado("COR_RACA", TipoDoDado.Opcao);
        corRaca.AdicionarValorDominio("PARDA", "Parda", 0, ativo: true);
        corRaca.DesativarValor("PARDA");
        CatalogoDeFatos catalogo = Catalogo(corRaca);

        Codigos(ConferenciaDaRegraDeAplicabilidade.Conferir(Quando(("COR_RACA", "PARDA")), vigente: null, catalogo))
            .Should().Equal(ModalidadeErrorCodes.RegraCitaValorDesativado);
        ConferenciaDaRegraDeAplicabilidade.Conferir(Quando(("COR_RACA", "PARDA")), vigente: Quando(("COR_RACA", "PARDA")), catalogo)
            .Should().BeEmpty();
    }

    [Fact]
    public void Conferir_ValorDesativado_NaoRepeteCodigos()
    {
        FatoCandidato fato = Declarado("FATO_DE_TESTE", TipoDoDado.Opcao);
        fato.AdicionarValorDominio("VALOR_DE_TESTE", "Valor", 0, ativo: true).IsSuccess.Should().BeTrue();
        fato.DesativarValor("VALOR_DE_TESTE").IsSuccess.Should().BeTrue();
        FieldError erro = ConferenciaDaRegraDeAplicabilidade.Conferir(
            Quando(("FATO_DE_TESTE", "VALOR_DE_TESTE")), vigente: null, Catalogo(fato)).Single();

        erro.Field.Should().Be("regraDeAplicabilidade[0][0].valor");
        erro.Error.Code.Should().Be(ModalidadeErrorCodes.RegraCitaValorDesativado);
        erro.Error.Message.Should().Be("O valor do fato está desativado e não aceita citação nova.");
        erro.Field.Should().NotContain("VALOR_DE_TESTE");
    }

    [Theory(DisplayName = "Só fato conhecido até a inscrição decide a modalidade; fase sem precedência até ela é recusada")]
    [InlineData("INSCRICAO", true)]
    [InlineData("SOLICITACAO_ISENCAO", true)]
    [InlineData("RESULTADO_FINAL", false)]
    [InlineData("HETEROIDENTIFICACAO", false)]
    public void Conferir_PontoResolucao(string ponto, bool aceita)
    {
        FatoCandidato aprovado = Declarado("APROVADO_NA_BANCA", ponto: ponto);
        PrecedenciaFase[] precedencias =
        [
            PrecedenciaFase.Criar("SOLICITACAO_ISENCAO", "INSCRICAO", permiteSobreposicao: false, []).Value!,
            PrecedenciaFase.Criar("INSCRICAO", "RESULTADO_FINAL", permiteSobreposicao: false, []).Value!,
        ];

        IReadOnlyList<FieldError> erros = ConferenciaDaRegraDeAplicabilidade.Conferir(
            Quando(("APROVADO_NA_BANCA", true)), vigente: null, new CatalogoDeFatos([aprovado], precedencias));

        Codigos(erros).Should().Equal(aceita ? [] : [ModalidadeErrorCodes.RegraCitaFatoPosteriorAInscricao]);
    }

    [Fact(DisplayName = "Operador incompatível com o domínio do fato é recusado")]
    public void Conferir_OperadorIncompativel_Recusa()
    {
        PredicadoDnf regra = Predicado(CondicaoDnf.Criar(
            "PCD", Operador.MaiorIgual, JsonSerializer.SerializeToElement(1)).Value!);

        Codigos(ConferenciaDaRegraDeAplicabilidade.Conferir(regra, vigente: null, Catalogo(Declarado("PCD"))))
            .Should().Equal(PredicadoDnfErrorCodes.OperadorIncompativelComDominio);
    }

    [Fact(DisplayName = "Regra que cita a própria modalidade é recusada")]
    public void Conferir_CitaAModalidade_Recusa()
    {
        FatoCandidato modalidade = Modalidade();

        Codigos(ConferenciaDaRegraDeAplicabilidade.Conferir(
                Quando((ModalidadeDoCandidato.Fato, "AC")), vigente: null, Catalogo(modalidade)))
            .Should().Equal(ModalidadeErrorCodes.RegraDependeDaModalidade);
    }

    [Fact(DisplayName = "Regra que cita derivado cujas regras padrão chegam à modalidade é recusada")]
    public void Conferir_CitaDerivadoDaModalidade_Recusa()
    {
        FatoCandidato modalidade = Modalidade();
        FatoCandidato concorreNaAmpla = Derivado("CONCORRE_NA_AMPLA");
        CatalogoDeFatos catalogo = Catalogo(modalidade, concorreNaAmpla);
        concorreNaAmpla.DefinirRegrasPadrao(
            [RegraDerivacao.CriarBooleana(Quando((ModalidadeDoCandidato.Fato, "AC")))], catalogo).IsSuccess.Should().BeTrue();

        Codigos(ConferenciaDaRegraDeAplicabilidade.Conferir(Quando(("CONCORRE_NA_AMPLA", true)), vigente: null, catalogo))
            .Should().Equal(ModalidadeErrorCodes.RegraDependeDaModalidade);
    }

    [Fact(DisplayName = "As recusas de condições distintas se acumulam no campo da regra")]
    public void Conferir_RecusasAcumuladas()
    {
        FatoCandidato pcd = Declarado("PCD");
        pcd.Desativar();
        FatoCandidato modalidade = Modalidade();

        IReadOnlyList<FieldError> erros = ConferenciaDaRegraDeAplicabilidade.Conferir(
            Quando(("PCD", true), (ModalidadeDoCandidato.Fato, "AC")), vigente: null, Catalogo(pcd, modalidade));

        erros.Select(static e => e.Field).Should().Equal("regraDeAplicabilidade[0][0].fato", "regraDeAplicabilidade");
        Codigos(erros).Should().BeEquivalentTo(
            [ModalidadeErrorCodes.RegraCitaFatoDesativado, ModalidadeErrorCodes.RegraDependeDaModalidade]);
    }

    [Fact]
    public void Conferir_FatoRepetidoEmDuasClausulas_RecusaCadaPosicao()
    {
        FatoCandidato fato = Declarado("FATO_DE_TESTE");
        fato.Desativar();
        CondicaoDnf condicao = CondicaoDnf.Criar("FATO_DE_TESTE", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;
        PredicadoDnf regra = PredicadoDnf.CriarDeCondicoesAgrupadas([(0, condicao), (1, condicao), (1, condicao)]).Value!;
        IReadOnlyList<FieldError> erros = ConferenciaDaRegraDeAplicabilidade.Conferir(regra, null, Catalogo(fato));
        erros.Select(static e => e.Field).Should().Equal("regraDeAplicabilidade[0][0].fato", "regraDeAplicabilidade[1][0].fato", "regraDeAplicabilidade[1][1].fato");
        erros.Should().OnlyContain(static e => e.Error.Code == ModalidadeErrorCodes.RegraCitaFatoDesativado);
        ConferenciaDaRegraDeAplicabilidade.Conferir(regra, Quando(("FATO_DE_TESTE", true)), Catalogo(fato)).Should().BeEmpty();
    }

    [Fact]
    public void Conferir_ValorRepetidoEmDuasCondicoes_RecusaCadaPosicao()
    {
        FatoCandidato fato = Declarado("FATO_DE_TESTE", TipoDoDado.Opcao);
        fato.AdicionarValorDominio("VALOR_DE_TESTE", "Valor", 0, ativo: true);
        fato.DesativarValor("VALOR_DE_TESTE");
        PredicadoDnf regra = Quando(("FATO_DE_TESTE", "VALOR_DE_TESTE"), ("FATO_DE_TESTE", "VALOR_DE_TESTE"));
        IReadOnlyList<FieldError> erros = ConferenciaDaRegraDeAplicabilidade.Conferir(regra, null, Catalogo(fato));
        erros.Select(static e => e.Field).Should().Equal("regraDeAplicabilidade[0][0].valor", "regraDeAplicabilidade[0][1].valor");
        erros.Should().OnlyContain(static e => e.Error.Code == ModalidadeErrorCodes.RegraCitaValorDesativado);
        erros.Should().AllSatisfy(static e => { e.Field.Should().NotContain("VALOR_DE_TESTE"); e.Error.Message.Should().NotContain("VALOR_DE_TESTE"); });
        ConferenciaDaRegraDeAplicabilidade.Conferir(regra, Quando(("FATO_DE_TESTE", "VALOR_DE_TESTE")), Catalogo(fato)).Should().BeEmpty();
    }

    [Fact]
    public void Conferir_RecusasDeFasesDistintas_PreservaOrdem()
    {
        FatoCandidato fato = Declarado("FATO", ponto: "RESULTADO_FINAL");
        fato.Desativar();
        FatoCandidato opcao = Declarado("OPCAO", TipoDoDado.Opcao);
        opcao.AdicionarValorDominio("INATIVO", "Inativo", 0, ativo: true);
        opcao.DesativarValor("INATIVO");
        PredicadoDnf regra = Quando(("FATO", true), ("DESCONHECIDO", true), ("OPCAO", "INATIVO"), (ModalidadeDoCandidato.Fato, "AC"));
        IReadOnlyList<FieldError> erros = ConferenciaDaRegraDeAplicabilidade.Conferir(regra, null, Catalogo(fato, opcao, Modalidade()));
        Codigos(erros).Should().Equal(ModalidadeErrorCodes.RegraCitaFatoDesativado, ModalidadeErrorCodes.RegraCitaFatoPosteriorAInscricao,
            "PredicadoDnf.FatoDesconhecido", ModalidadeErrorCodes.RegraCitaValorDesativado, ModalidadeErrorCodes.RegraDependeDaModalidade);
        erros.Select(static e => e.Field).Should().Equal("regraDeAplicabilidade[0][0].fato", "regraDeAplicabilidade[0][0].fato",
            "regraDeAplicabilidade[0][1].fato", "regraDeAplicabilidade[0][2].valor", "regraDeAplicabilidade");
    }

    [Fact]
    public void Conferir_ListaComValoresDesativados_RecusaUmaVez()
    {
        FatoCandidato fato = Declarado("FATO_DE_TESTE", TipoDoDado.Opcao);
        fato.AdicionarValorDominio("B", "B", 0, ativo: true);
        fato.AdicionarValorDominio("A", "A", 1, ativo: true);
        fato.DesativarValor("A");
        fato.DesativarValor("B");
        CondicaoDnf condicao = CondicaoDnf.Criar("FATO_DE_TESTE", Operador.AlgumDe, JsonSerializer.SerializeToElement(new[] { "B", "A", "B" })).Value!;
        FieldError erro = ConferenciaDaRegraDeAplicabilidade.Conferir(Predicado(condicao), null, Catalogo(fato)).Single();
        erro.Field.Should().Be("regraDeAplicabilidade[0][0].valor");
        erro.Error.Code.Should().Be(ModalidadeErrorCodes.RegraCitaValorDesativado);
        erro.Error.Message.Should().Be("O valor do fato está desativado e não aceita citação nova.");
        erro.Origem.Should().Be("FATO_DE_TESTE.A,B");
    }

    [Theory]
    [InlineData("DESCONHECIDO", Operador.Igual, true, "PredicadoDnf.FatoDesconhecido", "fato")]
    [InlineData("PCD", Operador.MaiorIgual, 1, PredicadoDnfErrorCodes.OperadorIncompativelComDominio, "operador")]
    [InlineData("PCD", Operador.Igual, "VALOR_DE_TESTE", PredicadoDnfErrorCodes.ValorIncompativelComTipo, "valor")]
    public void Conferir_PredicadoInvalido_ApontaCondicao(string fato, Operador operador, object valor, string codigo, string campo)
    {
        CondicaoDnf valida = CondicaoDnf.Criar("PCD", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;
        CondicaoDnf invalida = CondicaoDnf.Criar(fato, operador, JsonSerializer.SerializeToElement(valor)).Value!;
        PredicadoDnf regra = PredicadoDnf.CriarDeCondicoesAgrupadas([(0, valida), (1, valida), (1, invalida)]).Value!;
        FieldError erro = ConferenciaDaRegraDeAplicabilidade.Conferir(regra, null, Catalogo(Declarado("PCD"))).Single();
        erro.Field.Should().Be($"regraDeAplicabilidade[1][1].{campo}");
        erro.Error.Code.Should().Be(codigo);
        string mensagem = codigo switch
        {
            "PredicadoDnf.FatoDesconhecido" => "O fato 'DESCONHECIDO' não pertence ao vocabulário fechado.",
            PredicadoDnfErrorCodes.OperadorIncompativelComDominio => "O operador MAIOR_IGUAL não é compatível com o fato 'PCD', de domínio booleano. Os operadores admitidos são IGUAL, DIFERENTE.",
            _ => "O valor da condição sobre 'PCD' deve ser um booleano JSON.",
        };
        erro.Error.Message.Should().Be(mensagem);
    }

    [Fact]
    public void Conferir_ValorForaDoDominio_ApontaValor()
    {
        FatoCandidato fato = Declarado("FATO_DE_TESTE", TipoDoDado.Opcao);
        fato.AdicionarValorDominio("ATIVO", "Ativo", 0, ativo: true);
        FieldError erro = ConferenciaDaRegraDeAplicabilidade.Conferir(Quando(("FATO_DE_TESTE", "OUTRO")), null, Catalogo(fato)).Single();
        erro.Field.Should().Be("regraDeAplicabilidade[0][0].valor");
        erro.Error.Code.Should().Be(PredicadoDnfErrorCodes.ValorForaDoDominio);
        erro.Error.Message.Should().Be("O valor 'OUTRO' da condição sobre 'FATO_DE_TESTE' não pertence ao domínio declarado.");
    }

    private static string[] Codigos(IReadOnlyList<FieldError> erros) => [.. erros.Select(static e => e.Error.Code)];

    private static FatoCandidato Modalidade()
    {
        FatoCandidato modalidade = Derivado(ModalidadeDoCandidato.Fato, TipoDoDado.Opcao);
        modalidade.AdicionarValorDominio("AC", "Ampla concorrência", 0, ativo: true);
        return modalidade;
    }

    private static FatoCandidato Derivado(string codigo, TipoDoDado dominio = TipoDoDado.SimOuNao) =>
        CriacaoDoFato.CriarDerivadoDoAdministrador(
            new DescritivoDoFato(codigo, codigo, "Descrição do fato."),
            new DefinicaoDoFato(dominio, null, null, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Sensivel, Finalidade, Hipotese)).Value!;

    private static FatoCandidato Declarado(
        string codigo,
        TipoDoDado dominio = TipoDoDado.SimOuNao,
        string ponto = "INSCRICAO",
        FormatoTexto? formato = null) =>
        CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato(codigo, codigo, "Descrição do fato."),
            new DefinicaoDoFato(dominio, QuantidadeDeValores.ValorUnico, dominio == TipoDoDado.Opcao ? FonteValoresFato.Catalogo : null, formato, ponto, EscopoFato.Candidato, ClassificacaoDeProtecao.Sensivel, Finalidade, Hipotese)).Value!;

    private static CatalogoDeFatos Catalogo(params FatoCandidato[] fatos) => new(fatos, []);

    private static PredicadoDnf Quando(params (string Fato, object Valor)[] condicoes) =>
        Predicado([.. condicoes.Select(static c =>
            CondicaoDnf.Criar(c.Fato, Operador.Igual, JsonSerializer.SerializeToElement(c.Valor)).Value!)]);

    private static PredicadoDnf Predicado(params CondicaoDnf[] condicoes) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([.. condicoes.Select(static c => (0, c))]).Value!;
}
