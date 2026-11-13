namespace Unifesspa.UniPlus.Discentes.UnitTests.Sigaa;

using System.Net;
using System.Net.Http;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Authentication;

/// <summary>
/// Exercita a cadeia de envio montada no registro real — resiliência por fora,
/// autenticação por dentro —, substituindo apenas a rede.
/// </summary>
public sealed class SigaaClientCadeiaDeEnvioTests : IDisposable
{
    private const string Endereco = "https://sigaa.exemplo.test";

    private readonly List<IDisposable> _descartaveis = [];

    public void Dispose()
    {
        foreach (IDisposable descartavel in _descartaveis)
        {
            descartavel.Dispose();
        }
    }

    [Fact]
    public async Task Carimba_a_consulta_com_o_token_obtido_na_autenticacao()
    {
        string token = TokenValidoPorUmaHora();
        OrigemSimulada origem = new(_ => token);
        ISigaaVinculoDiscenteClient cliente = Montar(origem.Rede, out _);

        await cliente.ObterPaginaAsync(new FiltroDeVinculos("G"), 1);

        origem.Rede.AutorizacoesRecebidas.Should().Contain(token, "a consulta precisa sair autenticada");
    }

    [Fact]
    public async Task Recusa_do_token_dispara_renovacao_e_uma_unica_repeticao()
    {
        string primeiro = TokenValidoPorUmaHora("velho");
        string segundo = TokenValidoPorUmaHora("novo");

        // A origem recusa a primeira consulta como se o token tivesse vencido.
        OrigemSimulada origem = new(
            ordem => ordem == 1 ? primeiro : segundo,
            ordem => ordem == 1 ? RespostasDoSigaa.Status(HttpStatusCode.Unauthorized) : RespostasDoSigaa.ColecaoVazia());
        ISigaaVinculoDiscenteClient cliente = Montar(origem.Rede, out _);

        await cliente.ObterPaginaAsync(new FiltroDeVinculos("G"), 1);

        origem.Autenticacoes.Should().Be(2, "a recusa dispara exatamente uma renovação");
        origem.Rede.AutorizacoesRecebidas.Should().Contain(segundo, "a repetição sai com o token renovado");
    }

    /// <summary>
    /// Prova a ordem da cadeia, e não há como provar isso com uma recusa de token: a política de repetição não
    /// considera recusa de autorização uma falha repetível, então ela nem chega a repetir, e a segunda autenticação
    /// viria só do manipulador de autenticação — que faria o mesmo estando por dentro ou por fora.
    /// O cenário que distingue as duas ordens é uma falha que a política DE FATO repete, com o token vencendo entre
    /// as tentativas. Com a autenticação por dentro, a segunda tentativa reentra nela, percebe o vencimento e
    /// renova. Com ela por fora, o cabeçalho teria sido carimbado uma única vez antes de a política começar, e
    /// nenhuma renovação aconteceria.
    /// </summary>
    [Fact]
    public async Task Repeticao_da_resiliencia_relê_o_token_que_venceu_no_meio_do_caminho()
    {
        RelogioControlado relogio = new(new DateTimeOffset(2026, 9, 2, 3, 0, 0, TimeSpan.Zero));
        OrigemSimulada origem = new(
            ordem => TokenQueVenceEmDoisMinutos(relogio, ordem),
            ordem => ordem == 1 ? IndisponibilidadeQueVenceOToken(relogio) : RespostasDoSigaa.ColecaoVazia());
        ISigaaVinculoDiscenteClient cliente = Montar(origem.Rede, out _, relogio: relogio);

        await cliente.ObterPaginaAsync(new FiltroDeVinculos("G"), 1);

        origem.Consultas.Should().Be(2, "a indisponibilidade momentânea foi repetida");
        origem.Autenticacoes.Should().Be(
            2,
            "a segunda tentativa reentrou na autenticação e renovou o token vencido — o que só "
            + "acontece com a autenticação por dentro da política de resiliência");
    }

