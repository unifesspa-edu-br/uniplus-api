namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;


using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
            EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA").ComRotulo("Baixa renda").ComObrigatoriedade("QUANDO", [[Condicao("FATO_INEXISTENTE", "IGUAL", true)]]).Construir(),
        ]);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("itens[0].regras.obrigatoriedade.predicado");
    }

    [Theory(DisplayName = "Data e endereço são coletados no campo do domínio deles")]
    [InlineData("DATA_NASCIMENTO", "DATA")]
    [InlineData("ENDERECO_RESIDENCIAL", "ENDERECO")]
    public async Task Handle_DataEEndereco_ColetaNoCampoDoDominio(string fatoCodigo, string tipoRenderizacao)
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result resultado = await HandleAsync(mocks, new DefinirFatosColetadosCommand(
            processo.Id, FinalidadeFormulario.Habilitacao, [EntradaDoFatoColetadoDeTeste.Coletando(fatoCodigo).ComRotulo("Rótulo").DoTipo(tipoRenderizacao).Construir()]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.FatosColetados.Single().TipoRenderizacao.Should().Be(CatalogoDe<TipoRenderizacao>.Obter(tipoRenderizacao));
    }

    [Fact(DisplayName = "Fato de texto multivalorado não é coletável como campo de texto")]
    public async Task Handle_TextoMultivalorado_RecusaPelaCoerencia()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com(
            [.. VocabularioSeed().Select(static f => f.Codigo == "NOME_SOCIAL" ? f with { Cardinalidade = CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.VariosValores) } : f)]));
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Habilitacao,
            [EntradaDoFatoColetadoDeTeste.Coletando("NOME_SOCIAL").ComRotulo("Nome social").DoTipo("TEXTO").ComObrigatoriedade("NUNCA").Construir()]);

        (await HandleAsync(mocks, command)).Error!.Code.Should().Be("ItemFormulario.TipoRenderizacaoIncoerenteComDominio");
    }

    [Fact(DisplayName = "Campo de texto é aceito e congela o formato do fato no catálogo")]
    public async Task Handle_CampoDeTexto_CongelaOFormatoDoCatalogo()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Result resultado = await DefinirNaHabilitacaoAsync(
            processo,
            [EntradaDoFatoColetadoDeTeste.Coletando("NOME_SOCIAL").ComRotulo("Nome social").DoTipo("TEXTO").ComObrigatoriedade("NUNCA").Construir()]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.FatosColetados.Single().Formato.Should().Be("NOME_PESSOA");
    }

    [Fact(DisplayName = "CA-02 — congela a classificação de proteção de dados do fato no catálogo")]
    public async Task Handle_CampoDeTexto_CongelaAClassificacaoDoCatalogo()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Result resultado = await DefinirNaHabilitacaoAsync(
            processo,
            [EntradaDoFatoColetadoDeTeste.Coletando("CPF").DoTipo("TEXTO").ComObrigatoriedade("NUNCA").Construir()]);

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
            EntradaDoFatoColetadoDeTeste.Coletando("COR_RACA", -1).ComRotulo("Cor ou raça").DoTipo("ESCOLHA_UNICA").ComObrigatoriedade("NUNCA").Construir(),
            EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA", 1).ComRotulo("").ComObrigatoriedade("NUNCA").Construir(),
        ]);

        Result resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Field).Should().BeEquivalentTo(["itens[0].ordem", "itens[1].apresentacao.rotulo"]);
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FatoCodigo vazio recusa com FatoCodigoObrigatorio, não FatoDesconhecido: o item de forma inválida não segue para o catálogo")]
    public async Task Handle_FatoCodigoVazio_RecusaComErroDeFormaNaoDeVocabulario()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [EntradaDoFatoColetadoDeTeste.Coletando("").ComRotulo("Rótulo").DoTipo("ESCOLHA_UNICA").ComObrigatoriedade("NUNCA").Construir()]);

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal("ItemFormulario.FatoCodigoObrigatorio");
    }

    [Fact(DisplayName = "ADR-0125: a forma inválida de um item e o fato desconhecido de outro saem juntos, cada um no seu campo")]
    public async Task Handle_FormaInvalidaEmUmFatoEVocabularioDesconhecidoNoOutro_AcumulaAsDuas()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [
            EntradaDoFatoColetadoDeTeste.Coletando("COR_RACA", -1).ComRotulo("Cor ou raça").DoTipo("ESCOLHA_UNICA").ComObrigatoriedade("NUNCA").Construir(),
            EntradaDoFatoColetadoDeTeste.Coletando("FATO_INEXISTENTE", 1).ComRotulo("Rótulo").DoTipo("ESCOLHA_UNICA").ComObrigatoriedade("NUNCA").Construir(),
        ]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("itens[0].ordem", "ItemFormulario.OrdemInvalida"),
            ("itens[1].fatoCodigo", "ItemFormulario.FatoDesconhecido"),
        ]);
    }

    [Fact(DisplayName = "TipoRenderizacao coerente para fato de opção com vários valores (MODALIDADE) — mesmo não-coletável, a coerência é checada antes")]
    public async Task Handle_TipoRenderizacaoCoerenteComCategoricoMultivalorado_NaoFalhaPorCoerencia()
    {
        // MODALIDADE é OPCAO com VARIOS_VALORES no vocabulário-seed, mas é CALCULADO — a
        // coletabilidade recusa antes da coerência de renderização ser sequer checada. Este
        // teste prova que o erro devolvido continua sendo o de coletabilidade, não o de
        // coerência, quando o TipoRenderizacao informado é o coerente com o domínio.
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [EntradaDoFatoColetadoDeTeste.Coletando("MODALIDADE").ComRotulo("Modalidade").DoTipo("ESCOLHA_MULTIPLA").ComObrigatoriedade("NUNCA").Construir()]);

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
            EntradaDoFatoColetadoDeTeste.Coletando("NOME_SOCIAL").ComRotulo("Nome social").DoTipo("TEXTO").ComObrigatoriedade("NUNCA").ComRestricoes([new RestricaoValorInput(tipo, (decimal?)minimo, (decimal?)maximo)]).Construir(),
        ]);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "itens[0].regras.restricoes[0]",
            Error = new { Code = codigo },
        });
    }

    private static FatoColetadoInput DataComLimitesInvertidos() =>
        EntradaDoFatoColetadoDeTeste.Coletando("DATA_NASCIMENTO").ComRotulo("Data de nascimento").DoTipo("DATA")
            .ComRestricoes(new RestricaoValorInput(
                "FAIXA_DE_DATA",
                DataMinima: new LimiteDeDataInput("DATA_FIXA", Data: new DateOnly(2011, 1, 1)),
                DataMaxima: new LimiteDeDataInput("DATA_FIXA", Data: new DateOnly(2010, 12, 31))))
            .Construir();

    private static FatoColetadoInput DataDeNascimentoComFaixaForaDoCampo() =>
        EntradaDoFatoColetadoDeTeste.Coletando("NOME_SOCIAL", 1).ComRotulo("Nome social").DoTipo("TEXTO").ComObrigatoriedade("NUNCA")
            .ComRestricoes(new RestricaoValorInput("FAIXA_DE_DATA", DataMaxima: new LimiteDeDataInput("ENVIO", Anos: 0, Dias: 0)))
            .Construir();

    [Fact(DisplayName = "Faixa de data com a mínima depois da máxima e faixa de data fora do campo de data são recusadas juntas")]
    public async Task Handle_FaixaDeDataInvalida_RecusaCadaUmaNoSeuCampo()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(ProcessoEmRascunho(), [DataComLimitesInvertidos(), DataDeNascimentoComFaixaForaDoCampo()]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("itens[0].regras.restricoes[0]", "RestricaoValor.LimitesIncoerentes"),
            ("itens[1].regras.restricoes[0]", "ItemFormulario.RestricaoIncoerente"),
        ]);
    }

    [Fact(DisplayName = "Opção permitida fora do domínio do próprio fato é recusada no grupo dela")]
    public async Task Handle_OpcaoForaDoDominio_Recusa()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [
            EntradaDoFatoColetadoDeTeste.Coletando("COR_RACA").ComRotulo("Cor ou raça").DoTipo("ESCOLHA_UNICA").ComRestricoes([new RestricaoValorInput("OPCOES_PERMITIDAS", Entradas: [new OpcoesCondicionadasInput(null, ["PRETA", "ROXA"])])]).Construir(),
        ]);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "itens[0].regras.restricoes[0].entradas[0].valores",
            Error = new { Code = "PredicadoDnf.ValorForaDoDominio" },
        });
    }

    [Fact(DisplayName = "Opções permitidas em campo que não é de seleção são recusadas pela coerência com o tipo do campo")]
    public async Task Handle_OpcoesEmCampoBooleano_RecusaPelaCoerencia()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [
            EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA").ComRotulo("Baixa renda").ComObrigatoriedade("NUNCA").ComRestricoes([new RestricaoValorInput("OPCOES_PERMITIDAS", Entradas: [new OpcoesCondicionadasInput(null, ["SIM"])])]).Construir(),
        ]);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "itens[0].regras.restricoes[0]",
            Error = new { Code = ItemFormularioErrorCodes.RestricaoIncoerente },
        });
    }

    [Fact(DisplayName = "ADR-0125: recusa semântica da restrição e recusa do próprio item saem no mesmo lote")]
    public async Task Handle_RecusaSemanticaERecusaDoItem_Acumulam()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [
            EntradaDoFatoColetadoDeTeste.Coletando("COR_RACA").ComRotulo("Cor ou raça").DoTipo("ESCOLHA_UNICA").ComRestricoes([
                    new RestricaoValorInput("OPCOES_PERMITIDAS", Entradas: [new OpcoesCondicionadasInput(null, ["ROXA"])]),
                    new RestricaoValorInput("OPCOES_PERMITIDAS", Entradas: [new OpcoesCondicionadasInput(null, ["PRETA"])]),
                ]).Construir(),
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
            EntradaDoFatoColetadoDeTeste.Coletando("COR_RACA").ComRotulo("Cor ou raça").DoTipo("ESCOLHA_UNICA").ComRestricoes([new RestricaoValorInput("OPCOES_PERMITIDAS", Entradas: [new OpcoesCondicionadasInput(null, ["AMARELA"])])]).Construir(),
        ]);

        (await HandleAsync(mocks, command)).Error!.Code.Should().Be(VinculoCatalogoErrorCodes.ValorDesativado);
    }

    private static FatoColetadoInput[] OpcoesDasRespostasDe(string fonte) =>
    [
        EntradaDoFatoColetadoDeTeste.Coletando(fonte).ComRotulo("Campo de origem").DoTipo("ESCOLHA_UNICA").Construir(),
        EntradaDoFatoColetadoDeTeste.Coletando("OPCAO_LISTA_ESPERA", 1).ComRotulo("Lista de espera").DoTipo("ESCOLHA_UNICA").ComObrigatoriedade("NUNCA").ComRestricoes([new RestricaoValorInput("OPCOES_DAS_RESPOSTAS", Fatos: [fonte])]).Construir(),
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
            ("itens[1].regras.restricoes[0].fatos", ItemFormularioErrorCodes.OpcoesDeOutroDominio));
    }

    [Fact(DisplayName = "Regras ausentes são recusadas pela obrigatoriedade inválida, no caminho das regras")]
    public async Task Handle_RegrasAusentes_RecusaAObrigatoriedadeNoCaminhoDasRegras()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(), [EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA").SemRegras().Construir()]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("itens[0].regras.obrigatoriedade", "FatoColetado.ObrigatoriedadeInvalida"));
    }

    [Fact(DisplayName = "Obrigatoriedade ausente dentro das regras é recusada no caminho dela")]
    public async Task Handle_ObrigatoriedadeAusente_RecusaNoCaminhoDaObrigatoriedade()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(), [EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA").ComObrigatoriedade(null).Construir()]);

        resultado.Errors.Select(static e => e.Field).Should().Equal("itens[0].regras.obrigatoriedade");
    }

    [Fact(DisplayName = "A cláusula sem condição do predicado da obrigatoriedade é recusada na posição dela")]
    public async Task Handle_PredicadoDaObrigatoriedadeSemCondicao_RecusaNaPosicaoDaClausula()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(), [EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA").ComObrigatoriedade("QUANDO", [[]]).Construir()]);

        resultado.Errors.Select(static e => e.Field).Should().Equal("itens[0].regras.obrigatoriedade.predicado[0]");
    }
}
