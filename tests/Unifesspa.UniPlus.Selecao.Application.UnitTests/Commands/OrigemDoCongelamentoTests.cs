namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FatoCitadoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

/// <summary>
/// O que cada origem responde às perguntas do congelamento: a primeira publicação responde com o neutro de cada uma, e
/// a retificação parte da versão vigente e lê os fatos já coletados das versões publicadas.
/// </summary>
public sealed class OrigemDoCongelamentoTests
{
    private const string MotivoDaRetificacao = "Correção do prazo";

    [Fact(DisplayName = "A primeira publicação não retifica nada nem congelou nada")]
    public void PrimeiraPublicacao_NaoRetificaNemCongelouNada()
    {
        OrigemDoCongelamento origem = new PrimeiraPublicacao();

        origem.Retificacao.Should().BeNull();
        origem.CongeladoNaVersaoVigente.Should().BeNull();
        origem.TiposJaCongelados().Value.Should().BeEmpty();
    }

    [Theory(DisplayName = "A primeira publicação não tem anexo vigente a herdar, com ou sem a lista no pedido")]
    [InlineData(true)]
    [InlineData(false)]
    public void PrimeiraPublicacao_NaoTemAnexoVigente(bool herdaOsDaVersao) =>
        new PrimeiraPublicacao().AnexosVigentes(herdaOsDaVersao).Should().BeEmpty();

