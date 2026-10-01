namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
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
        new(Guid.CreateVersion7(), "DATA_NASCIMENTO", "Data de nascimento", null, "DATA", "DECLARADO", "ESCALAR",
            null, "INSCRICAO", "CAMPO_INSCRICAO:DATA_NASCIMENTO", null, null, Ativo: true),
        new(Guid.CreateVersion7(), "ENDERECO_RESIDENCIAL", "Endereço residencial", null, "ENDERECO", "DECLARADO", "ESCALAR",
            null, "INSCRICAO", "CAMPO_INSCRICAO:ENDERECO_RESIDENCIAL", null, null, Ativo: true),
        new(Guid.CreateVersion7(), "OPCAO_CURSO_1", "1ª opção de curso", null, "CATEGORICO", "DECLARADO", "ESCALAR",
            ["MEDICINA", "ENFERMAGEM"], "INSCRICAO", "CAMPO_INSCRICAO:OPCAO_CURSO_1", null, "GLOBAL", Ativo: true),
        new(Guid.CreateVersion7(), "OPCAO_LISTA_ESPERA", "Opção da lista de espera", null, "CATEGORICO", "DECLARADO", "ESCALAR",
            ["MEDICINA", "ENFERMAGEM"], "INSCRICAO", "CAMPO_INSCRICAO:OPCAO_LISTA_ESPERA", null, "GLOBAL", Ativo: true),
        new(Guid.CreateVersion7(), "OPCAO_CURSO_2", "2ª opção de curso", null, "CATEGORICO", "DECLARADO", "ESCALAR",
            ["MEDICINA"], "INSCRICAO", "CAMPO_INSCRICAO:OPCAO_CURSO_2", null, "GLOBAL", Ativo: true),
        new(Guid.CreateVersion7(), "FAIXA_ETARIA", "Faixa etária", null, "CATEGORICO", "DERIVADO", "ESCALAR",
            ["MENOR_DE_18", "DE_18_A_59", "60_OU_MAIS"], "INSCRICAO", "ATRIBUTO_CANDIDATO:FAIXA_ETARIA", null, "GLOBAL", Ativo: true),
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

        (await HandleAsync(mocks, coletaBaixaRenda)).Error!.Code.Should().Be(VinculoCatalogoErrorCodes.FatoDesativado);

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
        resultado.Error!.Code.Should().Be("ItemFormulario.FatoNaoColetavel");
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Fato de membro de grupo não é coletado como item do formulário")]
    public async Task Handle_FatoDeMembroDeGrupo_RetornaNaoColetavel()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(
            [.. VocabularioSeed().Select(static f => f.Codigo == "BAIXA_RENDA" ? f with { Escopo = "MEMBRO_GRUPO" } : f)]);
        DefinirFatosColetadosCommand command = new(
            processo.Id, FinalidadeFormulario.Inscricao, [new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "NUNCA", null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.Error!.Code.Should().Be(ItemFormularioErrorCodes.FatoNaoColetavel);
    }

    [Fact(DisplayName = "Coletar um fato computado de atributo (RENDA_PER_CAPITA) é recusado como não coletável")]
    public async Task Handle_FatoComputado_RetornaNaoColetavel()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao, [new FatoColetadoInput("RENDA_PER_CAPITA", 0, "Renda per capita", "NUMERO", "NUNCA", null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ItemFormulario.FatoNaoColetavel");
    }

    [Fact(DisplayName = "Fato fora do vocabulário é recusado como desconhecido, sem tradução")]
    public async Task Handle_FatoForaDoVocabulario_RetornaDesconhecido()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao, [new FatoColetadoInput("V", 0, "V", "SELECAO_UNICA", "NUNCA", null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ItemFormulario.FatoDesconhecido");
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
        resultado.Error!.Code.Should().Be("GrafoFormulario.CitaFatoPosterior");
    }

    [Theory(DisplayName = "TipoRenderizacao incoerente com Domínio/Cardinalidade do fato no catálogo é recusado")]
    [InlineData("COR_RACA", "BOOLEANO", "COR_RACA é CATEGORICO/ESCALAR — só SELECAO_UNICA é coerente")]
    [InlineData("COR_RACA", "NUMERO", "COR_RACA é CATEGORICO/ESCALAR — NUMERO não é coerente com nenhum categórico")]
    [InlineData("COR_RACA", "SELECAO_MULTIPLA", "COR_RACA é ESCALAR, não MULTIVALORADO — só SELECAO_UNICA é coerente")]
    [InlineData("BAIXA_RENDA", "SELECAO_UNICA", "BAIXA_RENDA é BOOLEANO — só BOOLEANO é coerente")]
    [InlineData("BAIXA_RENDA", "NUMERO", "BAIXA_RENDA é BOOLEANO — NUMERO não é coerente")]
    [InlineData("NOME_SOCIAL", "SELECAO_UNICA", "NOME_SOCIAL é TEXTO — só TEXTO é coerente")]
    [InlineData("BAIXA_RENDA", "TEXTO", "BAIXA_RENDA é BOOLEANO — TEXTO não é coerente")]
    [InlineData("DATA_NASCIMENTO", "TEXTO", "DATA_NASCIMENTO é DATA — só DATA é coerente")]
    [InlineData("ENDERECO_RESIDENCIAL", "DATA", "ENDERECO_RESIDENCIAL é ENDERECO — só ENDERECO é coerente")]
    public async Task Handle_TipoRenderizacaoIncoerente_RetornaErroDeCoerencia(
        string fatoCodigo, string tipoRenderizacaoIncoerente, string motivo)
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao, [new FatoColetadoInput(fatoCodigo, 0, "Rótulo", tipoRenderizacaoIncoerente, "NUNCA", null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue(motivo);
        resultado.Error!.Code.Should().Be("ItemFormulario.TipoRenderizacaoIncoerenteComDominio");
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

    [Theory(DisplayName = "Data e endereço são coletados no campo do domínio deles")]
    [InlineData("DATA_NASCIMENTO", "DATA")]
    [InlineData("ENDERECO_RESIDENCIAL", "ENDERECO")]
    public async Task Handle_DataEEndereco_ColetaNoCampoDoDominio(string fatoCodigo, string tipoRenderizacao)
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, new DefinirFatosColetadosCommand(
            processo.Id, FinalidadeFormulario.Inscricao, [new FatoColetadoInput(fatoCodigo, 0, "Rótulo", tipoRenderizacao, "SEMPRE", null)], PrecondicaoIfMatch.Ausente));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.FatosColetados.Single().TipoRenderizacao.Should().Be(TipoRenderizacaoCodigo.FromCodigo(tipoRenderizacao));
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

        (await HandleAsync(mocks, command)).Error!.Code.Should().Be("ItemFormulario.TipoRenderizacaoIncoerenteComDominio");
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

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal("ItemFormulario.FatoCodigoObrigatorio");
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
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao, [new FatoColetadoInput("MODALIDADE", 0, "Modalidade", "SELECAO_MULTIPLA", "NUNCA", null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ItemFormulario.FatoNaoColetavel");
    }

    [Theory(DisplayName = "Restrição de tipo desconhecido ou de limites incoerentes é recusada no campo dela")]
    [InlineData("REGEX", null, null, "RestricaoValor.TipoDesconhecido")]
    [InlineData("TAMANHO_TEXTO", 1.5, null, "RestricaoValor.LimitesIncoerentes")]
    [InlineData("TAMANHO_TEXTO", 10.0, 2.0, "RestricaoValor.LimitesIncoerentes")]
    public async Task Handle_RestricaoDeFormaInvalida_Recusa(string tipo, double? minimo, double? maximo, string codigo)
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao,
        [
            new FatoColetadoInput("NOME_SOCIAL", 0, "Nome social", "TEXTO", "NUNCA", null,
                Restricoes: [new RestricaoValorInput(tipo, (decimal?)minimo, (decimal?)maximo)]),
        ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "itens[0].restricoes[0]",
            Error = new { Code = codigo },
        });
    }

    [Fact(DisplayName = "Opção permitida fora do domínio do próprio fato é recusada no grupo dela")]
    public async Task Handle_OpcaoForaDoDominio_Recusa()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao,
        [
            new FatoColetadoInput("COR_RACA", 0, "Cor ou raça", "SELECAO_UNICA", "SEMPRE", null,
                Restricoes: [new RestricaoValorInput("OPCOES_PERMITIDAS", Entradas: [new OpcoesCondicionadasInput(null, ["PRETA", "ROXA"])])]),
        ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "itens[0].restricoes[0].entradas[0].valores",
            Error = new { Code = "PredicadoDnf.ValorForaDoDominio" },
        });
    }

    [Fact(DisplayName = "Opções permitidas em campo que não é de seleção são recusadas pela coerência com o tipo do campo")]
    public async Task Handle_OpcoesEmCampoBooleano_RecusaPelaCoerencia()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao,
        [
            new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "NUNCA", null,
                Restricoes: [new RestricaoValorInput("OPCOES_PERMITIDAS", Entradas: [new OpcoesCondicionadasInput(null, ["SIM"])])]),
        ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "itens[0].restricoes[0]",
            Error = new { Code = ItemFormularioErrorCodes.RestricaoIncoerente },
        });
    }

    [Fact(DisplayName = "ADR-0125: recusa semântica da restrição e recusa do próprio item saem no mesmo lote")]
    public async Task Handle_RecusaSemanticaERecusaDoItem_Acumulam()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao,
        [
            new FatoColetadoInput("COR_RACA", 0, "Cor ou raça", "SELECAO_UNICA", "SEMPRE", null,
                Restricoes:
                [
                    new RestricaoValorInput("OPCOES_PERMITIDAS", Entradas: [new OpcoesCondicionadasInput(null, ["ROXA"])]),
                    new RestricaoValorInput("OPCOES_PERMITIDAS", Entradas: [new OpcoesCondicionadasInput(null, ["PRETA"])]),
                ]),
        ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.Errors.Select(static e => e.Error.Code).Should().BeEquivalentTo(
            ["PredicadoDnf.ValorForaDoDominio", "RestricaoValor.TipoRepetido"]);
    }

    [Fact(DisplayName = "Opção permitida desativada no catálogo é recusada como vínculo novo")]
    public async Task Handle_OpcaoDesativada_RecusaVinculoNovo()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(
            [.. VocabularioSeed().Select(static f => f.Codigo == "COR_RACA"
                ? f with { ValoresDominioDeclarados = [new FatoValorDominioViewItem("AMARELA", null, 0, Ativo: false)] }
                : f)]);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao,
        [
            new FatoColetadoInput("COR_RACA", 0, "Cor ou raça", "SELECAO_UNICA", "SEMPRE", null,
                Restricoes: [new RestricaoValorInput("OPCOES_PERMITIDAS", Entradas: [new OpcoesCondicionadasInput(null, ["AMARELA"])])]),
        ], PrecondicaoIfMatch.Ausente);

        (await HandleAsync(mocks, command)).Error!.Code.Should().Be(VinculoCatalogoErrorCodes.ValorDesativado);
    }

    [Theory(DisplayName = "Opções formadas pelas respostas só vêm de campo cujas opções são todas opções do campo")]
    [InlineData("OPCAO_CURSO_1", true)]
    [InlineData("OPCAO_CURSO_2", true)]
    [InlineData("COR_RACA", false)]
    public async Task Handle_OpcoesDasRespostas_ExigemOpcoesContidasNoAlvo(string fonte, bool aceita)
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        string rotulo = fonte == "COR_RACA" ? "Cor ou raça" : "1ª opção de curso";
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao,
        [
            new FatoColetadoInput(fonte, 0, rotulo, "SELECAO_UNICA", "SEMPRE", null),
            new FatoColetadoInput("OPCAO_LISTA_ESPERA", 1, "Lista de espera", "SELECAO_UNICA", "NUNCA", null,
                Restricoes: [new RestricaoValorInput("OPCOES_DAS_RESPOSTAS", Fatos: [fonte])]),
        ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        if (aceita)
        {
            resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
            processo.FatosColetados.Single(static f => f.FatoCodigo == "OPCAO_LISTA_ESPERA").FatosCitados.Should().Equal(fonte);
        }
        else
        {
            resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Field = "itens[1].restricoes[0].fatos",
                Error = new { Code = ItemFormularioErrorCodes.OpcoesDeOutroDominio },
            });
        }
    }

    [Fact(DisplayName = "Grupo de opções condicionado a campo posterior é recusado")]
    public async Task Handle_CondicaoDasOpcoesSobreCampoPosterior_Recusa()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao,
        [
            new FatoColetadoInput("COR_RACA", 0, "Cor ou raça", "SELECAO_UNICA", "SEMPRE", null,
                Restricoes: [new RestricaoValorInput("OPCOES_PERMITIDAS",
                    Entradas: [new OpcoesCondicionadasInput([[Condicao("BAIXA_RENDA", "IGUAL", true)]], ["PRETA"])])]),
            new FatoColetadoInput("BAIXA_RENDA", 1, "Baixa renda", "BOOLEANO", "NUNCA", null),
        ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoPosterior);
    }

    [Fact(DisplayName = "Regra do item que cita fato calculado de atributos do candidato é recusada pelo nome")]
    public async Task Handle_CitaAtributoDoCandidato_RecusaNomeada()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao,
        [
            new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "NUNCA", [[Condicao("FAIXA_ETARIA", "IGUAL", "DE_18_A_59")]]),
        ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "itens[0]",
            Error = new { Code = GrafoFormularioErrorCodes.CitaAtributoDoCandidato },
        });
    }

    [Fact(DisplayName = "Condição sem fato é recusada, nunca estoura")]
    public async Task Handle_CondicaoSemFato_Recusa()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao,
            [new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "NUNCA", [[Condicao(null!, "IGUAL", true)]])],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
    }

    [Fact(DisplayName = "Acima do teto de itens, a lista é recusada inteira sem ler o catálogo")]
    public async Task Handle_AcimaDoTeto_RecusaSemLerOCatalogo()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        FatoColetadoInput[] itens = [.. Enumerable.Range(0, FormaDoItem.MaximoDeItens + 1)
            .Select(static i => new FatoColetadoInput($"FATO_{i}", i, "Campo", "BOOLEANO", "SEMPRE", null))];

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, new DefinirFatosColetadosCommand(processo.Id, FinalidadeFormulario.Inscricao, itens, PrecondicaoIfMatch.Ausente));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.ItensEmExcesso);
        await mocks.FatoCandidatoReader.DidNotReceive().ListarAsync(Arg.Any<CancellationToken>());
    }
}
