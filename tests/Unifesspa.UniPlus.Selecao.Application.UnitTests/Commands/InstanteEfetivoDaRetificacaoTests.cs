namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

/// <summary>
/// A retificação lê um instante efetivo só, o maior entre o relógio e a vigência da versão que sucede, e é nele que a
/// versão, o registro do ato do processo, o ato no registro central e a divulgação do certame são emitidos. Com o
/// relógio atrás da vigência, o instante cru ordenaria a versão nova antes da que ela emenda.
/// </summary>
public sealed class InstanteEfetivoDaRetificacaoTests
{
    [Theory(DisplayName = "Retificar emite a versão, o ato e a divulgação no maior entre o relógio e a vigência da versão anterior")]
    [InlineData(-30, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(30, 30)]
    public async Task Retificar_EmiteTudoNoInstanteEfetivo(int minutosDoRelogioSobreAVigencia, int minutosEfetivosSobreAVigencia)
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.Servicos.ComRelogio(new RelogioFixo(Agora.AddMinutes(minutosDoRelogioSobreAVigencia)));
        DateTimeOffset efetivo = Agora.AddMinutes(minutosEfetivosSobreAVigencia);

        (Result resposta, IEnumerable<object> _) = await cenario.RetificarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        await ConferirEmitidoNoInstanteAsync(cenario.Servicos, efetivo);
    }

    [Fact(DisplayName = "Retificar com o relógio atrás da vigência emite a versão seguinte no instante da anterior, e a mais nova prevalece pelo número")]
    public async Task Retificar_RelogioRegressivo_EmpataComAAnteriorENumeraASucessora()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.Servicos.ComRelogio(new RelogioFixo(Agora.AddHours(-2)));
        VersaoConfiguracao? emitida = null;
        await cenario.Servicos.Repositorio.AdicionarVersaoConfiguracaoAsync(Arg.Do<VersaoConfiguracao>(v => emitida = v), Arg.Any<CancellationToken>());

        (Result resposta, IEnumerable<object> _) = await cenario.RetificarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        emitida!.NumeroVersao.Should().Be(2);
        emitida.VigenteAPartirDe.Should().Be(Agora, "a vigência nunca precede a da versão que ela emenda");
    }

    [Fact(DisplayName = "A conferência do ato e do documento da retificação entrega às demais conferências o instante efetivo e o dia civil dele")]
    public async Task ConferirAtoEDocumento_Retificacao_EntregaOInstanteEfetivo()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao vigente) = ProcessoPublicado();
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo)
            .ComRelogio(new RelogioFixo(vigente.VigenteAPartirDe.AddHours(-10)));
        OrigemDoCongelamento retificacao = new RetificacaoDeAtoUnico(
            new BaseDaRetificacao(vigente, "Correção do prazo", servicos.Codecs.Reidratar(vigente)));

        Result<AtoEDocumentoConferidos> conferidos = await servicos.AtoEDocumento()
            .ConferirAsync(processo.Id, new EditalDeclarado(null, null, null, Guid.CreateVersion7(), null), NovoAto(), retificacao, CancellationToken.None);

        conferidos.Value.Publicacao.Instante.Should().Be(vigente.VigenteAPartirDe);
        conferidos.Value.Publicacao.Dia.Should().Be(new DateOnly(2026, 1, 1));
    }

    [Fact(DisplayName = "A conferência do ato e do documento da primeira publicação entrega o instante do relógio, pois não há versão anterior")]
    public async Task ConferirAtoEDocumento_PrimeiraPublicacao_EntregaOInstanteDoRelogio()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo)
            .ComRelogio(new RelogioFixo(Agora.AddHours(-10)));

        Result<AtoEDocumentoConferidos> conferidos = await servicos.AtoEDocumento()
            .ConferirAsync(processo.Id, new EditalDeclarado(null, null, null, Guid.CreateVersion7(), null), NovoAto(), new PrimeiraPublicacao(), CancellationToken.None);

        conferidos.Value.Publicacao.Instante.Should().Be(Agora.AddHours(-10));
    }

    private static async Task ConferirEmitidoNoInstanteAsync(
        ServicosDoCongelamentoDeTeste servicos, DateTimeOffset efetivo)
    {
        await servicos.Repositorio.Received(1).AdicionarVersaoConfiguracaoAsync(
            Arg.Is<VersaoConfiguracao>(v => v.VigenteAPartirDe == efetivo), Arg.Any<CancellationToken>());
        await servicos.Atos.Received(1).AdicionarAsync(
            Arg.Is<AtoDoProcesso>(a => a.PublicadoEm == efetivo), Arg.Any<CancellationToken>());
        await servicos.Registro.Received(1).RegistrarAsync(
            Arg.Is<AtoNormativoPublicado>(a => a.PublicadoEm == efetivo), Arg.Any<CancellationToken>());
        await servicos.Certames.Received(1).AdicionarAsync(
            Arg.Is<CertameDivulgado>(c => c.DivulgadoEm == efetivo), Arg.Any<CancellationToken>());
    }
}
