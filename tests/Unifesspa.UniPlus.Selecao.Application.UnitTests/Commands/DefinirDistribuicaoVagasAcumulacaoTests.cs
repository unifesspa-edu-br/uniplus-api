namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDaDistribuicaoDeVagas;

/// <summary>
/// A distribuição acumula as recusas de todas as ofertas, na ordem da forma, do cadastro e da declaração, e
/// uma regra só deixa de rodar quando um dado dela foi recusado.
/// </summary>
public sealed class DefinirDistribuicaoVagasAcumulacaoTests
{
    private const string Institucional = "DISTRIBUICAO_INSTITUCIONAL";

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "PSIQ 2026", TiposDeProcessoDeTeste.IndigenaEQuilombola, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static Mocks MocksComOferta(ProcessoSeletivo processo, Guid ofertaCursoId, RegraCatalogo? regra = null)
    {
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(NovaOferta(ofertaCursoId));
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), "v1", Arg.Any<CancellationToken>()).Returns(regra);
        return mocks;
    }

    private static ConfiguracaoDistribuicaoVagasInput Pedido(
        Guid ofertaCursoId, string regra, Guid[] modalidadeIds, params QuantidadeVagaInput[] quadro) =>
        new(ofertaCursoId, 60, null, regra, "v1", null, null, null, modalidadeIds, quadro);

    private static async Task<Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>> DefinirAsync(
        Mocks mocks, ProcessoSeletivo processo, params ConfiguracaoDistribuicaoVagasInput[] distribuicoes) =>
        await DefinirDistribuicaoVagasCommandHandler.Handle(
            new DefinirDistribuicaoVagasCommand(processo.Id, distribuicoes),
            mocks.Repository, mocks.Resolvedor, CadastrosVivos.CatalogoDeFatos(), mocks.UnitOfWork, CancellationToken.None);

    private static (int Oferta, string Campo, string Codigo) Recusa(int oferta, string campo, string codigo) =>
        (oferta, campo, "ConfiguracaoDistribuicaoVagas." + codigo);

    private static void DeveRecusarNaOrdem(
        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> resultado, params (int Oferta, string Campo, string Codigo)[] recusas) =>
        resultado.Errors.Select(static erro => erro.Field + "|" + erro.Error.Code).Should().Equal(
            recusas.Select(static r => $"distribuicaoVagas[{r.Oferta}].{r.Campo}|{r.Codigo}"));

    [Fact(DisplayName = "A quantidade negativa no quadro responde com o código e o campo específicos, citando o valor")]
    public async Task QuantidadeNegativa_RespondeComCodigoECampoEspecificos()
    {
        ProcessoSeletivo processo = NovoProcesso();
        (Guid oferta, Guid modalidade) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        Mocks mocks = MocksComOferta(processo, oferta, RegraDistribuicao(Institucional));
        mocks.ModalidadeReader.ObterPorIdAsync(modalidade, Arg.Any<CancellationToken>()).Returns(NovaModalidadeAmpla(modalidade));

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> resultado = await DefinirAsync(
            mocks, processo, Pedido(oferta, Institucional, [modalidade], new QuantidadeVagaInput(modalidade, -3)));

        DeveRecusarNaOrdem(resultado, Recusa(0, "quadro[0].quantidade", "QuantidadeVagaNegativa"));
        resultado.Error!.Message.Should().Be("A quantidade de vagas declarada não pode ser negativa (-3).");
    }

    [Fact(DisplayName = "As recusas de cadastro da oferta, da regra, da referência demográfica e da modalidade aparecem juntas")]
    public async Task RecusasDeCadastro_AparecemJuntas()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ConfiguracaoDistribuicaoVagasInput pedido = new(
            Guid.CreateVersion7(), 50, 0.5m, RegraDeDistribuicaoDeVagas.Lei12711.Codigo(), "v1", "AJUSTE", "v1", Guid.CreateVersion7(),
            [Guid.CreateVersion7(), Guid.CreateVersion7()], []);

        DeveRecusarNaOrdem(
            await DefinirAsync(NovosMocks(processo, processo.Id), processo, pedido),
            Recusa(0, "ofertaCursoId", "OfertaCursoNaoEncontrada"),
            Recusa(0, "regraDistribuicaoCodigo", "RegraDistribuicaoNaoEncontrada"),
            Recusa(0, "regraAjusteCodigo", "RegraAjusteNaoEncontrada"),
            Recusa(0, "referenciaReservaDemograficaId", "ReferenciaDemograficaNaoEncontrada"),
            Recusa(0, "modalidadeIds[0]", "ModalidadeNaoEncontrada"),
            Recusa(0, "modalidadeIds[1]", "ModalidadeNaoEncontrada"));
    }

    [Fact]
    public async Task ModalidadeComDoisDefeitos_PropagaTodasAsCausasNoItem()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid oferta = Guid.CreateVersion7();
        Guid modalidade = Guid.CreateVersion7();
        Mocks mocks = MocksComOferta(processo, oferta, RegraDistribuicao(Institucional));
        mocks.ModalidadeReader.ObterPorIdAsync(modalidade, Arg.Any<CancellationToken>()).Returns(
            NovaModalidadeAmpla(modalidade) with { BaseLegal = null, RegraRemanejamento = "CRUZADO" });
        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> resultado = await DefinirAsync(
            mocks, processo, Pedido(oferta, Institucional, [modalidade]));
        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("distribuicaoVagas[0].modalidadeIds[0]", "ModalidadeSelecionada.BaseLegalObrigatoria"),
            ("distribuicaoVagas[0].modalidadeIds[0]", "ModalidadeSelecionada.RemanejamentoCruzadoIncompleto"));
    }

    [Fact(DisplayName = "O nome recusado da oferta não esconde o teto de vagas, que vem da mesma oferta")]
    public async Task NomeDaOfertaRecusado_NaoEscondeOTetoDeVagas()
    {
        ProcessoSeletivo processo = NovoProcesso();
        (Guid oferta, Guid modalidade) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        Mocks mocks = MocksComOferta(processo, oferta, RegraDistribuicao(Institucional));
        mocks.OfertaCursoReader.ObterPorIdAsync(oferta, Arg.Any<CancellationToken>())
            .Returns(NovaOferta(oferta) with { Codigo = new string('X', OfertaCursoSnapshot.CodigoMaxLength + 1) });
        mocks.ModalidadeReader.ObterPorIdAsync(modalidade, Arg.Any<CancellationToken>()).Returns(NovaModalidadeAmpla(modalidade));

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> resultado = await DefinirAsync(
            mocks, processo, Pedido(oferta, Institucional, [modalidade], new QuantidadeVagaInput(modalidade, 150)) with { VagasOfertadasBase = 150 });

        resultado.Errors.Select(static erro => erro.Field).Should().Contain(
            ["distribuicaoVagas[0].ofertaCursoId", "distribuicaoVagas[0].vagasOfertadasBase"]);
    }

    [Fact(DisplayName = "A regra de distribuição recusada não gera a falsa recusa de referência demográfica obrigatória")]
    public async Task RegraRecusada_NaoGeraFalsaReferenciaDemograficaObrigatoria()
    {
        ProcessoSeletivo processo = NovoProcesso();
        (Guid oferta, Guid modalidade) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        Mocks mocks = MocksComOferta(processo, oferta);
        mocks.ModalidadeReader.ObterPorIdAsync(modalidade, Arg.Any<CancellationToken>()).Returns(NovaModalidadeAmpla(modalidade));

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> resultado = await DefinirAsync(
            mocks, processo, Pedido(oferta, RegraDeDistribuicaoDeVagas.Lei12711.Codigo(), [modalidade]));

        DeveRecusarNaOrdem(resultado, Recusa(0, "regraDistribuicaoCodigo", "RegraDistribuicaoNaoEncontrada"));
    }

    [Fact(DisplayName = "A modalidade recusada suprime só a ausência de código do rol, e a fronteira da quantidade das aceitas continua conferida")]
    public async Task ModalidadeRecusada_SuprimeAAusenciaNoRolEMantemAFronteiraDasAceitas()
    {
        ProcessoSeletivo processo = NovoProcesso();
        (Guid oferta, Guid aceita, Guid recusada) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        Mocks mocks = MocksComOferta(processo, oferta, RegraDistribuicaoComRol(Institucional, "IND", "QUI"));
        mocks.ModalidadeReader.ObterPorIdAsync(aceita, Arg.Any<CancellationToken>()).Returns(NovaModalidadeCotaReservada(aceita, "IND"));

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> resultado = await DefinirAsync(
            mocks, processo, Pedido(oferta, Institucional, [aceita, recusada]));

        DeveRecusarNaOrdem(
            resultado,
            Recusa(0, "modalidadeIds[1]", "ModalidadeNaoEncontrada"),
            Recusa(0, "modalidadeIds", "QuadroModalidadeAusente"));
    }

    [Fact(DisplayName = "O quadro com modalidade fora das selecionadas é recusado em cada linha, no campo da modalidade do quadro")]
    public async Task QuadroComModalidadeNaoSelecionada_RecusaCadaLinha()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid selecionada = Guid.CreateVersion7();
        ConfiguracaoDistribuicaoVagasInput pedido = Pedido(
            Guid.CreateVersion7(), "X", [selecionada],
            new QuantidadeVagaInput(selecionada, 10), new QuantidadeVagaInput(Guid.CreateVersion7(), 10), new QuantidadeVagaInput(Guid.CreateVersion7(), 10));

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> resultado = await DefinirAsync(NovosMocks(processo, processo.Id), processo, pedido);

        resultado.Errors.Take(2).Select(static erro => erro.Field).Should().Equal(
            "distribuicaoVagas[0].quadro[1].modalidadeId", "distribuicaoVagas[0].quadro[2].modalidadeId");
        resultado.Errors.Take(2).Should().OnlyContain(erro => erro.Error.Code == "ConfiguracaoDistribuicaoVagas.QuadroModalidadeNaoSelecionada");
    }

    [Fact(DisplayName = "A forma de todas as ofertas vem antes do cadastro, e todas as ofertas são resolvidas")]
    public async Task FormaDeTodasAsOfertas_VemAntesDoCadastro()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ConfiguracaoDistribuicaoVagasInput primeira = Pedido(Guid.CreateVersion7(), "X", [Guid.CreateVersion7()]);
        ConfiguracaoDistribuicaoVagasInput segunda = primeira with { OfertaCursoId = Guid.CreateVersion7(), PercentualDaReservaLegal = 1.5m };

        DeveRecusarNaOrdem(
            await DefinirAsync(NovosMocks(processo, processo.Id), processo, primeira, segunda),
            Recusa(1, "percentualDaReservaLegal", "PercentualDaReservaLegalForaDoLimite"),
            Recusa(0, "ofertaCursoId", "OfertaCursoNaoEncontrada"),
            Recusa(0, "regraDistribuicaoCodigo", "RegraDistribuicaoNaoEncontrada"),
            Recusa(0, "modalidadeIds[0]", "ModalidadeNaoEncontrada"),
            Recusa(1, "ofertaCursoId", "OfertaCursoNaoEncontrada"),
            Recusa(1, "regraDistribuicaoCodigo", "RegraDistribuicaoNaoEncontrada"),
            Recusa(1, "modalidadeIds[0]", "ModalidadeNaoEncontrada"));
    }

    [Fact(DisplayName = "A lista de modalidades vazia é recusada junto com o cadastro, antes dele na ordem")]
    public async Task ModalidadeIdsVazio_VemAntesDoCadastro()
    {
        ProcessoSeletivo processo = NovoProcesso();

        DeveRecusarNaOrdem(
            await DefinirAsync(NovosMocks(processo, processo.Id), processo, Pedido(Guid.CreateVersion7(), "X", [])),
            Recusa(0, "modalidadeIds", "ModalidadesVazias"),
            Recusa(0, "ofertaCursoId", "OfertaCursoNaoEncontrada"),
            Recusa(0, "regraDistribuicaoCodigo", "RegraDistribuicaoNaoEncontrada"));
    }
}
