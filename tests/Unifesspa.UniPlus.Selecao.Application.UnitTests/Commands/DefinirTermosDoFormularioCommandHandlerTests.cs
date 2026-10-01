namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Termos exigidos pelo formulário (UNI-REQ-0086): a versão vem do catálogo e o conteúdo dela é
/// congelado; as condições citam só o que o processo coleta ou deriva; a obrigatoriedade tem
/// predicado se, e só se, é <c>QUANDO</c>.
/// </summary>
public sealed class DefinirTermosDoFormularioCommandHandlerTests
{
    private static readonly Guid TermoId = Guid.CreateVersion7();
    private static readonly Guid VersaoId = Guid.CreateVersion7();

    private sealed record Mocks(
        IProcessoSeletivoRepository Repository,
        IFatoCandidatoReader FatoCandidatoReader,
        ITermoConsentimentoReader TermoReader,
        ISelecaoUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo processo)
    {
        Mocks mocks = new(
            Substitute.For<IProcessoSeletivoRepository>(),
            Substitute.For<IFatoCandidatoReader>(),
            Substitute.For<ITermoConsentimentoReader>(),
            Substitute.For<ISelecaoUnitOfWork>());
        mocks.Repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new(Guid.CreateVersion7(), "COR_RACA", "Cor ou raça", null, "CATEGORICO", "DECLARADO", "ESCALAR",
                ["BRANCA", "PRETA", "PARDA"], "INSCRICAO", "CAMPO_INSCRICAO:COR_RACA", null, "GLOBAL", Ativo: true),
            new(Guid.CreateVersion7(), "BAIXA_RENDA", "Baixa renda", null, "BOOLEANO", "DECLARADO", "ESCALAR",
                null, "INSCRICAO", "CAMPO_INSCRICAO:BAIXA_RENDA", null, null, Ativo: true),
            new(Guid.CreateVersion7(), "FAIXA_ETARIA", "Faixa etária", null, "CATEGORICO", "DERIVADO", "ESCALAR",
                ["MENOR_DE_18", "DE_18_A_59"], "INSCRICAO", "ATRIBUTO_CANDIDATO:FAIXA_ETARIA", null, "GLOBAL", Ativo: true),
        ]);
        mocks.TermoReader.ListarVersoesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(
        [
            new VersaoTermoConsentimentoView(TermoId, VersaoId, "Autorização de consulta", "Autorizo a consulta.", "Lei 12.711/2012",
                "REGISTRO_DIGITAL_COM_LOG_IP", new string('b', 64)),
        ]);
        return mocks;
    }

    private static Task<Result<MutacaoAceita>> HandleAsync(Mocks mocks, ProcessoSeletivo processo, params TermoExigidoInput[] termos) =>
        DefinirTermosDoFormularioCommandHandler.Handle(
            new DefinirTermosDoFormularioCommand(processo.Id, FinalidadeFormulario.Inscricao, termos, PrecondicaoIfMatch.Ausente),
            mocks.Repository, mocks.FatoCandidatoReader, mocks.TermoReader, mocks.UnitOfWork, CancellationToken.None);

    private static ProcessoSeletivo ProcessoQueColetaCorRaca()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Termos", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        processo.DefinirItens(
            [FatoColetado.Criar("COR_RACA", 0, "Cor ou raça", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> Quando(string fato, object valor) =>
        [[new CondicaoPrecondicaoInput(fato, "IGUAL", JsonSerializer.SerializeToElement(valor))]];

    [Fact(DisplayName = "Termo condicional é aceito e congela o conteúdo da versão escolhida")]
    public async Task Handle_TermoCondicional_CongelaVersao()
    {
        ProcessoSeletivo processo = ProcessoQueColetaCorRaca();
        Mocks mocks = NovosMocks(processo);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, processo,
            new TermoExigidoInput("CONSULTA", 0, TermoId, VersaoId, Quando("COR_RACA", "PRETA"), "QUANDO", Quando("COR_RACA", "PRETA")));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        TermoExigidoFormulario termo = processo.TermosExigidos.Should().ContainSingle().Which;
        termo.Should().BeEquivalentTo(new { Texto = "Autorizo a consulta.", FormaAceite = "REGISTRO_DIGITAL_COM_LOG_IP", HashVersao = new string('b', 64) });
        termo.Obrigatoriedade.Tipo.Should().Be(TipoObrigatoriedade.Quando);
        termo.Exibicao!.FatosCitados.Should().Equal("COR_RACA");
    }

    [Fact(DisplayName = "Versão que pertence a outro termo é recusada no índice do termo")]
    public async Task Handle_VersaoDeOutroTermo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoQueColetaCorRaca();
        Mocks mocks = NovosMocks(processo);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, processo,
            new TermoExigidoInput("CONSULTA", 0, Guid.CreateVersion7(), VersaoId, null, "SEMPRE", null));

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "termos[0].versaoId",
            Error = new { Code = TermoExigidoFormularioErrorCodes.VersaoNaoEncontrada },
        });
    }

    [Fact(DisplayName = "Condição de termo que cita fato que o processo não coleta nem deriva é recusada")]
    public async Task Handle_CondicaoCitaFatoNaoColetado_Recusa()
    {
        ProcessoSeletivo processo = ProcessoQueColetaCorRaca();
        Mocks mocks = NovosMocks(processo);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, processo,
            new TermoExigidoInput("CONSULTA", 0, TermoId, VersaoId, Quando("BAIXA_RENDA", true), "SEMPRE", null));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("termos[0].exibicao");
        processo.TermosExigidos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Condição que cita fato calculado de atributos do candidato é recusada pelo nome, sem esconder a outra condição")]
    public async Task Handle_CondicaoCitaAtributoDoCandidato_RecusaNomeadaEAcumula()
    {
        ProcessoSeletivo processo = ProcessoQueColetaCorRaca();
        Mocks mocks = NovosMocks(processo);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, processo,
            new TermoExigidoInput("CONSULTA", 0, TermoId, VersaoId, Quando("FAIXA_ETARIA", "DE_18_A_59"), "QUANDO", Quando("BAIXA_RENDA", true)));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("termos[0].exibicao", GrafoFormularioErrorCodes.CitaAtributoDoCandidato),
            ("termos[0].predicadoObrigatoriedade", "PredicadoDnf.FatoNaoColetadoPeloProcesso"),
        ]);
    }

    [Fact(DisplayName = "Versão inexistente não esconde a condição inválida do mesmo termo: as duas recusas saem juntas")]
    public async Task Handle_VersaoInexistenteECondicaoInvalida_AcumulaAsDuas()
    {
        ProcessoSeletivo processo = ProcessoQueColetaCorRaca();
        Mocks mocks = NovosMocks(processo);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, processo,
            new TermoExigidoInput("CONSULTA", 0, TermoId, Guid.CreateVersion7(), Quando("BAIXA_RENDA", true), "SEMPRE", null));

        resultado.Errors.Select(static e => e.Field).Should().BeEquivalentTo(["termos[0].exibicao", "termos[0].versaoId"]);
    }

    [Theory(DisplayName = "Obrigatoriedade tem predicado se, e só se, é QUANDO")]
    [InlineData("QUANDO", false)]
    [InlineData("SEMPRE", true)]
    [InlineData("OBRIGATORIO", false)]
    public async Task Handle_ObrigatoriedadeIncoerente_Recusa(string tipo, bool comPredicado)
    {
        ProcessoSeletivo processo = ProcessoQueColetaCorRaca();
        Mocks mocks = NovosMocks(processo);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, processo,
            new TermoExigidoInput("CONSULTA", 0, TermoId, VersaoId, null, tipo, comPredicado ? Quando("COR_RACA", "PRETA") : null));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(TermoExigidoFormularioErrorCodes.ObrigatoriedadeInvalida);
    }

    [Fact(DisplayName = "Forma, versão e unicidade saem no mesmo lote, entre termos diferentes")]
    public async Task Handle_RecusasDeFasesDiferentes_SaemJuntas()
    {
        ProcessoSeletivo processo = ProcessoQueColetaCorRaca();
        Mocks mocks = NovosMocks(processo);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, processo,
            new TermoExigidoInput("", 0, TermoId, VersaoId, null, "SEMPRE", null),
            new TermoExigidoInput("CONSULTA", 1, TermoId, Guid.CreateVersion7(), null, "SEMPRE", null),
            new TermoExigidoInput("CONSULTA", 2, TermoId, VersaoId, null, "SEMPRE", null));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("termos[0].codigo", TermoFormularioErrorCodes.CodigoObrigatorio),
            ("termos[1].versaoId", TermoExigidoFormularioErrorCodes.VersaoNaoEncontrada),
            ("termos[2].codigo", TermoFormularioErrorCodes.CodigoDuplicado),
        ]);
    }
}
