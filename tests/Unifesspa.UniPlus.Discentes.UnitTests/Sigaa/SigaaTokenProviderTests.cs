namespace Unifesspa.UniPlus.Discentes.UnitTests.Sigaa;

using System.Net;
using System.Net.Http;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Authentication;

public sealed class SigaaTokenProviderTests : IDisposable
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 2, 3, 0, 0, TimeSpan.Zero);

    private readonly List<IDisposable> _descartaveis = [];

    public void Dispose()
    {
        foreach (IDisposable descartavel in _descartaveis)
        {
            descartavel.Dispose();
        }
    }

    [Fact]
    public async Task Reaproveita_o_token_enquanto_ele_vale()
    {
        RedeSimulada rede = new((_, indice) =>
            RespostasDoSigaa.ComToken(RespostasDoSigaa.TokenCom(Agora, Agora.AddHours(1), $"t{indice}")));

        SigaaTokenProvider provedor = Montar(rede, out RelogioControlado _);

        string primeiro = await provedor.ObterAsync(CancellationToken.None);
        string segundo = await provedor.ObterAsync(CancellationToken.None);

        segundo.Should().Be(primeiro);
        rede.AutenticacoesRealizadas.Should().Be(1, "o token guardado ainda vale");
    }

    [Fact]
    public async Task Renova_quando_o_token_se_aproxima_do_vencimento()
    {
        RedeSimulada rede = new((_, indice) =>
            RespostasDoSigaa.ComToken(RespostasDoSigaa.TokenCom(Agora, Agora.AddMinutes(10), $"t{indice}")));

        SigaaTokenProvider provedor = Montar(rede, out RelogioControlado relogio);

        string primeiro = await provedor.ObterAsync(CancellationToken.None);
        relogio.Avancar(TimeSpan.FromMinutes(10));
        string segundo = await provedor.ObterAsync(CancellationToken.None);

        segundo.Should().NotBe(primeiro);
        rede.AutenticacoesRealizadas.Should().Be(2);
    }

    [Fact]
    public async Task Usa_a_validade_da_configuracao_quando_o_token_nao_a_declara()
    {
        RedeSimulada rede = new((_, _) => RespostasDoSigaa.ComToken(RespostasDoSigaa.TokenSemValidade()));

        SigaaTokenProvider provedor = Montar(
            rede,
            out RelogioControlado relogio,
            new SigaaOptions
            {
                BaseUrl = "https://sigaa.exemplo.test",
                Usuario = "servico",
                Senha = "segredo",
                ValidadeAssumidaDoTokenEmSegundos = 600,
                MargemDeRenovacaoDoTokenEmSegundos = 60,
            });

        await provedor.ObterAsync(CancellationToken.None);

        relogio.Avancar(TimeSpan.FromMinutes(5));
        await provedor.ObterAsync(CancellationToken.None);
        rede.AutenticacoesRealizadas.Should().Be(1, "a validade assumida ainda não se esgotou");

        relogio.Avancar(TimeSpan.FromMinutes(5));
        await provedor.ObterAsync(CancellationToken.None);
        rede.AutenticacoesRealizadas.Should().Be(2, "passados dez minutos, a validade assumida terminou");
    }

    [Fact]
    public async Task Limita_a_validade_ao_teto_configurado()
    {
        // Expiração absurda: sem teto, o cliente fixaria este token por um século.
        RedeSimulada rede = new((_, indice) =>
            RespostasDoSigaa.ComToken(RespostasDoSigaa.TokenCom(Agora, Agora.AddYears(100), $"t{indice}")));

        SigaaTokenProvider provedor = Montar(
            rede,
            out RelogioControlado relogio,
            new SigaaOptions
            {
                BaseUrl = "https://sigaa.exemplo.test",
                Usuario = "servico",
                Senha = "segredo",
                ValidadeMaximaDoTokenEmSegundos = 300,
                MargemDeRenovacaoDoTokenEmSegundos = 30,
            });

        await provedor.ObterAsync(CancellationToken.None);
        relogio.Avancar(TimeSpan.FromMinutes(5));
        await provedor.ObterAsync(CancellationToken.None);

        rede.AutenticacoesRealizadas.Should().Be(2, "o teto de cinco minutos prevalece sobre a expiração declarada");
    }

    [Fact]
    public async Task Descarta_apenas_o_token_recusado_e_nao_o_que_ja_foi_renovado()
    {
        // Esta é a corrida que uma invalidação incondicional provocaria: várias
        // requisições saem juntas com o mesmo token vencido e todas recebem recusa. Se
        // cada uma apagasse o que encontrasse, a segunda apagaria o token que a primeira
        // acabou de renovar, e haveria uma renovação por requisição em voo.
        RedeSimulada rede = new((_, indice) =>
            RespostasDoSigaa.ComToken(RespostasDoSigaa.TokenCom(Agora, Agora.AddHours(1), $"t{indice}")));

        SigaaTokenProvider provedor = Montar(rede, out RelogioControlado _);

        string vencido = await provedor.ObterAsync(CancellationToken.None);

        provedor.Descartar(vencido);
        string renovado = await provedor.ObterAsync(CancellationToken.None);

        // A segunda requisição chega atrasada com o token velho na mão.
        provedor.Descartar(vencido);
        string depois = await provedor.ObterAsync(CancellationToken.None);

        depois.Should().Be(renovado, "o descarte do token velho não pode derrubar o já renovado");
        rede.AutenticacoesRealizadas.Should().Be(2, "houve exatamente uma renovação, não uma por requisição");
    }

    [Theory]
    [InlineData("nao-e-um-token")]
    [InlineData("dois.trechos")]
    [InlineData("cabecalho..assinatura")]
    [InlineData("cabecalho.corpo*invalido.assinatura")]
    [InlineData("cabecalho.bmFvIGVoIGpzb24.assinatura")]
    public async Task Token_em_formato_inesperado_cai_na_validade_assumida_sem_derrubar_a_autenticacao(
        string tokenEstranho)
    {
        // Um token que não seja três trechos separados por ponto, ou cujo trecho do meio
        // não decodifique, não pode derrubar a sincronização: a validade é detalhe de
        // agendamento, e não saber quando renovar apenas antecipa a renovação.
        RedeSimulada rede = new((_, _) => RespostasDoSigaa.ComToken(tokenEstranho));

        SigaaTokenProvider provedor = Montar(rede, out RelogioControlado _);

        string obtido = await provedor.ObterAsync(CancellationToken.None);

        obtido.Should().Be(tokenEstranho, "o token é repassado como veio; quem o valida é a origem");
    }

    [Theory]
    [InlineData(253_402_300_800L)]        // um segundo além da maior data representável
    [InlineData(1_800_000_000_000L)]      // expiração gravada em milissegundos, por engano
    [InlineData(-62_135_596_801L)]        // antes da menor data representável
    public async Task Instante_de_expiracao_fora_de_faixa_cai_na_validade_assumida(long expiracao)
    {
        // Um instante que não cabe na faixa de datas representáveis é mais um caso de
        // validade ilegível. Não pode impedir o token de chegar à origem — que é quem
        // decide se ele vale.
        string token = RespostasDoSigaa.TokenComExpiracaoBruta(expiracao);
        RedeSimulada rede = new((_, _) => RespostasDoSigaa.ComToken(token));

        SigaaTokenProvider provedor = Montar(rede, out RelogioControlado relogio);

        string obtido = await provedor.ObterAsync(CancellationToken.None);
        obtido.Should().Be(token);

        // E a validade assumida passa a valer: antes dela, não se renova.
        relogio.Avancar(TimeSpan.FromMinutes(1));
        await provedor.ObterAsync(CancellationToken.None);
        rede.AutenticacoesRealizadas.Should().Be(1);
    }

    [Fact]
    public async Task Recusa_de_credencial_falha_sem_expor_o_corpo_da_resposta()
    {
        const string CorpoComDadoSensivel = """{"message":"Bad credentials","username":"servico"}""";

        RedeSimulada rede = new((_, _) =>
            RespostasDoSigaa.Json(HttpStatusCode.Unauthorized, CorpoComDadoSensivel));

        RegistroDeLog registro = new();
        SigaaTokenProvider provedor = Montar(rede, out RelogioControlado _, registro: registro);

        Func<Task> tentativa = () => provedor.ObterAsync(CancellationToken.None);

        await tentativa.Should().ThrowAsync<SigaaAutenticacaoException>();
        registro.TudoQueFoiRegistrado.Should().NotContain(
            "Bad credentials",
            "o corpo da recusa ecoa o que foi enviado e não pode ir para o log");
    }

    private SigaaTokenProvider Montar(
        RedeSimulada rede,
        out RelogioControlado relogio,
        SigaaOptions? opcoes = null,
        RegistroDeLog? registro = null)
    {
        relogio = new RelogioControlado(Agora);

        SigaaOptions efetivas = opcoes ?? new SigaaOptions
        {
            BaseUrl = "https://sigaa.exemplo.test",
            Usuario = "servico",
            Senha = "segredo",
        };

        ServiceCollection servicos = new();
        servicos.AddHttpClient(SigaaTokenProvider.NomeDoClienteHttp, cliente =>
                cliente.BaseAddress = new Uri(efetivas.BaseUrl))
            .ConfigurePrimaryHttpMessageHandler(() => rede);

        // O provedor precisa continuar vivo enquanto o teste roda: a fábrica de clientes
        // abre um escopo nele a cada cliente criado, e descartá-lo aqui derrubaria a
        // primeira autenticação.
        ServiceProvider provedorDeServicos = servicos.BuildServiceProvider();
        _descartaveis.Add(provedorDeServicos);

        ILogger<SigaaTokenProvider> log;
        if (registro is null)
        {
            log = NullLogger<SigaaTokenProvider>.Instance;
        }
        else
        {
            LoggerFactory fabrica = new([registro]);
            _descartaveis.Add(fabrica);
            log = fabrica.CreateLogger<SigaaTokenProvider>();
        }

        SigaaTokenProvider provedor = new(
            provedorDeServicos.GetRequiredService<IHttpClientFactory>(),
            Options.Create(efetivas),
            log,
            relogio);

        _descartaveis.Add(provedor);
        return provedor;
    }
}
