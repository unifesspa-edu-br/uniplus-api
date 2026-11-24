namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FatoCitadoDeTeste;

public sealed class ProtecaoDosItensDasVersoesPublicadasTests
{
    private const string HipoteseAtual = "EXECUCAO_POLITICAS_PUBLICAS";
    private const string FinalidadeAtual = "Finalidade corrigida no catálogo.";
    private const string DescricaoAtual = "Descrição corrigida no catálogo.";
    private const string ComponenteAtual = "MOEDA";

    [Theory(DisplayName = "Retificar conserva a hipótese e a finalidade publicadas e adota a apresentação atual, com uma ou duas versões")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retificar_FatoJaColetado_ConservaProtecaoPublicadaEAtualizaApresentacao(bool duasVersoes)
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        EnvelopeReidratado publicado = await PrepararHistoricoAsync(cenario, duasVersoes);
        FatoColetado itemPublicado = publicado.Configuracao.Coleta.Itens.Single(static f => f.FatoCodigo == Fato);
        (string hipotese, string finalidade) = (itemPublicado.HipoteseLegal, itemPublicado.FinalidadeTratamento);
        await CorrigirCatalogoAsync(cenario);

        (Result resposta, _) = await cenario.RetificarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        hipotese.Should().NotBe(HipoteseAtual);
        finalidade.Should().NotBe(FinalidadeAtual);
        FatoColetado congelado = cenario.EntradaCanonicalizada!.Processo.FatosColetados.Single(static f => f.FatoCodigo == Fato);
        (congelado.HipoteseLegal, congelado.FinalidadeTratamento, congelado.Descricao, congelado.ComponenteExibicao)
            .Should().Be((hipotese, finalidade, DescricaoAtual, ComponenteAtual));
    }

    [Fact(DisplayName = "Retificar copia a hipótese, a finalidade e a apresentação atuais do fato ausente das versões publicadas")]
    public async Task Retificar_FatoNovo_CopiaProtecaoEApresentacaoAtuais()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(new SituacaoDoFatoCitado { ComItemNovo = true });
        EnvelopeReidratado publicado = await PrepararHistoricoAsync(cenario, duasVersoes: false);
        publicado.Configuracao.Coleta.Itens.Should().NotContain(static f => f.FatoCodigo == FatoNovo);
        await CorrigirCatalogoAsync(cenario);

        (Result resposta, _) = await cenario.RetificarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        FatoColetado congelado = cenario.EntradaCanonicalizada!.Processo.FatosColetados.Single(static f => f.FatoCodigo == FatoNovo);
        (congelado.HipoteseLegal, congelado.FinalidadeTratamento, congelado.Descricao, congelado.ComponenteExibicao)
            .Should().Be((HipoteseAtual, FinalidadeAtual, DescricaoAtual, ComponenteAtual));
    }

    [Fact(DisplayName = "Retificar recusa hipótese e finalidade históricas divergentes mesmo com o item igual ao vigente")]
    public async Task Retificar_HistoricoComProtecaoDivergente_Recusa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        EnvelopeReidratado vigente = await PrepararHistoricoAsync(cenario, duasVersoes: true);
        await DivergirProtecaoDaPrimeiraVersaoAsync(cenario, vigente);

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        FieldError causa = resposta.Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be(CodigoDaRecusaDoEixoAlterado);
        causa.Error.Message.Should().Contain(Fato).And.Contain("hipótese legal");
        causa.Field.Should().Be("/formularios/0/itens/28");
        eventos.Should().BeEmpty();
        cenario.Processo.DomainEvents.Should().BeEmpty();
        await cenario.Servicos.NadaFoiGravadoAsync();
    }

    private static async Task DivergirProtecaoDaPrimeiraVersaoAsync(CenarioDeRetificacaoPublicada cenario, EnvelopeReidratado vigente)
    {
        IReadOnlyList<VersaoConfiguracao> versoes = await cenario.Servicos.Repositorio.ObterVersoesAsync(cenario.Processo.Id, CancellationToken.None);
        ProcessoSeletivo antigo = ProcessoSoComOItemPublicado(Unifesspa.UniPlus.Regras.Enums.TipoRenderizacao.SimOuNao);
        FatoColetado item = antigo.FatosColetados.Single(static f => f.FatoCodigo == Fato);
        typeof(FatoColetado).GetProperty(nameof(FatoColetado.HipoteseLegal))!.SetValue(item, HipoteseAtual);
        typeof(FatoColetado).GetProperty(nameof(FatoColetado.FinalidadeTratamento))!.SetValue(item, FinalidadeAtual);
        EnvelopeReidratado historico = new(
            Unifesspa.UniPlus.Testes.Compartilhado.VersoesPublicadasDeTeste.DoProcesso(antigo),
            vigente.Dados, vigente.HashDocumento, vigente.FusoHorario, null, null,
            new DadosDeCatalogoDaConfiguracaoPublicada(vigente.DadosDeCatalogo.MetadadosFatosCongelados));
        cenario.Servicos.Codecs.Reidratar(versoes[0]).Returns(Result<EnvelopeReidratado>.Success(historico));
    }

    private static async Task<EnvelopeReidratado> PrepararHistoricoAsync(CenarioDeRetificacaoPublicada cenario, bool duasVersoes)
    {
        VersaoConfiguracao vigente = (await cenario.Servicos.Repositorio.ObterVersaoAtualAsync(cenario.Processo.Id, CancellationToken.None))!;
        EnvelopeReidratado envelope = cenario.Servicos.Codecs.Reidratar(vigente).Value!;
        if (duasVersoes)
        {
            AcrescentarVersaoComOsMesmosEixos(cenario, vigente, envelope);
        }

        return envelope;
    }

    private static void AcrescentarVersaoComOsMesmosEixos(CenarioDeRetificacaoPublicada cenario, VersaoConfiguracao anterior, EnvelopeReidratado envelope)
    {
        VersaoConfiguracao sucessora = VersaoConfiguracao.Suceder(
            anterior, new DocumentoCongelado(anterior.ConfiguracaoCongeladaCanonica, anterior.SchemaVersion, anterior.AlgoritmoHash, anterior.AtoCriadorHash),
            Guid.CreateVersion7(), "teste", anterior.VigenteAPartirDe);
        cenario.Servicos.Codecs.Reidratar(sucessora).Returns(Result<EnvelopeReidratado>.Success(envelope));
        cenario.Servicos.Repositorio.ObterVersaoAtualAsync(cenario.Processo.Id, Arg.Any<CancellationToken>()).Returns(sucessora);
        cenario.Servicos.Repositorio.ObterVersoesAsync(cenario.Processo.Id, Arg.Any<CancellationToken>()).Returns([anterior, sucessora]);
    }

    private static async Task CorrigirCatalogoAsync(CenarioDeRetificacaoPublicada cenario)
    {
        IReadOnlyList<FatoCandidatoView> catalogo = await cenario.Servicos.CatalogoDeFatos.ListarAsync(CancellationToken.None);
        cenario.Servicos.CatalogoDeFatos.ListarAsync(Arg.Any<CancellationToken>()).Returns(
            [.. catalogo.Select(static f => f.Codigo is Fato or FatoNovo ? f with
            {
                HipoteseLegal = HipoteseAtual,
                FinalidadeTratamento = FinalidadeAtual,
                Descricao = DescricaoAtual,
                ComponenteExibicao = ComponenteAtual,
            } : f)]);
    }
}