    /// <summary>
    /// A origem reconhece lista pelo nome do parâmetro terminando em colchetes. Sem eles, os valores chegam como
    /// ocorrências soltas do mesmo nome e a linguagem da origem guarda só a última: o filtro valeria por uma
    /// situação, e os vínculos das outras sumiriam da sincronização sem erro nenhum.
    /// </summary>
    [Fact]
    public async Task Envia_cada_situacao_pedida_como_item_de_lista()
    {
        OrigemSimulada origem = new(_ => TokenValidoPorUmaHora());
        ISigaaVinculoDiscenteClient cliente = Montar(origem.Rede, out _);

        await cliente.ObterPaginaAsync(new FiltroDeVinculos("G", Situacoes: [1, 8, 9]), 1);

        string consulta = ConsultaDeVinculosEnviada(origem.Rede);
        foreach (int situacao in new[] { 1, 8, 9 })
        {
            consulta.Should().Contain($"status.id[]={situacao}", "cada situação pedida precisa chegar como item de lista");
        }
    }

    [Fact]
    public async Task Repete_falha_transitoria_da_origem_ate_obter_resposta()
    {
        OrigemSimulada origem = new(
            _ => TokenValidoPorUmaHora(),
            ordem => ordem == 1
                ? RespostasDoSigaa.Status(HttpStatusCode.ServiceUnavailable)
                : RespostasDoSigaa.ColecaoVazia());
        ISigaaVinculoDiscenteClient cliente = Montar(origem.Rede, out _);

        await cliente.ObterPaginaAsync(new FiltroDeVinculos("G"), 1);

        origem.Consultas.Should().Be(2, "indisponibilidade momentânea é repetida");
    }

    [Fact]
    public async Task Nao_repete_recusa_definitiva_da_origem()
    {
        OrigemSimulada origem = new(_ => TokenValidoPorUmaHora(), _ => RespostasDoSigaa.Status(HttpStatusCode.BadRequest));
        ISigaaVinculoDiscenteClient cliente = Montar(origem.Rede, out _);

        Func<Task> chamada = () => cliente.ObterPaginaAsync(new FiltroDeVinculos("G"), 1);
        await chamada.Should().ThrowAsync<Exception>();

        origem.Consultas.Should().Be(1, "filtro inválido não melhora com repetição; insistir só multiplicaria a recusa");
    }

    [Fact]
    public async Task Token_nao_aparece_no_log_nem_no_nivel_mais_detalhado()
    {
        string token = TokenValidoPorUmaHora("segredo-que-nao-pode-vazar");
        OrigemSimulada origem = new(_ => token);
        RegistroDeLog registro = new();
        ISigaaVinculoDiscenteClient cliente = Montar(origem.Rede, out _, registro);

        await cliente.ObterPaginaAsync(new FiltroDeVinculos("G"), 1);

        registro.TudoQueFoiRegistrado.Should().NotContain(
            token,
            "o registrador do cliente HTTP grava todos os cabeçalhos no nível mais detalhado; "
            + "sem a omissão explícita do cabeçalho de autorização, o token sairia inteiro");
    }

    private static bool EhAutenticacao(HttpRequestMessage requisicao) =>
        requisicao.RequestUri?.AbsolutePath.Contains("authentication_token", StringComparison.Ordinal) == true;

