namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cobertura do <see cref="DefinirFatosColetadosCommandHandler"/> (Story #984): a coletabilidade
/// (só fato declarado com binding de campo de inscrição), a validação semântica das
/// pré-condições contra o vocabulário fechado, e a delegação da estrutura do grafo ao agregado.
/// </summary>
public sealed class DefinirFatosColetadosCommandHandlerTests
{
    private sealed record Mocks(
        IProcessoSeletivoRepository Repository,
        IFatoCandidatoReader FatoCandidatoReader,
        ISelecaoUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);

        Mocks mocks = new(repository, Substitute.For<IFatoCandidatoReader>(), Substitute.For<ISelecaoUnitOfWork>());
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(VocabularioSeed());
        return mocks;
    }

    private static Task<Result<MutacaoAceita>> HandleAsync(Mocks mocks, DefinirFatosColetadosCommand command) =>
        DefinirFatosColetadosCommandHandler.Handle(
            command, mocks.Repository, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

    private static IReadOnlyList<FatoCandidatoView> VocabularioSeed() =>
    [
        new(Guid.CreateVersion7(), "COR_RACA", "Cor ou raça", null, "CATEGORICO", "DECLARADO", "ESCALAR",
            ["BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_INFORMADO"], "INSCRICAO", "CAMPO_INSCRICAO:COR_RACA", null, "GLOBAL", Ativo: true),
        new(Guid.CreateVersion7(), "BAIXA_RENDA", "Baixa renda", null, "BOOLEANO", "DECLARADO", "ESCALAR",
            null, "INSCRICAO", "CAMPO_INSCRICAO:BAIXA_RENDA", null, null, Ativo: true),
        new(Guid.CreateVersion7(), "MODALIDADE", "Modalidade", null, "CATEGORICO", "DERIVADO", "MULTIVALORADO",
            null, "INSCRICAO", "REGRA_DERIVACAO:MODALIDADE", null, "MODALIDADE", Ativo: true),
        new(Guid.CreateVersion7(), "RENDA_PER_CAPITA", "Renda per capita", null, "NUMERICO", "DERIVADO", "ESCALAR",
            null, "INSCRICAO", "ATRIBUTO_CANDIDATO:RENDA_PER_CAPITA", null, null, Ativo: true),
        new(Guid.CreateVersion7(), "NOME_SOCIAL", "Nome social", null, "TEXTO", "DECLARADO", "ESCALAR",
            null, "INSCRICAO", "CAMPO_INSCRICAO:NOME_SOCIAL", null, null, Ativo: true, Formato: "NOME_PESSOA"),
    ];

    private static ProcessoSeletivo ProcessoEmRascunho()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Fatos", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        FormularioDeTeste.GarantirFormulario(processo, FinalidadeFormulario.Inscricao, PrecondicaoIfMatch.Ausente);
        return processo;
    }

    private static CondicaoPrecondicaoInput Condicao(string fato, string operador, object valor) =>
        new(fato, operador, JsonSerializer.SerializeToElement(valor));

    [Fact(DisplayName = "Processo inexistente retorna ProcessoSeletivo.NaoEncontrado sem tocar no reader")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Guid processoId = Guid.CreateVersion7();
        Mocks mocks = NovosMocks(processo: null, processoId);
        DefinirFatosColetadosCommand command = new(processoId, FinalidadeFormulario.Inscricao, [], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Coleta com pré-condição válida é aceita, persistida, e devolve 204 sem ETag em rascunho")]
    public async Task Handle_ColetaValida_DefineEPersiste()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);

        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao,
        [
            new FatoColetadoInput("COR_RACA", 0, "Cor ou raça", "SELECAO_UNICA", "NUNCA", null),
            new FatoColetadoInput("BAIXA_RENDA", 1, "Baixa renda", "BOOLEANO", "NUNCA", [[Condicao("COR_RACA", "IGUAL", "PRETA")]]),
        ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ETag.Should().BeNull("em rascunho não há sessão editorial nem ETag");
        processo.FatosColetados.Select(f => f.FatoCodigo).Should().BeEquivalentTo(["COR_RACA", "BAIXA_RENDA"]);
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Dois itens fora das seções do formulário saem juntos, cada um no seu campo")]
    public async Task Handle_ItensForaDeSecao_AcumulaAsRecusas()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);

        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao,
        [
            new FatoColetadoInput("COR_RACA", 0, "Cor ou raça", "SELECAO_UNICA", "NUNCA", null, EtapaCodigo: "INEXISTENTE"),
            new FatoColetadoInput("BAIXA_RENDA", 1, "Baixa renda", "BOOLEANO", "NUNCA", null, EtapaCodigo: "OUTRA"),
        ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.Errors.Select(static e => e.Field).Should().BeEquivalentTo(["itens[0].etapaCodigo", "itens[1].etapaCodigo"]);
    }

    [Fact(DisplayName = "Coletar fato desativado no catálogo é recusado quando o processo ainda não o coletava; já coletado, continua")]
    public async Task Handle_FatoDesativado_RecusaSoVinculoNovo()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(
            [.. VocabularioSeed().Select(static f => f.Codigo == "BAIXA_RENDA" ? f with { Ativo = false } : f)]);
        DefinirFatosColetadosCommand coletaBaixaRenda = new(
            processo.Id, FinalidadeFormulario.Inscricao, [new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "NUNCA", null)], PrecondicaoIfMatch.Ausente);

        (await HandleAsync(mocks, coletaBaixaRenda)).Error!.Code.Should().Be("ProcessoSeletivo.FatoDesativado");

        processo.DefinirItens(
            [FatoColetado.Criar("BAIXA_RENDA", 0, "Baixa renda", TipoRenderizacao.Booleano, Obrigatoriedade.Nunca, null).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        (await HandleAsync(mocks, coletaBaixaRenda)).IsSuccess.Should().BeTrue("o fato já era coletado pelo processo");
    }

    [Fact(DisplayName = "Coletar um fato derivado (MODALIDADE) é recusado como não coletável")]
    public async Task Handle_FatoDerivado_RetornaNaoColetavel()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao, [new FatoColetadoInput("MODALIDADE", 0, "Modalidade", "SELECAO_MULTIPLA", "NUNCA", null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FatoColetado.FatoNaoColetavel");
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Coletar um fato computado de atributo (RENDA_PER_CAPITA) é recusado como não coletável")]
    public async Task Handle_FatoComputado_RetornaNaoColetavel()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao, [new FatoColetadoInput("RENDA_PER_CAPITA", 0, "Renda per capita", "NUMERO", "NUNCA", null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FatoColetado.FatoNaoColetavel");
    }

    [Fact(DisplayName = "Fato fora do vocabulário é recusado como desconhecido, sem tradução")]
    public async Task Handle_FatoForaDoVocabulario_RetornaDesconhecido()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao, [new FatoColetadoInput("V", 0, "V", "SELECAO_UNICA", "NUNCA", null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FatoColetado.FatoDesconhecido");
    }

    [Fact(DisplayName = "Pré-condição com operador incompatível com o domínio do fato citado é recusada")]
    public async Task Handle_OperadorIncompativel_RetornaErroSemantico()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);

        // COR_RACA é categórico: MAIOR_IGUAL não se aplica.
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao,
        [
            new FatoColetadoInput("COR_RACA", 0, "Cor ou raça", "SELECAO_UNICA", "NUNCA", null),
            new FatoColetadoInput("BAIXA_RENDA", 1, "Baixa renda", "BOOLEANO", "NUNCA", [[Condicao("COR_RACA", "MAIOR_IGUAL", "PRETA")]]),
        ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.OperadorIncompativelComDominio");
    }

    [Fact(DisplayName = "Pré-condição que cita fato posterior na ordem é recusada pela estrutura do grafo (domínio)")]
    public async Task Handle_CitaFatoPosterior_RetornaErroDoDominio()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);

        // BAIXA_RENDA na ordem 0 cita COR_RACA (ordem 1, posterior).
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao,
        [
            new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "NUNCA", [[Condicao("COR_RACA", "IGUAL", "PRETA")]]),
            new FatoColetadoInput("COR_RACA", 1, "Cor ou raça", "SELECAO_UNICA", "NUNCA", null),
        ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FatoColetado.PrecondicaoCitaFatoPosterior");
    }

    [Theory(DisplayName = "TipoRenderizacao incoerente com Domínio/Cardinalidade do fato no catálogo é recusado")]
    [InlineData("COR_RACA", "BOOLEANO", "COR_RACA é CATEGORICO/ESCALAR — só SELECAO_UNICA é coerente")]
    [InlineData("COR_RACA", "NUMERO", "COR_RACA é CATEGORICO/ESCALAR — NUMERO não é coerente com nenhum categórico")]
    [InlineData("COR_RACA", "SELECAO_MULTIPLA", "COR_RACA é ESCALAR, não MULTIVALORADO — só SELECAO_UNICA é coerente")]
    [InlineData("BAIXA_RENDA", "SELECAO_UNICA", "BAIXA_RENDA é BOOLEANO — só BOOLEANO é coerente")]
    [InlineData("BAIXA_RENDA", "NUMERO", "BAIXA_RENDA é BOOLEANO — NUMERO não é coerente")]
    [InlineData("NOME_SOCIAL", "SELECAO_UNICA", "NOME_SOCIAL é TEXTO — só TEXTO é coerente")]
    [InlineData("BAIXA_RENDA", "TEXTO", "BAIXA_RENDA é BOOLEANO — TEXTO não é coerente")]
    public async Task Handle_TipoRenderizacaoIncoerente_RetornaErroDeCoerencia(
        string fatoCodigo, string tipoRenderizacaoIncoerente, string motivo)
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao, [new FatoColetadoInput(fatoCodigo, 0, "Rótulo", tipoRenderizacaoIncoerente, "NUNCA", null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue(motivo);
        resultado.Error!.Code.Should().Be("FatoColetado.TipoRenderizacaoIncoerenteComDominio");
    }

    [Theory(DisplayName = "Obrigatoriedade é SEMPRE ou NUNCA sem predicado, ou QUANDO com ele; o resto é recusado no campo")]
    [InlineData("QUANDO", false)]
    [InlineData("SEMPRE", true)]
    [InlineData("OBRIGATORIO", false)]
    public async Task Handle_ObrigatoriedadeIncoerente_Recusa(string tipo, bool comPredicado)
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao,
        [
            new FatoColetadoInput("COR_RACA", 0, "Cor ou raça", "SELECAO_UNICA", "NUNCA", null),
            new FatoColetadoInput("BAIXA_RENDA", 1, "Baixa renda", "BOOLEANO", tipo, null,
                PredicadoObrigatoriedade: comPredicado ? [[Condicao("COR_RACA", "IGUAL", "PRETA")]] : null),
        ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "itens[1].obrigatoriedade",
            Error = new { Code = FatoColetadoErrorCodes.ObrigatoriedadeInvalida },
        });
    }

    [Fact(DisplayName = "Obrigatoriedade QUANDO sobre campo anterior é aceita e congela o predicado")]
    public async Task Handle_ObrigatoriedadeQuando_Aceita()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao,
        [
            new FatoColetadoInput("COR_RACA", 0, "Cor ou raça", "SELECAO_UNICA", "SEMPRE", null),
            new FatoColetadoInput("BAIXA_RENDA", 1, "Baixa renda", "BOOLEANO", "QUANDO", null,
                PredicadoObrigatoriedade: [[Condicao("COR_RACA", "IGUAL", "PRETA")]], Ajuda: "Renda por pessoa da família", PedirConfirmacao: true),
        ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        FatoColetado baixaRenda = processo.FatosColetados.Single(static f => f.FatoCodigo == "BAIXA_RENDA");
        baixaRenda.Obrigatoriedade.Tipo.Should().Be(TipoObrigatoriedade.Quando);
        baixaRenda.Obrigatoriedade.FatosCitados.Should().Equal("COR_RACA");
        baixaRenda.Ajuda.Should().Be("Renda por pessoa da família");
        baixaRenda.PedirConfirmacao.Should().BeTrue();
    }

    [Fact(DisplayName = "Predicado da obrigatoriedade que cita fato fora do vocabulário é recusado no campo dele")]
    public async Task Handle_PredicadoDaObrigatoriedadeComFatoDesconhecido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao,
        [
            new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "QUANDO", null,
                PredicadoObrigatoriedade: [[Condicao("FATO_INEXISTENTE", "IGUAL", true)]]),
        ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("itens[0].predicadoObrigatoriedade");
    }

    [Fact(DisplayName = "Fato de texto multivalorado não é coletável como campo de texto")]
    public async Task Handle_TextoMultivalorado_RecusaPelaCoerencia()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(
            [.. VocabularioSeed().Select(static f => f.Codigo == "NOME_SOCIAL" ? f with { Cardinalidade = "MULTIVALORADO" } : f)]);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao,
            [new FatoColetadoInput("NOME_SOCIAL", 0, "Nome social", "TEXTO", "NUNCA", null)], PrecondicaoIfMatch.Ausente);

        (await HandleAsync(mocks, command)).Error!.Code.Should().Be("FatoColetado.TipoRenderizacaoIncoerenteComDominio");
    }

    [Fact(DisplayName = "Campo de texto é aceito e congela o formato do fato no catálogo")]
    public async Task Handle_CampoDeTexto_CongelaOFormatoDoCatalogo()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao,
            [new FatoColetadoInput("NOME_SOCIAL", 0, "Nome social", "TEXTO", "NUNCA", null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.FatosColetados.Single().Formato.Should().Be("NOME_PESSOA");
    }

    [Fact(DisplayName = "ADR-0125: violações de forma de FatoColetado.Criar acumulam entre fatos, com o índice prefixado ao field")]
    public async Task Handle_DoisFatosComViolacaoDeForma_AcumulaComIndicePrefixadoAoField()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);

        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao,
        [
            new FatoColetadoInput("COR_RACA", -1, "Cor ou raça", "SELECAO_UNICA", "NUNCA", null),
            new FatoColetadoInput("BAIXA_RENDA", 1, "", "BOOLEANO", "NUNCA", null),
        ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Field).Should().BeEquivalentTo(["itens[0].ordem", "itens[1].rotulo"]);
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FatoCodigo vazio recusa com FatoCodigoObrigatorio, não FatoDesconhecido: o item de forma inválida não segue para o catálogo")]
    public async Task Handle_FatoCodigoVazio_RecusaComErroDeFormaNaoDeVocabulario()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao, [new FatoColetadoInput("", 0, "Rótulo", "SELECAO_UNICA", "NUNCA", null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal("FatoColetado.FatoCodigoObrigatorio");
    }

    [Fact(DisplayName = "ADR-0125: a forma inválida de um item e o fato desconhecido de outro saem juntos, cada um no seu campo")]
    public async Task Handle_FormaInvalidaEmUmFatoEVocabularioDesconhecidoNoOutro_AcumulaAsDuas()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);

        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao,
        [
            new FatoColetadoInput("COR_RACA", -1, "Cor ou raça", "SELECAO_UNICA", "NUNCA", null),
            new FatoColetadoInput("FATO_INEXISTENTE", 1, "Rótulo", "SELECAO_UNICA", "NUNCA", null),
        ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("itens[0].ordem", "FatoColetado.OrdemInvalida"),
            ("itens[1].fatoCodigo", "FatoColetado.FatoDesconhecido"),
        ]);
    }

    [Fact(DisplayName = "TipoRenderizacao coerente para fato CATEGORICO multivalorado (MODALIDADE) — mesmo não-coletável, a coerência é checada antes")]
    public async Task Handle_TipoRenderizacaoCoerenteComCategoricoMultivalorado_NaoFalhaPorCoerencia()
    {
        // MODALIDADE é CATEGORICO/MULTIVALORADO no vocabulário-seed, mas é DERIVADO — a
        // coletabilidade recusa antes da coerência de renderização ser sequer checada. Este
        // teste prova que o erro devolvido continua sendo o de coletabilidade, não o de
        // coerência, quando o TipoRenderizacao informado é o coerente com o domínio.
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao, [new FatoColetadoInput("MODALIDADE", 0, "Modalidade", "SELECAO_MULTIPLA", "NUNCA", null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FatoColetado.FatoNaoColetavel");
    }
}