    [Fact(DisplayName = "A primeira publicação não lê as versões publicadas")]
    public async Task PrimeiraPublicacao_NaoLeAsVersoesPublicadas()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo);
        OrigemDoCongelamento origem = new PrimeiraPublicacao();

        Result<FatosDasVersoesPublicadas?> fatos = await origem.FatosJaPublicadosAsync(servicos.VersoesPublicadas(), processo, CancellationToken.None);

        fatos.IsSuccess.Should().BeTrue();
        fatos.Value.Should().BeNull();
        await servicos.Repositorio.DidNotReceive().ObterVersoesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A retificação parte do ato da versão vigente, com o motivo do pedido")]
    public void Retificacao_PartaDoAtoDaVersaoVigente()
    {
        (_, VersaoConfiguracao vigente) = ProcessoPublicado();
        OrigemDoCongelamento origem = new RetificacaoDeAtoUnico(Base(vigente, CodecsDeTeste.QueReidrataSemTiposDeAto()));

        origem.Retificacao.Should().Be(new RetificacaoDoEdital(vigente.AtoCriadorId, MotivoDaRetificacao));
    }

    [Fact(DisplayName = "A versão vigente que o codec não lê não congela nada e recusa com o erro do codec")]
    public void VersaoIlegivel_NaoCongelaNadaERecusaComOErroDoCodec()
    {
        (_, VersaoConfiguracao vigente) = ProcessoPublicado();
        var erroDoCodec = new DomainError("EnvelopeCodec.VersaoDesconhecida", "A versão não é reconhecida.");
        var ilegivel = new BaseDaRetificacao(vigente, MotivoDaRetificacao, Result<EnvelopeReidratado>.Failure(erroDoCodec));

        OrigemDoCongelamento origem = new RetificacaoDeAtoUnico(ilegivel);

        origem.CongeladoNaVersaoVigente.Should().BeNull();
        origem.TiposJaCongelados().Error.Should().Be(erroDoCodec);
    }

    [Fact(DisplayName = "Sem anexo na versão vigente, a retificação não tem anexo a herdar")]
    public void VersaoSemAnexos_NaoHerdaNada()
    {
        (_, VersaoConfiguracao vigente) = ProcessoPublicado();
        OrigemDoCongelamento origem = new RetificacaoDeAtoUnico(Base(vigente, CodecsDeTeste.QueReidrataSemTiposDeAto()));

        origem.AnexosVigentes(herdaOsDaVersao: true).Should().BeEmpty();
        origem.AnexosVigentes(herdaOsDaVersao: false).Should().BeEmpty();
    }

    [Fact(DisplayName = "A versão vigente de forma inesperada recusa a herança dos anexos, e sem herança não tem anexo")]
    public void VersaoDeFormaInesperada_RecusaAHerancaDosAnexos()
    {
        (_, VersaoConfiguracao vigente) = ProcessoPublicado(Encoding.UTF8.GetBytes("{}"));
        OrigemDoCongelamento origem = new RetificacaoDeAtoUnico(Base(vigente, CodecsDeTeste.QueReidrataSemTiposDeAto()));

        Func<IReadOnlyList<AnexoDoEdital>> herdando = () => origem.AnexosVigentes(herdaOsDaVersao: true);

        herdando.Should().Throw<InvalidOperationException>();
        origem.AnexosVigentes(herdaOsDaVersao: false).Should().BeEmpty();
    }

    [Fact(DisplayName = "A retificação de ato único consulta as versões publicadas e, sem nenhuma, não tem fato já coletado")]
    public async Task RetificacaoDeAtoUnico_ConsultaAsVersoesPublicadas()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao vigente) = ProcessoPublicado();
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo);
        servicos.Repositorio.ObterVersoesAsync(processo.Id, Arg.Any<CancellationToken>()).Returns([]);
        OrigemDoCongelamento origem = new RetificacaoDeAtoUnico(Base(vigente, servicos.Codecs));

        Result<FatosDasVersoesPublicadas?> fatos = await origem.FatosJaPublicadosAsync(servicos.VersoesPublicadas(), processo, CancellationToken.None);

        fatos.IsSuccess.Should().BeTrue();
        fatos.Value.Should().BeNull();
        await servicos.Repositorio.Received(1).ObterVersoesAsync(processo.Id, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A retificação de ato único recusa com o erro do codec quando uma versão publicada não é lida")]
    public async Task RetificacaoDeAtoUnico_RecusaComOErroDoCodec()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao vigente) = ProcessoPublicado();
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo);
        servicos.Repositorio.ObterVersoesAsync(processo.Id, Arg.Any<CancellationToken>()).Returns([vigente]);
        var erroDoCodec = new DomainError("EnvelopeCodec.VersaoDesconhecida", "A versão não é reconhecida.");
        servicos.Codecs.Reidratar(Arg.Any<VersaoConfiguracao>()).Returns(Result<EnvelopeReidratado>.Failure(erroDoCodec));
        OrigemDoCongelamento origem = new RetificacaoDeAtoUnico(Base(vigente, servicos.Codecs));

        Result<FatosDasVersoesPublicadas?> fatos = await origem.FatosJaPublicadosAsync(servicos.VersoesPublicadas(), processo, CancellationToken.None);

        fatos.Error!.Code.Should().Be(erroDoCodec.Code);
    }

    [Fact(DisplayName = "A primeira publicação não tem fases de versão publicada com que comparar o cronograma")]
    public void PrimeiraPublicacao_NaoTemFasesDaVersaoPublicada() =>
        new PrimeiraPublicacao().FasesDaVersaoPublicada().Should().BeNull();

    [Fact(DisplayName = "A retificação compara o cronograma com as fases da versão vigente que o codec leu")]
    public async Task Retificacao_TemAsFasesDaVersaoVigente()
    {
        (ProcessoSeletivo processo, _) = ProcessoPublicado();
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo);
        VersaoPublicadaDeTeste.Publicar(processo, servicos.Repositorio, servicos.Codecs);
        VersaoConfiguracao vigente = (await servicos.Repositorio.ObterVersaoAtualAsync(processo.Id, CancellationToken.None))!;
        OrigemDoCongelamento origem = new RetificacaoDeAtoUnico(Base(vigente, servicos.Codecs));

        origem.FasesDaVersaoPublicada().Should().BeEquivalentTo(processo.CronogramaFases);
    }

    [Fact(DisplayName = "A versão vigente que o codec não leu não tem fases com que comparar: é falha técnica, e não recusa nem aceite")]
    public void VersaoIlegivel_NaoTemFasesParaComparar()
    {
        (_, VersaoConfiguracao vigente) = ProcessoPublicado();
        var ilegivel = new BaseDaRetificacao(vigente, MotivoDaRetificacao, Result<EnvelopeReidratado>.Failure(new DomainError("EnvelopeCodec.VersaoDesconhecida", "A versão não é reconhecida.")));
        OrigemDoCongelamento origem = new RetificacaoDeAtoUnico(ilegivel);

        Func<IReadOnlyCollection<FaseCronograma>?> ler = origem.FasesDaVersaoPublicada;

        ler.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "A primeira publicação faz nascer a versão de abertura")]
    public void PrimeiraPublicacao_FazNascerAVersaoDeAbertura() =>
        new PrimeiraPublicacao().NumeroDaVersaoQueNasce.Should().Be(VersaoConfiguracao.NumeroDaAbertura);

    [Fact(DisplayName = "A retificação faz nascer a versão que sucede a vigente")]
    public void Retificacao_FazNascerASucessoraDaVigente()
    {
        (_, VersaoConfiguracao vigente) = ProcessoPublicado();
        BaseDaRetificacao baseDaRetificacao = Base(vigente, CodecsDeTeste.QueReidrataSemTiposDeAto());

        new RetificacaoDeAtoUnico(baseDaRetificacao).NumeroDaVersaoQueNasce.Should().Be(vigente.NumeroVersao + 1);
    }

    private static BaseDaRetificacao Base(VersaoConfiguracao vigente, IRegistroCodecsEnvelope codecs) =>
        new(vigente, MotivoDaRetificacao, codecs.Reidratar(vigente));
}
