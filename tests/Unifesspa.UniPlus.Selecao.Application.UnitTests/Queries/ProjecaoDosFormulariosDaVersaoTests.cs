namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries.FormulariosDaInscricaoDeTeste;

/// <summary>
/// A projeção dos formulários de uma versão já resolvida: por código, e os da inscrição de uma vez só, a partir
/// do mesmo congelado.
/// </summary>
public sealed class ProjecaoDosFormulariosDaVersaoTests
{
    private static readonly IReadOnlyList<FormularioDaVersao> DoisObrigatorios =
    [
        new(FormularioDeTeste.CodigoDaInscricao, Obrigatorio: true),
        new("COMPLEMENTO_DA_INSCRICAO", Obrigatorio: true),
    ];

    [Fact(DisplayName = "A projeção do código que a versão não declara é sucesso sem formulário, não falha")]
    public void Projetar_VersaoSemOFormularioDoCodigo_SucessoSemFormulario()
    {
        (VersaoConfiguracao versao, EnvelopeReidratado envelope, _) = MontarVersaoComFormularioDeInscricao(
            formularios: [new FormularioDaVersao("SOLICITACAO_DE_ISENCAO", Obrigatorio: false)]);

        Result<FormularioApresentadoDoProcessoDto?> projetado = ProjecaoDoFormularioDaVersao.Projetar(
            versao, CodigoDoFormulario.Criar(FormularioDeTeste.CodigoDaInscricao).Value!, RegistroReidratando(envelope), acervoDaComprovacao: null);

        projetado.IsSuccess.Should().BeTrue(projetado.Error?.Message);
        projetado.Value.Should().BeNull();
    }

    [Theory(DisplayName = "A projeção por código devolve o formulário de qualquer código que a versão declara")]
    [InlineData("INSCRICAO")]
    [InlineData("COMPLEMENTO_DA_INSCRICAO")]
    public void Projetar_CodigoDeclaradoNaVersao_DevolveOFormulario(string codigo)
    {
        (VersaoConfiguracao versao, EnvelopeReidratado envelope, _) = MontarVersaoComFormularioDeInscricao(formularios: DoisObrigatorios);

        Result<FormularioApresentadoDoProcessoDto?> projetado = ProjecaoDoFormularioDaVersao.Projetar(
            versao, CodigoDoFormulario.Criar(codigo).Value!, RegistroReidratando(envelope), acervoDaComprovacao: null);

        projetado.Value!.Codigo.Should().Be(codigo);
    }

    [Fact(DisplayName = "Os formulários da inscrição saem todos de uma vez, na ordem do código")]
    public void ProjetarDaInscricao_DoisObrigatorios_DevolveOsDois()
    {
        (VersaoConfiguracao versao, EnvelopeReidratado envelope, _) = MontarVersaoComFormularioDeInscricao(formularios: DoisObrigatorios);

        Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>> projetados =
            ProjecaoDoFormularioDaVersao.ProjetarDaInscricao(versao, RegistroReidratando(envelope));

        projetados.IsSuccess.Should().BeTrue(projetados.Error?.Message);
        projetados.Value!.Select(static f => f.Codigo).Should().Equal("COMPLEMENTO_DA_INSCRICAO", FormularioDeTeste.CodigoDaInscricao);
    }

    [Fact(DisplayName = "Versão sem formulário de inscrição projeta lista vazia, não falha")]
    public void ProjetarDaInscricao_SemFormularioDeInscricao_ListaVazia()
    {
        (VersaoConfiguracao versao, EnvelopeReidratado envelope, _) = MontarVersaoComFormularioDeInscricao(
            formularios: [new FormularioDaVersao("SOLICITACAO_DE_ISENCAO", Obrigatorio: false)]);

        Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>> projetados =
            ProjecaoDoFormularioDaVersao.ProjetarDaInscricao(versao, RegistroReidratando(envelope));

        projetados.IsSuccess.Should().BeTrue(projetados.Error?.Message);
        projetados.Value.Should().BeEmpty();
    }

    [Fact(DisplayName = "Documento congelado ilegível falha a projeção da inscrição, sem lista parcial")]
    public void ProjetarDaInscricao_CongeladoIlegivel_FalhaSemLista()
    {
        (_, EnvelopeReidratado envelope, _) = MontarVersaoComFormularioDeInscricao(formularios: DoisObrigatorios);
        VersaoConfiguracao ilegivel = VersaoComDocumento("isto não é um documento");

        Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>> projetados =
            ProjecaoDoFormularioDaVersao.ProjetarDaInscricao(ilegivel, RegistroReidratando(envelope));

        projetados.IsFailure.Should().BeTrue();
        projetados.Error!.Code.Should().Be("Snapshot.VigenteAusente");
        projetados.Value.Should().BeNull();
    }

    [Fact(DisplayName = "Data de referência dos dados fora da forma falha a projeção dos dois formulários, sem lista parcial")]
    public void ProjetarDaInscricao_FormularioSemApresentacao_FalhaSemLista()
    {
        (_, EnvelopeReidratado envelope, _) = MontarVersaoComFormularioDeInscricao(formularios: DoisObrigatorios);
        VersaoConfiguracao semReferencia = VersaoComDocumento("""{"documentosExigidos": {"dataDeReferenciaDosDados": "31/01/2026", "exigencias": []}}""");

        Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>> projetados =
            ProjecaoDoFormularioDaVersao.ProjetarDaInscricao(semReferencia, RegistroReidratando(envelope));

        projetados.IsFailure.Should().BeTrue();
        projetados.Value.Should().BeNull();
    }
}
