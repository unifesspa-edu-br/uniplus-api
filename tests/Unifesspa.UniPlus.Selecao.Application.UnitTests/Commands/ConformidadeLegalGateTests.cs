namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.DublesDoCongelamentoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.RegrasLegaisDeTeste;

/// <summary>
/// A verificação legal é a segunda dimensão de conformidade, ao lado da estrutural
/// (<see cref="PublicarProcessoSeletivoGateTests"/>), nos três handlers que congelam
/// a configuração. Uma regra vigente reprovada impede a canonicalização; a aprovação
/// repassa o <see cref="ResultadoConformidade"/> em <see cref="EntradaCanonicalizacao"/>.
/// </summary>
public sealed class ConformidadeLegalGateTests
{
    // O atalho não é só desempenho: sem ele, todo tipo de processo que ainda não tem
    // obrigatoriedade legal cadastrada passa a depender da leitura de cinco cadastros
    // que não vai usar.
    [Fact(DisplayName = "Processo sem regra vigente não consulta catálogo algum")]
    public async Task Publicar_SemRegraVigente_NaoConsultaCatalogos()
    {
        DependenciasDosHandlersQueCongelam handlers = DependenciasDosHandlersQueCongelam.Para(PublicacaoDeTeste.NovoProcessoConforme());
        handlers.Modalidades = Substitute.For<IModalidadeReader>();
        handlers.TiposDocumento = Substitute.For<ITipoDocumentoReader>();

        (Result resposta, _) = await handlers.PublicarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        await handlers.Modalidades.DidNotReceive().ListarVivosAsync(Arg.Any<CancellationToken>());
        await handlers.TiposDocumento.DidNotReceive().ListarVivosAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CA-12 (Publicar): regra legal vigente reprovada recusa ANTES da canonicalização")]
    public async Task Publicar_ComRegraLegalReprovada_RecusaSemCanonicalizar()
    {
        DependenciasDosHandlersQueCongelam handlers = DependenciasDosHandlersQueCongelam.Para(PublicacaoDeTeste.NovoProcessoConforme());
        handlers.ObrigatoriedadesLegais = RepositorioCom(RegraQueReprova());

        (Result resposta, IEnumerable<object> eventos) = await handlers.PublicarAsync();

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeLegalInsuficiente");
        _ = handlers.Canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        eventos.Should().BeEmpty();
        handlers.Processo.Situacao.Should().Be(SituacaoDoProcessoSeletivo.Rascunho, "a publicação recusada não transita o processo");
    }

    [Fact(DisplayName = "CA-13 (Publicar): regra legal aprovada congela ResultadoConformidade no campo novo da entrada")]
    public async Task Publicar_ComRegraLegalAprovada_RepassaConformidadeParaCanonicalizador()
    {
        ObrigatoriedadeLegal regra = RegraQueAprova();
        EntradaCanonicalizacao? entradaCapturada = null;
        DependenciasDosHandlersQueCongelam handlers = DependenciasDosHandlersQueCongelam.Para(PublicacaoDeTeste.NovoProcessoConforme());
        handlers.ObrigatoriedadesLegais = RepositorioCom(regra);
        handlers.Canonicalizer = CanonicalizerSubstituto(e => entradaCapturada = e);

        (Result resposta, IEnumerable<object> _) = await handlers.PublicarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        ConferirRegraCongelada(entradaCapturada, regra);
    }

    [Fact(DisplayName = "CA-14 (Retificar): regra legal vigente reprovada recusa ANTES da canonicalização")]
    public async Task Retificar_ComRegraLegalReprovada_RecusaSemCanonicalizar()
    {
        DependenciasDosHandlersQueCongelam handlers = DependenciasDosHandlersQueCongelam.ParaProcessoPublicado();
        handlers.ObrigatoriedadesLegais = RepositorioCom(RegraQueReprova());

        (Result resposta, IEnumerable<object> eventos) = await handlers.RetificarAsync();

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeLegalInsuficiente");
        _ = handlers.Canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        eventos.Should().BeEmpty();
    }

    private static void ConferirRegraCongelada(EntradaCanonicalizacao? entrada, ObrigatoriedadeLegal regra)
    {
        entrada.Should().NotBeNull();
        entrada!.Conformidade.Should().NotBeNull();
        RegraAvaliada regraCongelada = entrada.Conformidade!.Regras.Should().ContainSingle().Subject;
        regraCongelada.RegraId.Should().Be(regra.Id);
        regraCongelada.RegraCodigo.Should().Be("GATE_APROVA");
        regraCongelada.Aprovada.Should().BeTrue();
        regraCongelada.Hash.Should().Be(regra.Hash);
    }
}