    private static string TokenValidoPorUmaHora(string marca = "t") =>
        RespostasDoSigaa.TokenCom(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), marca);

    /// <summary>Validade curta: o avanço do relógio da indisponibilidade a ultrapassa.</summary>
    private static string TokenQueVenceEmDoisMinutos(RelogioControlado relogio, int ordem) =>
        RespostasDoSigaa.TokenCom(relogio.GetUtcNow(), relogio.GetUtcNow().AddMinutes(2), $"token{ordem}");

    /// <summary>Indisponibilidade momentânea, que a política repete; entre uma tentativa e outra, o token vence.</summary>
    private static HttpResponseMessage IndisponibilidadeQueVenceOToken(RelogioControlado relogio)
    {
        relogio.Avancar(TimeSpan.FromMinutes(5));

        return RespostasDoSigaa.Status(HttpStatusCode.ServiceUnavailable);
    }

    private static string ConsultaDeVinculosEnviada(RedeSimulada rede) =>
        Uri.UnescapeDataString(rede.CaminhosChamados.Single(c => c.Contains("vinculo_discentes", StringComparison.Ordinal)));

    private ISigaaVinculoDiscenteClient Montar(
        RedeSimulada rede,
        out ServiceProvider provedor,
        RegistroDeLog? registro = null,
        TimeProvider? relogio = null)
    {
        ServiceCollection servicos = new();
        IConfiguration configuracao = ConfiguracaoDoSigaa();
        servicos.AddSingleton(configuracao);
        servicos.AddSingleton(relogio ?? TimeProvider.System);
        servicos.AddLogging(construtor => ConfigurarRegistroDeLog(construtor, registro));
        servicos.AddSigaaVinculoDiscenteClient(configuracao);
        UsarSomenteARede(servicos, rede);

        provedor = servicos.BuildServiceProvider();
        _descartaveis.Add(provedor);

        return provedor.GetRequiredService<ISigaaVinculoDiscenteClient>();
    }

    /// <summary>
    /// Sem espera entre tentativas: o teste prova quantas vezes se tenta, não quanto tempo se espera. Medir tempo de
    /// parede tornaria o teste lento e sujeito à variação da máquina, e com o relógio sob controle do teste uma
    /// espera real nunca terminaria, porque o tempo só avança quando o teste manda.
    /// </summary>
    private static IConfiguration ConfiguracaoDoSigaa() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SigaaOptions.SectionName}:{nameof(SigaaOptions.BaseUrl)}"] = Endereco,
                [$"{SigaaOptions.SectionName}:{nameof(SigaaOptions.Usuario)}"] = "servico",
                [$"{SigaaOptions.SectionName}:{nameof(SigaaOptions.Senha)}"] = "segredo",
                [$"{SigaaOptions.SectionName}:{nameof(SigaaOptions.MaximoDeRetentativas)}"] = "1",
                [$"{SigaaOptions.SectionName}:{nameof(SigaaOptions.EsperaBaseEntreTentativasEmMilissegundos)}"] = "0",
            })
            .Build();

    private static void ConfigurarRegistroDeLog(ILoggingBuilder construtor, RegistroDeLog? registro)
    {
        construtor.SetMinimumLevel(LogLevel.Trace);
        if (registro is not null)
        {
            construtor.AddProvider(registro);
        }
    }

    /// <summary>Substitui só a rede: resiliência e autenticação continuam sendo as reais.</summary>
    private static void UsarSomenteARede(ServiceCollection servicos, RedeSimulada rede) =>
        servicos.ConfigureAll<HttpClientFactoryOptions>(opcoes =>
            opcoes.HttpMessageHandlerBuilderActions.Add(construtor =>
                construtor.PrimaryHandler = rede));

    /// <summary>
    /// Origem do SIGAA simulada: cada autenticação devolve o token da sua ordem e cada consulta, a resposta da sua
    /// ordem, contadas desde 1.
    /// </summary>
    private sealed class OrigemSimulada
    {
        private readonly Func<int, string> _tokenDaAutenticacao;
        private readonly Func<int, HttpResponseMessage> _respostaDaConsulta;

        public OrigemSimulada(
            Func<int, string> tokenDaAutenticacao,
            Func<int, HttpResponseMessage>? respostaDaConsulta = null)
        {
            _tokenDaAutenticacao = tokenDaAutenticacao;
            _respostaDaConsulta = respostaDaConsulta ?? (_ => RespostasDoSigaa.ColecaoVazia());
            Rede = new RedeSimulada((requisicao, _) => Responder(requisicao));
        }

        public RedeSimulada Rede { get; }

        public int Autenticacoes { get; private set; }

        public int Consultas { get; private set; }

        private HttpResponseMessage Responder(HttpRequestMessage requisicao)
        {
            if (EhAutenticacao(requisicao))
            {
                Autenticacoes++;
                return RespostasDoSigaa.ComToken(_tokenDaAutenticacao(Autenticacoes));
            }

            Consultas++;
            return _respostaDaConsulta(Consultas);
        }
    }
}
