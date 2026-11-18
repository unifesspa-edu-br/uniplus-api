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

    private static string CorpoValidoPor(int segundos) =>
        RespostasDoSigaa.CodificarCorpo(
            $$"""{"exp":{{Agora.AddSeconds(segundos).ToUnixTimeSeconds()}},"iat":{{Agora.ToUnixTimeSeconds()}}}""");

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
        RedeSimulada rede = RedeComTokenQueExpiraEm(Agora.AddHours(1));
        SigaaTokenProvider provedor = Montar(rede, out RelogioControlado _);

        string primeiro = await provedor.ObterAsync(CancellationToken.None);
        string segundo = await provedor.ObterAsync(CancellationToken.None);

        segundo.Should().Be(primeiro);
        rede.AutenticacoesRealizadas.Should().Be(1, "o token guardado ainda vale");
    }

    [Fact]
    public async Task Renova_quando_o_token_se_aproxima_do_vencimento()
    {
        RedeSimulada rede = RedeComTokenQueExpiraEm(Agora.AddMinutes(10));
        SigaaTokenProvider provedor = Montar(rede, out RelogioControlado relogio);

        string primeiro = await provedor.ObterAsync(CancellationToken.None);
        string segundo = await ObterDepoisDeAsync(provedor, relogio, TimeSpan.FromMinutes(10));

        segundo.Should().NotBe(primeiro);
        rede.AutenticacoesRealizadas.Should().Be(2);
    }

    [Fact]
    public async Task Usa_a_validade_da_configuracao_quando_o_token_nao_a_declara()
    {
        RedeSimulada rede = new((_, _) => RespostasDoSigaa.ComToken(RespostasDoSigaa.TokenSemValidade()));
        SigaaTokenProvider provedor = Montar(rede, out RelogioControlado relogio, OpcoesDoSigaa.Com(
            (nameof(SigaaOptions.ValidadeAssumidaDoTokenEmSegundos), "600"),
            (nameof(SigaaOptions.MargemDeRenovacaoDoTokenEmSegundos), "60")));

        await provedor.ObterAsync(CancellationToken.None);

        await ObterDepoisDeAsync(provedor, relogio, TimeSpan.FromMinutes(5));
        rede.AutenticacoesRealizadas.Should().Be(1, "a validade assumida ainda não se esgotou");

        await ObterDepoisDeAsync(provedor, relogio, TimeSpan.FromMinutes(5));
        rede.AutenticacoesRealizadas.Should().Be(2, "passados dez minutos, a validade assumida terminou");
    }

    /// <summary>
    /// Expiração absurda: sem limite, o cliente fixaria este token por um século.
    /// </summary>
    [Fact]
    public async Task Limita_a_validade_ao_limite_configurado()
    {
        RedeSimulada rede = RedeComTokenQueExpiraEm(Agora.AddYears(100));
        SigaaTokenProvider provedor = Montar(rede, out RelogioControlado relogio, OpcoesDoSigaa.Com(
            (nameof(SigaaOptions.ValidadeMaximaDoTokenEmSegundos), "300"),
            (nameof(SigaaOptions.MargemDeRenovacaoDoTokenEmSegundos), "30")));

        await provedor.ObterAsync(CancellationToken.None);
        await ObterDepoisDeAsync(provedor, relogio, TimeSpan.FromMinutes(5));

        rede.AutenticacoesRealizadas.Should().Be(2, "o limite de cinco minutos prevalece sobre a expiração declarada");
    }

    /// <summary>
    /// Esta é a corrida que uma invalidação incondicional provocaria: várias requisições
    /// saem juntas com o mesmo token vencido e todas recebem recusa. Se cada uma apagasse o
    /// que encontrasse, a segunda apagaria o token que a primeira acabou de renovar, e
    /// haveria uma renovação por requisição em voo.
    /// </summary>
    [Fact]
    public async Task Descarta_apenas_o_token_recusado_e_nao_o_que_ja_foi_renovado()
    {
        RedeSimulada rede = RedeComTokenQueExpiraEm(Agora.AddHours(1));
        SigaaTokenProvider provedor = Montar(rede, out RelogioControlado _);

        string vencido = await provedor.ObterAsync(CancellationToken.None);
        provedor.Descartar(vencido);
        string renovado = await provedor.ObterAsync(CancellationToken.None);

        provedor.Descartar(vencido);
        string depois = await provedor.ObterAsync(CancellationToken.None);

        depois.Should().Be(renovado, "o descarte do token velho não pode derrubar o já renovado");
        rede.AutenticacoesRealizadas.Should().Be(2, "houve exatamente uma renovação, não uma por requisição");
    }

    /// <summary>
    /// Um token que não seja três trechos separados por ponto, ou cujo trecho do meio não
    /// decodifique, não pode derrubar a sincronização: a validade é detalhe de agendamento,
    /// e não saber quando renovar apenas antecipa a renovação.
    /// </summary>
    [Theory]
    [InlineData("nao-e-um-token")]
    [InlineData("dois.trechos")]
    [InlineData("cabecalho..assinatura")]
    [InlineData("cabecalho.corpo*invalido.assinatura")]
    [InlineData("cabecalho.bmFvIGVoIGpzb24.assinatura")]
    public async Task Token_em_formato_inesperado_cai_na_validade_assumida_sem_derrubar_a_autenticacao(
        string tokenEstranho)
    {
        RedeSimulada rede = new((_, _) => RespostasDoSigaa.ComToken(tokenEstranho));
        SigaaTokenProvider provedor = Montar(rede, out RelogioControlado _);

        string obtido = await provedor.ObterAsync(CancellationToken.None);

        obtido.Should().Be(tokenEstranho, "o token é repassado como veio; quem o valida é a origem");
    }

    /// <summary>
    /// Um instante que não cabe na faixa de datas representáveis é mais um caso de validade
    /// ilegível. Não pode impedir o token de chegar à origem — que é quem decide se ele vale
    /// — e a validade assumida passa a valer: antes dela, não se renova.
    /// </summary>
    [Theory]
    [InlineData(253_402_300_800L)]        // um segundo além da maior data representável
    [InlineData(1_800_000_000_000L)]      // expiração gravada em milissegundos, por engano
    [InlineData(-62_135_596_801L)]        // antes da menor data representável
    public async Task Instante_de_expiracao_fora_de_faixa_cai_na_validade_assumida(long expiracao)
    {
        string token = RespostasDoSigaa.TokenComExpiracaoBruta(expiracao);
        RedeSimulada rede = new((_, _) => RespostasDoSigaa.ComToken(token));
        SigaaTokenProvider provedor = Montar(rede, out RelogioControlado relogio);

        string obtido = await provedor.ObterAsync(CancellationToken.None);
        await ObterDepoisDeAsync(provedor, relogio, TimeSpan.FromMinutes(1));

        obtido.Should().Be(token);
        rede.AutenticacoesRealizadas.Should().Be(1);
    }

    public static readonly TheoryData<string> TokensSemInstantesLegiveis = new()
    {
        RespostasDoSigaa.TokenComCorpo("{}"),
        RespostasDoSigaa.TokenComCorpo("""{"iat":1700000000}"""),
        RespostasDoSigaa.TokenComCorpo("""{"exp":99999999999999999999}"""),
        RespostasDoSigaa.TokenComCorpo("""{"exp":1.5}"""),
        $"cabecalho.{RespostasDoSigaa.CodificarCorpo("{}")}",
    };

    public static readonly TheoryData<string, int> TokensLidosPeloTrechoCerto = new()
    {
        { $"cabecalho.{CorpoValidoPor(600)}", 600 },
        { $"cabecalho.{CorpoValidoPor(600)}.assinatura", 600 },
        { $"cabecalho.{CorpoValidoPor(600)}.assinatura.sobra", 600 },
        { RespostasDoSigaa.TokenComCorpo($$"""{"exp":{{Agora.AddSeconds(900).ToUnixTimeSeconds()}},"iat":99999999999999999999}"""), 900 },
    };

    public static readonly TheoryData<string> TokensComTipoJsonErrado = new()
    {
        RespostasDoSigaa.TokenComCorpo("""{"exp":"abc"}"""),
        RespostasDoSigaa.TokenComCorpo("""{"exp":null}"""),
        RespostasDoSigaa.TokenComCorpo("""{"exp":{}}"""),
        RespostasDoSigaa.TokenComCorpo("""{"exp":true}"""),
        RespostasDoSigaa.TokenComCorpo("""{"exp":1,"iat":"abc"}"""),
        RespostasDoSigaa.TokenComCorpo("""{"exp":1,"iat":null}"""),
        RespostasDoSigaa.TokenComCorpo("[1]"),
        RespostasDoSigaa.TokenComCorpo("5"),
        RespostasDoSigaa.TokenComCorpo("\"texto\""),
        RespostasDoSigaa.TokenComCorpo("null"),
    };

    [Theory]
    [MemberData(nameof(TokensSemInstantesLegiveis))]
    public async Task Token_sem_instantes_legiveis_vale_pela_validade_assumida(string token)
    {
        RedeSimulada rede = new((_, _) => RespostasDoSigaa.ComToken(token));
        SigaaTokenProvider provedor = Montar(rede, out RelogioControlado relogio);

        await provedor.ObterAsync(CancellationToken.None);
        await ObterDepoisDeAsync(provedor, relogio, TimeSpan.FromSeconds(3539));
        rede.AutenticacoesRealizadas.Should().Be(1, "a validade assumida é de uma hora, menos a margem");

        await ObterDepoisDeAsync(provedor, relogio, TimeSpan.FromSeconds(1));
        rede.AutenticacoesRealizadas.Should().Be(2);
    }

    [Theory]
    [MemberData(nameof(TokensLidosPeloTrechoCerto))]
    public async Task Os_instantes_sao_lidos_do_trecho_que_vem_depois_do_primeiro_ponto(string token, int validade)
    {
        RedeSimulada rede = new((_, _) => RespostasDoSigaa.ComToken(token));
        SigaaTokenProvider provedor = Montar(rede, out RelogioControlado relogio);

        await provedor.ObterAsync(CancellationToken.None);
        await ObterDepoisDeAsync(provedor, relogio, TimeSpan.FromSeconds(validade - 61));
        rede.AutenticacoesRealizadas.Should().Be(1, "falta um segundo para entrar na margem de renovação");

        await ObterDepoisDeAsync(provedor, relogio, TimeSpan.FromSeconds(1));
        rede.AutenticacoesRealizadas.Should().Be(2);
    }

    /// <summary>
    /// Instante com tipo JSON que não é número, ou corpo que não é objeto, não cai na validade
    /// assumida: a leitura lança, e a autenticação falha com a exceção da própria leitura.
    /// </summary>
    [Theory]
    [MemberData(nameof(TokensComTipoJsonErrado))]
    public async Task Token_com_tipo_json_errado_continua_lancando(string token)
    {
        RedeSimulada rede = new((_, _) => RespostasDoSigaa.ComToken(token));
        SigaaTokenProvider provedor = Montar(rede, out RelogioControlado _);

        Func<Task> obter = () => provedor.ObterAsync(CancellationToken.None);

        await obter.Should().ThrowExactlyAsync<InvalidOperationException>();
    }

    /// <summary>
    /// Sem emissão declarada, a validade vem da diferença entre a expiração e o relógio
    /// local: dez minutos, menos a margem de renovação de um minuto.
    /// </summary>
    [Fact]
    public async Task Sem_emissao_declarada_a_validade_vem_do_relogio_local()
    {
        string token = RespostasDoSigaa.TokenComExpiracaoBruta(Agora.AddMinutes(10).ToUnixTimeSeconds());
        RedeSimulada rede = new((_, _) => RespostasDoSigaa.ComToken(token));
        SigaaTokenProvider provedor = Montar(rede, out RelogioControlado relogio);

        await provedor.ObterAsync(CancellationToken.None);

        await ObterDepoisDeAsync(provedor, relogio, TimeSpan.FromMinutes(8));
        rede.AutenticacoesRealizadas.Should().Be(1, "ainda faltam dois minutos, mais que a margem");

        await ObterDepoisDeAsync(provedor, relogio, TimeSpan.FromMinutes(1.5));
        rede.AutenticacoesRealizadas.Should().Be(2, "restam trinta segundos, menos que a margem");
    }

    /// <summary>
    /// Uma expiração que já passou, sem emissão para comparar, não serve de validade: vale a
    /// assumida.
    /// </summary>
    [Fact]
    public async Task Expiracao_ja_vencida_sem_emissao_cai_na_validade_assumida()
    {
        string token = RespostasDoSigaa.TokenComExpiracaoBruta(Agora.AddSeconds(-10).ToUnixTimeSeconds());
        RedeSimulada rede = new((_, _) => RespostasDoSigaa.ComToken(token));
        SigaaTokenProvider provedor = Montar(rede, out RelogioControlado relogio);

        await provedor.ObterAsync(CancellationToken.None);
        await ObterDepoisDeAsync(provedor, relogio, TimeSpan.FromMinutes(30));

        rede.AutenticacoesRealizadas.Should().Be(1, "a validade assumida é de uma hora");
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

    private static RedeSimulada RedeComTokenQueExpiraEm(DateTimeOffset expiraEm) =>
        new((_, indice) =>
            RespostasDoSigaa.ComToken(RespostasDoSigaa.TokenCom(Agora, expiraEm, $"t{indice}")));

    private static async Task<string> ObterDepoisDeAsync(
        SigaaTokenProvider provedor,
        RelogioControlado relogio,
        TimeSpan quanto)
    {
        relogio.Avancar(quanto);

        return await provedor.ObterAsync(CancellationToken.None);
    }

    private SigaaTokenProvider Montar(
        RedeSimulada rede,
        out RelogioControlado relogio,
        SigaaOptions? opcoes = null,
        RegistroDeLog? registro = null)
    {
        relogio = new RelogioControlado(Agora);
        SigaaOptions efetivas = opcoes ?? OpcoesDoSigaa.Com();

        SigaaTokenProvider provedor = new(
            FabricaDeClientes(rede, efetivas),
            Options.Create(efetivas),
            LoggerDoProvedor(registro),
            relogio);

        _descartaveis.Add(provedor);
        return provedor;
    }

    private IHttpClientFactory FabricaDeClientes(RedeSimulada rede, SigaaOptions opcoes)
    {
        ServiceCollection servicos = new();
        servicos.AddHttpClient(SigaaTokenProvider.NomeDoClienteHttp, cliente => cliente.BaseAddress = opcoes.BaseUrl)
            .ConfigurePrimaryHttpMessageHandler(() => rede);

        // O provedor precisa continuar vivo enquanto o teste roda: a fábrica de clientes
        // abre um escopo nele a cada cliente criado, e descartá-lo aqui derrubaria a
        // primeira autenticação.
        ServiceProvider provedorDeServicos = servicos.BuildServiceProvider();
        _descartaveis.Add(provedorDeServicos);

        return provedorDeServicos.GetRequiredService<IHttpClientFactory>();
    }

    private ILogger<SigaaTokenProvider> LoggerDoProvedor(RegistroDeLog? registro)
    {
        if (registro is null)
        {
            return NullLogger<SigaaTokenProvider>.Instance;
        }

        LoggerFactory fabrica = new([registro]);
        _descartaveis.Add(fabrica);
        return fabrica.CreateLogger<SigaaTokenProvider>();
    }
}
