namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDeRecusaNoCongelamento;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

/// <summary>
/// A forma de cada recusa, por etapa: a que traz um erro único e sem campo, a que acumula o lote inteiro na ordem em
/// que o portão o produziu, e a que acontece depois de o agregado transitar.
/// </summary>
public sealed class FormaDasRecusasAoCongelarTests
{
    public static TheoryData<DefeitoDoCongelamento> RecusasDeErroUnico() =>
    [
        DefeitoDoCongelamento.Documento,
        DefeitoDoCongelamento.TipoDeAto,
        DefeitoDoCongelamento.Cronograma,
        DefeitoDoCongelamento.TiposDeAtoCongelados,
        DefeitoDoCongelamento.Anexos,
        DefeitoDoCongelamento.ConformidadeLegal,
    ];

    [Theory(DisplayName = "A recusa de um portão de erro único chega sem campo e sem eventos")]
    [MemberData(nameof(RecusasDeErroUnico))]
    public async Task PortaoDeErroUnico_ChegaSemCampo(DefeitoDoCongelamento defeito)
    {
        (Result resposta, IEnumerable<object> eventos) = await Para(OperacaoQueCongela.Publicar).Com(defeito).ExecutarAsync();

        resposta.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "O ciclo recursal aberto chega com os erros que a conferência produziu, sem perder nenhum")]
    public async Task CicloRecursal_ChegaComOsErrosDaConferencia()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.ComPreliminarPublicadoEDefinitivoPendente();
        VersaoConfiguracao versao = (await cenario.Servicos.Repositorio.ObterVersaoAtualAsync(cenario.Processo.Id, CancellationToken.None))!;
        Result esperado = await ConferenciaDoCicloRecursalAberto.ConferirAsync(
            cenario.Servicos.Atos, cenario.Servicos.Codecs, cenario.Processo, versao, CancellationToken.None);

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        esperado.IsFailure.Should().BeTrue();
        resposta.Errors.Should().Equal(esperado.Errors);
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "A recópia chega com o lote inteiro de erros, os itens antes das unidades")]
    public async Task Recopia_ChegaComOLoteInteiro()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Preparo");
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo)
            .ComCatalogoDeFatos(CatalogoComORgUfPorMunicipio())
            .ComUnidades(ResolvedorDeUnidadeDeTeste.Com());
        CatalogoVivo catalogo = await servicos.Catalogos().LerAsync(processo, CancellationToken.None);
        Result<RecopiadoAoCongelar> esperado = await servicos.Recopia().RecopiarAsync(processo, catalogo, new PrimeiraPublicacao(), CancellationToken.None);

        (Result resposta, IEnumerable<object> eventos) = await servicos.PublicarAsync(ComandoDePublicacao(processo));

        esperado.Errors.Should().HaveCountGreaterThan(1);
        resposta.Errors.Should().Equal(esperado.Errors);
        eventos.Should().BeEmpty();
    }

    [Theory(DisplayName = "A proteção pendente do tipo e do fato chega com os dois erros, nessa ordem")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Protecao_ChegaComOsDoisErrosNaOrdem(bool fechamento)
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        if (fechamento)
        {
            cenario.AbrirSessao();
        }

        cenario.ComTipoDaExigenciaPendente();
        await cenario.ComFatoCitadoPendenteAsync();

        (Result resposta, _) = fechamento ? await cenario.FecharAsync() : await cenario.RetificarAsync();

        resposta.Errors.Select(static e => e.Error.Code).Should().Equal(
            "ProcessoSeletivo.TipoDocumentoComProtecaoPendente", "ProcessoSeletivo.FatoComProtecaoPendente");
    }

    [Fact(DisplayName = "A transição recusada devolve erro único e sem eventos, descarta o rastreamento e não grava")]
    public async Task TransicaoRecusada_DevolveErroUnicoEDescarta()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.AbrirSessao();

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        resposta.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
        resposta.Error!.Code.Should().Be("RascunhoRetificacao.JaAberta");
        eventos.Should().BeEmpty();
        _ = cenario.Servicos.Canonicalizer.Received(1).Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        cenario.Servicos.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await cenario.Servicos.Repositorio.DidNotReceive().AdicionarVersaoConfiguracaoAsync(Arg.Any<VersaoConfiguracao>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O registro do ato recusado depois da transição chega com o campo, sem eventos, e nada é escrito")]
    public async Task RegistroDoAtoRecusado_ChegaComOCampoENadaEEscrito()
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.Retificar);
        VersaoConfiguracao vigente = (await cenario.Servicos.Repositorio.ObterVersaoAtualAsync(cenario.Processo.Id, CancellationToken.None))!;
        typeof(VersaoConfiguracao).GetProperty(nameof(VersaoConfiguracao.AtoCriadorId))!.SetValue(vigente, Guid.Empty);

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        FieldError erro = resposta.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("atoCorrigidoId");
        erro.Error.Code.Should().Be(AtoDoProcessoErrorCodes.AtoCorrigidoInvalido);
        eventos.Should().BeEmpty();
        cenario.Servicos.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await cenario.Servicos.Repositorio.DidNotReceive().AdicionarVersaoConfiguracaoAsync(Arg.Any<VersaoConfiguracao>(), Arg.Any<CancellationToken>());
        await cenario.Servicos.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A unicidade violada no flush chega como erro único, sem eventos, e o rastreamento com a remoção do rascunho é descartado")]
    public async Task UnicidadeNoFlush_ChegaComoErroUnico()
    {
        DependenciasDosHandlersQueCongelam publicacao = DependenciasDosHandlersQueCongelam.Para(NovoProcessoConforme());
        publicacao.Servicos.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(ExcecaoDeBancoDeTeste.ViolacaoDeUnicidade("ux_versoes_configuracao_processo_numero"));

        (Result resposta, IEnumerable<object> eventos) = await publicacao.PublicarAsync();

        resposta.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
        resposta.Error!.Code.Should().Be("VersaoConfiguracao.NumeroDuplicado");
        eventos.Should().BeEmpty();
        publicacao.Servicos.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    private static PublicarProcessoSeletivoCommand ComandoDePublicacao(ProcessoSeletivo processo) =>
        new(processo.Id, "001/2026", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto());

    private static IFatoCandidatoReader CatalogoComORgUfPorMunicipio()
    {
        IReadOnlyList<FatoCandidatoView> fatos =
            [.. CatalogoDoConjuntoBasico.Fatos.Select(static f => f.Codigo == "RG_UF" ? f with { FonteValores = "GEO_MUNICIPIO" } : f)];
        IFatoCandidatoReader catalogo = Substitute.For<IFatoCandidatoReader>();
        catalogo.ListarAsync(Arg.Any<CancellationToken>()).Returns(fatos);

        return catalogo;
    }
}
