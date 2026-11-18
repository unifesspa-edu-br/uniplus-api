namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;


using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDaColetaDeFatos;

/// <summary>
/// A forma dos itens que o handler de coleta de fatos aceita ou recusa: o predicado da obrigatoriedade, o tipo de campo, a classificação
/// congelada, as restrições, as faixas de data e as opções permitidas.
/// </summary>
public sealed class FormaDosItensDaColetaDeFatosTests
{
    [Fact(DisplayName = "Predicado da obrigatoriedade que cita fato fora do vocabulário é recusado no campo dele")]
    public async Task Handle_PredicadoDaObrigatoriedadeComFatoDesconhecido_Recusa()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [
            new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "QUANDO", null,
                PredicadoObrigatoriedade: [[Condicao("FATO_INEXISTENTE", "IGUAL", true)]]),
        ]);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("itens[0].predicadoObrigatoriedade");
    }

    [Theory(DisplayName = "Data e endereço são coletados no campo do domínio deles")]
    [InlineData("DATA_NASCIMENTO", "DATA")]
    [InlineData("ENDERECO_RESIDENCIAL", "ENDERECO")]
    public async Task Handle_DataEEndereco_ColetaNoCampoDoDominio(string fatoCodigo, string tipoRenderizacao)
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result resultado = await HandleAsync(mocks, new DefinirFatosColetadosCommand(
            processo.Id, FinalidadeFormulario.Habilitacao, [new FatoColetadoInput(fatoCodigo, 0, "Rótulo", tipoRenderizacao, "SEMPRE", null)]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.FatosColetados.Single().TipoRenderizacao.Should().Be(TipoRenderizacaoCodigo.FromCodigo(tipoRenderizacao));
    }

    [Fact(DisplayName = "Fato de texto multivalorado não é coletável como campo de texto")]
    public async Task Handle_TextoMultivalorado_RecusaPelaCoerencia()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com(
            [.. VocabularioSeed().Select(static f => f.Codigo == "NOME_SOCIAL" ? f with { Cardinalidade = "MULTIVALORADO" } : f)]));
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Habilitacao,
            [new FatoColetadoInput("NOME_SOCIAL", 0, "Nome social", "TEXTO", "NUNCA", null)]);

        (await HandleAsync(mocks, command)).Error!.Code.Should().Be("ItemFormulario.TipoRenderizacaoIncoerenteComDominio");
    }

    [Fact(DisplayName = "Campo de texto é aceito e congela o formato do fato no catálogo")]
    public async Task Handle_CampoDeTexto_CongelaOFormatoDoCatalogo()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Result resultado = await DefinirNaHabilitacaoAsync(
            processo,
            [new FatoColetadoInput("NOME_SOCIAL", 0, "Nome social", "TEXTO", "NUNCA", null)]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.FatosColetados.Single().Formato.Should().Be("NOME_PESSOA");
    }

    [Fact(DisplayName = "CA-02 — congela a classificação de proteção de dados do fato no catálogo")]
    public async Task Handle_CampoDeTexto_CongelaAClassificacaoDoCatalogo()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Result resultado = await DefinirNaHabilitacaoAsync(
            processo,
            [new FatoColetadoInput("CPF", 0, "CPF", "TEXTO", "NUNCA", null)]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.FatosColetados.Single().ClassificacaoProtecao.Should().Be("IDENTIFICADOR");
    }

    [Fact(DisplayName = "ADR-0125: violações de forma de FatoColetado.Criar acumulam entre fatos, com o índice prefixado ao field")]
    public async Task Handle_DoisFatosComViolacaoDeForma_AcumulaComIndicePrefixadoAoField()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);

        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Habilitacao,
        [
            new FatoColetadoInput("COR_RACA", -1, "Cor ou raça", "SELECAO_UNICA", "NUNCA", null),
            new FatoColetadoInput("BAIXA_RENDA", 1, "", "BOOLEANO", "NUNCA", null),
        ]);

        Result resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Field).Should().BeEquivalentTo(["itens[0].ordem", "itens[1].rotulo"]);
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FatoCodigo vazio recusa com FatoCodigoObrigatorio, não FatoDesconhecido: o item de forma inválida não segue para o catálogo")]
    public async Task Handle_FatoCodigoVazio_RecusaComErroDeFormaNaoDeVocabulario()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [new FatoColetadoInput("", 0, "Rótulo", "SELECAO_UNICA", "NUNCA", null)]);

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal("ItemFormulario.FatoCodigoObrigatorio");
    }

    [Fact(DisplayName = "ADR-0125: a forma inválida de um item e o fato desconhecido de outro saem juntos, cada um no seu campo")]
    public async Task Handle_FormaInvalidaEmUmFatoEVocabularioDesconhecidoNoOutro_AcumulaAsDuas()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [
            new FatoColetadoInput("COR_RACA", -1, "Cor ou raça", "SELECAO_UNICA", "NUNCA", null),
            new FatoColetadoInput("FATO_INEXISTENTE", 1, "Rótulo", "SELECAO_UNICA", "NUNCA", null),
        ]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("itens[0].ordem", "ItemFormulario.OrdemInvalida"),
            ("itens[1].fatoCodigo", "ItemFormulario.FatoDesconhecido"),
        ]);
    }

    [Fact(DisplayName = "TipoRenderizacao coerente para fato CATEGORICO multivalorado (MODALIDADE) — mesmo não-coletável, a coerência é checada antes")]
    public async Task Handle_TipoRenderizacaoCoerenteComCategoricoMultivalorado_NaoFalhaPorCoerencia()
    {
        // MODALIDADE é CATEGORICO/MULTIVALORADO no vocabulário-seed, mas é DERIVADO — a
        // coletabilidade recusa antes da coerência de renderização ser sequer checada. Este
        // teste prova que o erro devolvido continua sendo o de coletabilidade, não o de
        // coerência, quando o TipoRenderizacao informado é o coerente com o domínio.
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [new FatoColetadoInput("MODALIDADE", 0, "Modalidade", "SELECAO_MULTIPLA", "NUNCA", null)]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ItemFormulario.FatoNaoColetavel");
    }

    [Theory(DisplayName = "Restrição de tipo desconhecido ou de limites incoerentes é recusada no campo dela")]
    [InlineData("REGEX", null, null, "RestricaoValor.TipoDesconhecido")]
    [InlineData("TAMANHO_TEXTO", 1.5, null, "RestricaoValor.LimitesIncoerentes")]
    [InlineData("TAMANHO_TEXTO", 10.0, 2.0, "RestricaoValor.LimitesIncoerentes")]
    public async Task Handle_RestricaoDeFormaInvalida_Recusa(string tipo, double? minimo, double? maximo, string codigo)
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [
            new FatoColetadoInput("NOME_SOCIAL", 0, "Nome social", "TEXTO", "NUNCA", null,
                Restricoes: [new RestricaoValorInput(tipo, (decimal?)minimo, (decimal?)maximo)]),
        ]);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "itens[0].restricoes[0]",
            Error = new { Code = codigo },
        });
    }

    private static FatoColetadoInput DataComLimitesInvertidos() => new(
        "DATA_NASCIMENTO", 0, "Data de nascimento", "DATA", "SEMPRE", null,
        Restricoes:
        [
            new RestricaoValorInput(
                "FAIXA_DE_DATA",
                DataMinima: new LimiteDeDataInput("DATA_FIXA", Data: new DateOnly(2011, 1, 1)),
                DataMaxima: new LimiteDeDataInput("DATA_FIXA", Data: new DateOnly(2010, 12, 31))),
        ]);

    private static FatoColetadoInput DataDeNascimentoComFaixaForaDoCampo() => new(
        "NOME_SOCIAL", 1, "Nome social", "TEXTO", "NUNCA", null,
        Restricoes: [new RestricaoValorInput("FAIXA_DE_DATA", DataMaxima: new LimiteDeDataInput("ENVIO", Anos: 0, Dias: 0))]);

    [Fact(DisplayName = "Faixa de data com a mínima depois da máxima e faixa de data fora do campo de data são recusadas juntas")]
    public async Task Handle_FaixaDeDataInvalida_RecusaCadaUmaNoSeuCampo()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(ProcessoEmRascunho(), [DataComLimitesInvertidos(), DataDeNascimentoComFaixaForaDoCampo()]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("itens[0].restricoes[0]", "RestricaoValor.LimitesIncoerentes"),
            ("itens[1].restricoes[0]", "ItemFormulario.RestricaoIncoerente"),
        ]);
    }

    [Fact(DisplayName = "Opção permitida fora do domínio do próprio fato é recusada no grupo dela")]
    public async Task Handle_OpcaoForaDoDominio_Recusa()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [
            new FatoColetadoInput("COR_RACA", 0, "Cor ou raça", "SELECAO_UNICA", "SEMPRE", null,
                Restricoes: [new RestricaoValorInput("OPCOES_PERMITIDAS", Entradas: [new OpcoesCondicionadasInput(null, ["PRETA", "ROXA"])])]),
        ]);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "itens[0].restricoes[0].entradas[0].valores",
            Error = new { Code = "PredicadoDnf.ValorForaDoDominio" },
        });
    }

    [Fact(DisplayName = "Opções permitidas em campo que não é de seleção são recusadas pela coerência com o tipo do campo")]
    public async Task Handle_OpcoesEmCampoBooleano_RecusaPelaCoerencia()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [
            new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "NUNCA", null,
                Restricoes: [new RestricaoValorInput("OPCOES_PERMITIDAS", Entradas: [new OpcoesCondicionadasInput(null, ["SIM"])])]),
        ]);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "itens[0].restricoes[0]",
            Error = new { Code = ItemFormularioErrorCodes.RestricaoIncoerente },
        });
    }

    [Fact(DisplayName = "ADR-0125: recusa semântica da restrição e recusa do próprio item saem no mesmo lote")]
    public async Task Handle_RecusaSemanticaERecusaDoItem_Acumulam()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [
            new FatoColetadoInput("COR_RACA", 0, "Cor ou raça", "SELECAO_UNICA", "SEMPRE", null,
                Restricoes:
                [
                    new RestricaoValorInput("OPCOES_PERMITIDAS", Entradas: [new OpcoesCondicionadasInput(null, ["ROXA"])]),
                    new RestricaoValorInput("OPCOES_PERMITIDAS", Entradas: [new OpcoesCondicionadasInput(null, ["PRETA"])]),
                ]),
        ]);

        resultado.Errors.Select(static e => e.Error.Code).Should().BeEquivalentTo(
            ["PredicadoDnf.ValorForaDoDominio", "RestricaoValor.TipoRepetido"]);
    }

    [Fact(DisplayName = "Opção permitida desativada no catálogo é recusada como vínculo novo")]
    public async Task Handle_OpcaoDesativada_RecusaVinculoNovo()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com(
            [.. VocabularioSeed().Select(static f => f.Codigo == "COR_RACA"
                ? f with { ValoresDominioDeclarados = [new FatoValorDominioViewItem("AMARELA", null, 0, Ativo: false)] }
                : f)]));
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Habilitacao,
        [
            new FatoColetadoInput("COR_RACA", 0, "Cor ou raça", "SELECAO_UNICA", "SEMPRE", null,
                Restricoes: [new RestricaoValorInput("OPCOES_PERMITIDAS", Entradas: [new OpcoesCondicionadasInput(null, ["AMARELA"])])]),
        ]);

        (await HandleAsync(mocks, command)).Error!.Code.Should().Be(VinculoCatalogoErrorCodes.ValorDesativado);
    }

    private static FatoColetadoInput[] OpcoesDasRespostasDe(string fonte) =>
    [
        new FatoColetadoInput(fonte, 0, "Campo de origem", "SELECAO_UNICA", "SEMPRE", null),
        new FatoColetadoInput("OPCAO_LISTA_ESPERA", 1, "Lista de espera", "SELECAO_UNICA", "NUNCA", null,
            Restricoes: [new RestricaoValorInput("OPCOES_DAS_RESPOSTAS", Fatos: [fonte])]),
    ];

    [Theory(DisplayName = "Opções formadas pelas respostas de campo cujas opções são todas opções do campo são aceitas, e o campo de origem fica citado")]
    [InlineData("OPCAO_CURSO_1")]
    [InlineData("OPCAO_CURSO_2")]
    public async Task Handle_OpcoesDasRespostas_AceitaOpcoesContidasNoAlvo(string fonte)
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();

        Result resultado = await DefinirNaHabilitacaoAsync(processo, OpcoesDasRespostasDe(fonte));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.FatosColetados.Single(static f => f.FatoCodigo == "OPCAO_LISTA_ESPERA").FatosCitados.Should().Equal(fonte);
    }

    [Fact(DisplayName = "Opções formadas pelas respostas de campo com opções de outro domínio são recusadas no campo das respostas")]
    public async Task Handle_OpcoesDasRespostas_RecusaOpcoesDeOutroDominio()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(ProcessoEmRascunho(), OpcoesDasRespostasDe("COR_RACA"));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("itens[1].restricoes[0].fatos", ItemFormularioErrorCodes.OpcoesDeOutroDominio));
    }
}
