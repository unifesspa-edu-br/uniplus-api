namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// A retificação pelo comando real grava a versão seguinte, o registro do ato, o ato no registro central, a divulgação
/// do certame e as mensagens dos eventos numa única transação: ou tudo existe, ou nada. Quem lê de outra conexão enxerga
/// a versão anterior até a confirmação, e a seguinte depois dela.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class AtomicidadeDaRetificacaoTests
{
    private readonly CascadingFixture _fixture;

    public AtomicidadeDaRetificacaoTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Retificar grava a versão 2, o ato e a divulgação na mesma transação e no mesmo instante, e as mensagens de evento ficam gravadas, pendentes de envio, nessa mesma transação")]
    public async Task Retificar_GravaTudoNaMesmaTransacaoENoMesmoInstante()
    {
        await using CenarioDaRetificacaoAtomica cenario = await CenarioDaRetificacaoAtomica.PublicadoAsync(_fixture, nameof(Retificar_GravaTudoNaMesmaTransacaoENoMesmoInstante));
        cenario.Controle.Armar(cenario.ProcessoId, ComportamentoNaConfirmacao.Observar);

        Result resultado = await cenario.RetificarAsync(await cenario.SemearDocumentoAsync());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        (await cenario.Controle.AguardarObservadoAsync()).MensagensPendentesDeEnvio.Should().BePositive("as mensagens de evento já estavam gravadas, pendentes de envio, antes da confirmação");
        await ConferirMesmaTransacaoEMesmoInstanteAsync(cenario);
    }

    [Fact(DisplayName = "A falha depois das gravações e antes da confirmação não deixa a versão 2, o ato nem mensagem pendente, e a divulgação e a leitura seguem na versão 1")]
    public async Task FalhaAntesDaConfirmacao_NaoDeixaNada()
    {
        await using CenarioDaRetificacaoAtomica cenario = await CenarioDaRetificacaoAtomica.PublicadoAsync(_fixture, nameof(FalhaAntesDaConfirmacao_NaoDeixaNada));
        Guid documento = await cenario.SemearDocumentoAsync();
        cenario.Controle.Armar(cenario.ProcessoId, ComportamentoNaConfirmacao.Falhar);

        Func<Task> retificar = () => cenario.RetificarAsync(documento);

        await retificar.Should().ThrowAsync<Exception>();
        ConteudoDaTransacaoDaRetificacao naTransacao = await cenario.Controle.AguardarObservadoAsync();
        ConferirTudoGravadoDentroDaTransacao(naTransacao);
        await ConferirSomenteAVersaoUmAsync(cenario, naTransacao);
    }

    [Fact(DisplayName = "Duas retificações concorrentes terminam serializadas em versões sucessivas, sem ramificar a cadeia")]
    public async Task DuasRetificacoesConcorrentes_SerializamEmVersoesSucessivas()
    {
        await using CenarioDaRetificacaoAtomica cenario = await CenarioDaRetificacaoAtomica.PublicadoAsync(_fixture, nameof(DuasRetificacoesConcorrentes_SerializamEmVersoesSucessivas));
        Guid primeiro = await cenario.SemearDocumentoAsync();
        Guid segundo = await cenario.SemearDocumentoAsync();
        await EstadoDaRetificacaoNoBanco.MensagensPendentesAssentadasAsync(cenario.Servicos, cenario.ProcessoId, cenario.AtoDaAbertura);
        cenario.Controle.Armar(cenario.ProcessoId, ComportamentoNaConfirmacao.Reter);

        Task<Result> primeira = cenario.RetificarAsync(primeiro);
        ConteudoDaTransacaoDaRetificacao retida = await cenario.Controle.AguardarObservadoAsync();
        Task<Result> segunda = cenario.RetificarAsync(segundo);
        await ConferirQueASegundaEsperaAPrimeiraAsync(cenario, retida.PidDaConexao, segundo, segunda);
        cenario.Controle.Liberar();

        (await Task.WhenAll(primeira, segunda)).Should().OnlyContain(r => r.IsSuccess, "a segunda segue assim que a primeira confirma e solta a trava");
        await ConferirCadeiaLinearAsync(cenario);
    }

    [Fact(DisplayName = "A retificação recusada pela vaga do objeto depois de a versão e o ato serem gravados não deixa versão, ato nem mensagem pendente, e a divulgação não muda")]
    public async Task RecusaDaVagaDoObjeto_NaoDeixaResiduo()
    {
        await using CenarioDaRetificacaoAtomica cenario = await CenarioDaRetificacaoAtomica.PublicadoAsync(_fixture, nameof(RecusaDaVagaDoObjeto_NaoDeixaResiduo));
        string tipoUnico = await cenario.SemearTipoDeAtoUnicoPorObjetoAsync();
        await cenario.OcuparAVagaDoObjetoAsync(tipoUnico);
        IReadOnlyList<string> mensagensAntes = await EstadoDaRetificacaoNoBanco.MensagensPendentesAssentadasAsync(cenario.Servicos, cenario.ProcessoId, cenario.AtoDaAbertura);

        Result recusa = await cenario.RetificarAsync(await cenario.SemearDocumentoAsync(), tipoUnico);

        recusa.Error!.Code.Should().Be(AtoNormativoErrorCodes.ObjetoJaTemAtoVivoDoTipo);
        await ConferirNadaMudouAsync(cenario, mensagensAntes);
    }

    [Fact(DisplayName = "Quem lê por outra conexão com a confirmação retida vê só a versão 1 e o ato 1, e depois de soltá-la vê a versão 2")]
    public async Task LeituraComAConfirmacaoRetida_VeSoAVersaoUm_EDepoisAVersaoDois()
    {
        await using CenarioDaRetificacaoAtomica cenario = await CenarioDaRetificacaoAtomica.PublicadoAsync(_fixture, nameof(LeituraComAConfirmacaoRetida_VeSoAVersaoUm_EDepoisAVersaoDois));
        Guid documento = await cenario.SemearDocumentoAsync();
        cenario.Controle.Armar(cenario.ProcessoId, ComportamentoNaConfirmacao.Reter);

        Task<Result> retificacao = cenario.RetificarAsync(documento);
        ConteudoDaTransacaoDaRetificacao retida = await cenario.Controle.AguardarObservadoAsync();
        await ConferirSoAVersaoUmNasTresLeiturasAsync(cenario, retida);
        cenario.Controle.Liberar();

        (await retificacao).IsSuccess.Should().BeTrue();
        await ConferirAVersaoDoisNasTresLeiturasAsync(cenario, retida, documento);
    }

    private static async Task ConferirQueASegundaEsperaAPrimeiraAsync(CenarioDaRetificacaoAtomica cenario, int pidDaPrimeira, Guid documentoDaSegunda, Task<Result> segunda)
    {
        await EstadoDaRetificacaoNoBanco.AguardarSessaoEsperandoPelaTravaDeAsync(
            cenario.Servicos, pidDaPrimeira, () => cenario.Conexoes.ProcessosDoBancoDe(documentoDaSegunda), TimeSpan.FromSeconds(10));

        segunda.IsCompleted.Should().BeFalse("a primeira ainda tem a trava do processo e a segunda espera por ela");
        (await EstadoDaRetificacaoNoBanco.VersoesAsync(cenario.Servicos, cenario.ProcessoId)).Should().ContainSingle("a segunda só grava depois de a primeira confirmar");
    }

    private static async Task ConferirMesmaTransacaoEMesmoInstanteAsync(CenarioDaRetificacaoAtomica cenario)
    {
        VersaoConfiguracao versao = (await EstadoDaRetificacaoNoBanco.VersoesAsync(cenario.Servicos, cenario.ProcessoId)).Single(v => v.NumeroVersao == 2);
        AtoDoProcesso ato = await EstadoDaRetificacaoNoBanco.AtoDoProcessoAsync(cenario.Servicos, versao.AtoCriadorId);
        CertameDivulgado divulgado = await EstadoDaRetificacaoNoBanco.DivulgacaoAsync(cenario.Servicos, cenario.ProcessoId);

        divulgado.NumeroVersao.Should().Be(2);
        divulgado.AtoCriadorId.Should().Be(versao.AtoCriadorId);
        divulgado.DivulgadoEm.Should().Be(versao.VigenteAPartirDe).And.Be(ato.PublicadoEm);
        (await EstadoDaRetificacaoNoBanco.TransacoesDaRetificacaoAsync(cenario.Servicos, cenario.ProcessoId, versao.AtoCriadorId))
            .Distinct().Should().ContainSingle("a versão, o ato do processo, o ato central e a divulgação foram gravados pela mesma transação");
    }

    private static void ConferirTudoGravadoDentroDaTransacao(ConteudoDaTransacaoDaRetificacao naTransacao)
    {
        naTransacao.AtoDoProcessoGravado.Should().BeTrue("o registro do ato já estava na transação quando ela falhou");
        naTransacao.AtoNormativoGravado.Should().BeTrue("o ato central já estava na transação quando ela falhou");
        naTransacao.NumeroDaVersaoDivulgada.Should().Be(2, "a divulgação avançada já estava na transação quando ela falhou");
        naTransacao.MensagensPendentesDeEnvio.Should().BePositive("as mensagens de evento já estavam gravadas na transação quando ela falhou");
    }

    private static async Task ConferirSomenteAVersaoUmAsync(CenarioDaRetificacaoAtomica cenario, ConteudoDaTransacaoDaRetificacao naTransacao)
    {
        IServiceProvider servicos = cenario.Servicos;

        (await EstadoDaRetificacaoNoBanco.VersoesAsync(servicos, cenario.ProcessoId)).Should().ContainSingle().Which.NumeroVersao.Should().Be(1);
        (await EstadoDaRetificacaoNoBanco.AtoDoProcessoExisteAsync(servicos, naTransacao.AtoDaVersaoDois)).Should().BeFalse("não restou o ato do processo da versão 2");
        (await EstadoDaRetificacaoNoBanco.AtoNormativoExisteAsync(servicos, naTransacao.AtoDaVersaoDois)).Should().BeFalse("não restou o ato central da versão 2");
        (await EstadoDaRetificacaoNoBanco.MensagensPendentesQueCitamAsync(servicos, naTransacao.AtoDaVersaoDois)).Should().Be(0, "nenhuma mensagem da versão 2 ficou pendente depois da falha");
        (await EstadoDaRetificacaoNoBanco.DivulgacaoAsync(servicos, cenario.ProcessoId)).AtoCriadorId.Should().Be(cenario.AtoDaAbertura);
        (await LeiturasDoCertameNaRetificacao.NumeroDaVersaoVigenteAsync(servicos, cenario.ProcessoId, DateTimeOffset.UtcNow)).Should().Be(1);
    }

    private static async Task ConferirCadeiaLinearAsync(CenarioDaRetificacaoAtomica cenario)
    {
        List<VersaoConfiguracao> versoes = await EstadoDaRetificacaoNoBanco.VersoesAsync(cenario.Servicos, cenario.ProcessoId);
        CertameDivulgado divulgado = await EstadoDaRetificacaoNoBanco.DivulgacaoAsync(cenario.Servicos, cenario.ProcessoId);

        versoes.Select(v => v.NumeroVersao).Should().Equal(1, 2, 3);
        versoes.Skip(1).Zip(versoes, (atual, anterior) => atual.AtoCriadorRetificaId == anterior.AtoCriadorId)
            .Should().OnlyContain(emendaAAnterior => emendaAAnterior, "cada versão emenda o ato da versão anterior, sem ramificar");
        versoes.Skip(1).Zip(versoes, (atual, anterior) => atual.VigenteAPartirDe >= anterior.VigenteAPartirDe)
            .Should().OnlyContain(naoPrecede => naoPrecede, "a vigência de uma versão nunca precede a da anterior");
        divulgado.AtoCriadorId.Should().Be(versoes[^1].AtoCriadorId, "a divulgação avança junto com a última versão");
    }

    private static async Task ConferirNadaMudouAsync(CenarioDaRetificacaoAtomica cenario, IReadOnlyList<string> mensagensAntes)
    {
        IServiceProvider servicos = cenario.Servicos;

        (await EstadoDaRetificacaoNoBanco.VersoesAsync(servicos, cenario.ProcessoId)).Should().ContainSingle().Which.NumeroVersao.Should().Be(1);
        (await EstadoDaRetificacaoNoBanco.AtosDoProcessoAsync(servicos, cenario.ProcessoId)).Should().Be(1, "só o ato da abertura existe");
        (await EstadoDaRetificacaoNoBanco.RetificacoesNoRegistroCentralAsync(servicos, cenario.AtoDaAbertura)).Should().Be(0);
        CertameDivulgado divulgado = await EstadoDaRetificacaoNoBanco.DivulgacaoAsync(servicos, cenario.ProcessoId);
        divulgado.NumeroVersao.Should().Be(1);
        divulgado.AtoCriadorId.Should().Be(cenario.AtoDaAbertura);
        (await EstadoDaRetificacaoNoBanco.MensagensPendentesDoProcessoAsync(servicos, cenario.ProcessoId))
            .Should().BeEquivalentTo(mensagensAntes, "a recusa não deixou mensagem pendente além das que já existiam");
    }

    private static async Task ConferirSoAVersaoUmNasTresLeiturasAsync(CenarioDaRetificacaoAtomica cenario, ConteudoDaTransacaoDaRetificacao retida)
    {
        (await LeiturasDoCertameNaRetificacao.NumeroDaVersaoVigenteAsync(cenario.Servicos, cenario.ProcessoId, retida.VigenciaDaVersaoDois)).Should().Be(1);
        (await LeiturasDoCertameNaRetificacao.DocumentoDivulgadoAsync(cenario.Cliente, cenario.ProcessoId)).Should().Be(cenario.DocumentoDaAbertura);
        (await LeiturasDoCertameNaRetificacao.AtosListadosAsync(cenario.Cliente, cenario.ProcessoId)).Should().ContainSingle().Which.Should().Be(cenario.AtoDaAbertura);
    }

    private static async Task ConferirAVersaoDoisNasTresLeiturasAsync(CenarioDaRetificacaoAtomica cenario, ConteudoDaTransacaoDaRetificacao retida, Guid documentoDaRetificacao)
    {
        (await LeiturasDoCertameNaRetificacao.NumeroDaVersaoVigenteAsync(cenario.Servicos, cenario.ProcessoId, retida.VigenciaDaVersaoDois)).Should().Be(2);
        (await LeiturasDoCertameNaRetificacao.DocumentoDivulgadoAsync(cenario.Cliente, cenario.ProcessoId)).Should().Be(documentoDaRetificacao);
        (await LeiturasDoCertameNaRetificacao.AtosListadosAsync(cenario.Cliente, cenario.ProcessoId)).Should().BeEquivalentTo([cenario.AtoDaAbertura, retida.AtoDaVersaoDois]);
    }
}
