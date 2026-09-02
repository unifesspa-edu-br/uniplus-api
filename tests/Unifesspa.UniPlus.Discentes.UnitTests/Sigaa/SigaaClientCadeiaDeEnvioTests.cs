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
        string token = RespostasDoSigaa.TokenCom(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1));

        RedeSimulada rede = new((requisicao, _) => EhAutenticacao(requisicao)
            ? RespostasDoSigaa.ComToken(token)
            : RespostasDoSigaa.ColecaoVazia());

        ISigaaVinculoDiscenteClient cliente = Montar(rede, out _);

        await cliente.ObterPaginaAsync(new FiltroDeVinculos("G"), 1);

        rede.AutorizacoesRecebidas.Should().Contain(token, "a consulta precisa sair autenticada");
    }

    [Fact]
    public async Task Recusa_do_token_dispara_renovacao_e_uma_unica_repeticao()
    {
        string primeiro = RespostasDoSigaa.TokenCom(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), "velho");
        string segundo = RespostasDoSigaa.TokenCom(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), "novo");

        int autenticacoes = 0;
        bool jaRecusou = false;

        RedeSimulada rede = new((requisicao, _) =>
        {
            if (EhAutenticacao(requisicao))
            {
                autenticacoes++;
                return RespostasDoSigaa.ComToken(autenticacoes == 1 ? primeiro : segundo);
            }

            // A origem recusa a primeira consulta como se o token tivesse vencido.
            if (!jaRecusou)
            {
                jaRecusou = true;
                return RespostasDoSigaa.Status(HttpStatusCode.Unauthorized);
            }

            return RespostasDoSigaa.ColecaoVazia();
        });

        ISigaaVinculoDiscenteClient cliente = Montar(rede, out _);

        await cliente.ObterPaginaAsync(new FiltroDeVinculos("G"), 1);

        autenticacoes.Should().Be(2, "a recusa dispara exatamente uma renovação");
        rede.AutorizacoesRecebidas.Should().Contain(segundo, "a repetição sai com o token renovado");
    }

    [Fact]
    public async Task Repeticao_da_resiliencia_relê_o_token_que_venceu_no_meio_do_caminho()
    {
        // Prova a ordem da cadeia, e não há como provar isso com uma recusa de token: a
        // política de repetição não considera recusa de autorização uma falha repetível,
        // então ela nem chega a repetir, e a segunda autenticação viria só do manipulador
        // de autenticação — que faria o mesmo estando por dentro ou por fora.
        //
        // O cenário que distingue as duas ordens é uma falha que a política DE FATO
        // repete, com o token vencendo entre as tentativas. Com a autenticação por dentro,
        // a segunda tentativa reentra nela, percebe o vencimento e renova. Com ela por
        // fora, o cabeçalho teria sido carimbado uma única vez antes de a política começar,
        // e nenhuma renovação aconteceria.
        RelogioControlado relogio = new(new DateTimeOffset(2026, 9, 2, 3, 0, 0, TimeSpan.Zero));

        int autenticacoes = 0;
        int consultas = 0;

        RedeSimulada rede = new((requisicao, _) =>
        {
            if (EhAutenticacao(requisicao))
            {
                autenticacoes++;

                // Validade curta: o avanço do relógio abaixo a ultrapassa.
                return RespostasDoSigaa.ComToken(RespostasDoSigaa.TokenCom(
                    relogio.GetUtcNow(),
                    relogio.GetUtcNow().AddMinutes(2),
                    $"token{autenticacoes}"));
            }

            consultas++;

            if (consultas == 1)
            {
                // Indisponibilidade momentânea: esta a política repete. Entre uma
                // tentativa e outra, o token vence.
                relogio.Avancar(TimeSpan.FromMinutes(5));
                return RespostasDoSigaa.Status(HttpStatusCode.ServiceUnavailable);
            }

            return RespostasDoSigaa.ColecaoVazia();
        });

        ISigaaVinculoDiscenteClient cliente = Montar(rede, out _, relogio: relogio);

        await cliente.ObterPaginaAsync(new FiltroDeVinculos("G"), 1);

        consultas.Should().Be(2, "a indisponibilidade momentânea foi repetida");
        autenticacoes.Should().Be(
            2,
            "a segunda tentativa reentrou na autenticação e renovou o token vencido — o que só "
            + "acontece com a autenticação por dentro da política de resiliência");
    }

    [Fact]
    public async Task Envia_cada_situacao_pedida_como_item_de_lista()
    {
        // A origem reconhece lista pelo nome do parâmetro terminando em colchetes. Sem
        // eles, os valores chegam como ocorrências soltas do mesmo nome e a linguagem da
        // origem guarda só a última: o filtro valeria por uma situação, e os vínculos das
        // outras sumiriam da sincronização sem erro nenhum.
        RedeSimulada rede = new((requisicao, _) => EhAutenticacao(requisicao)
            ? RespostasDoSigaa.ComToken(
                RespostasDoSigaa.TokenCom(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)))
            : RespostasDoSigaa.ColecaoVazia());

        ISigaaVinculoDiscenteClient cliente = Montar(rede, out _);

        await cliente.ObterPaginaAsync(new FiltroDeVinculos("G", Situacoes: [1, 8, 9]), 1);

        string consulta = Uri.UnescapeDataString(
            rede.CaminhosChamados.Single(c => c.Contains("vinculo_discentes", StringComparison.Ordinal)));

        foreach (int situacao in new[] { 1, 8, 9 })
        {
            consulta.Should().Contain(
                $"status.id[]={situacao}",
                "cada situação pedida precisa chegar como item de lista");
        }
    }

    [Fact]
    public async Task Repete_falha_transitoria_da_origem_ate_obter_resposta()
    {
        int consultas = 0;

        RedeSimulada rede = new((requisicao, _) =>
        {
            if (EhAutenticacao(requisicao))
            {
                return RespostasDoSigaa.ComToken(
                    RespostasDoSigaa.TokenCom(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)));
            }

            consultas++;
            return consultas == 1
                ? RespostasDoSigaa.Status(HttpStatusCode.ServiceUnavailable)
                : RespostasDoSigaa.ColecaoVazia();
        });

        ISigaaVinculoDiscenteClient cliente = Montar(rede, out _);

        await cliente.ObterPaginaAsync(new FiltroDeVinculos("G"), 1);

        consultas.Should().Be(2, "indisponibilidade momentânea é repetida");
    }

    [Fact]
    public async Task Nao_repete_recusa_definitiva_da_origem()
    {
        int consultas = 0;

        RedeSimulada rede = new((requisicao, _) =>
        {
            if (EhAutenticacao(requisicao))
            {
                return RespostasDoSigaa.ComToken(
                    RespostasDoSigaa.TokenCom(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)));
            }

            consultas++;
            return RespostasDoSigaa.Status(HttpStatusCode.BadRequest);
        });

        ISigaaVinculoDiscenteClient cliente = Montar(rede, out _);

        Func<Task> chamada = () => cliente.ObterPaginaAsync(new FiltroDeVinculos("G"), 1);
        await chamada.Should().ThrowAsync<Exception>();

        consultas.Should().Be(
            1,
            "filtro inválido não melhora com repetição; insistir só multiplicaria a recusa");
    }

    [Fact]
    public async Task Token_nao_aparece_no_log_nem_no_nivel_mais_detalhado()
    {
        string token = RespostasDoSigaa.TokenCom(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1),
            "segredo-que-nao-pode-vazar");

        RedeSimulada rede = new((requisicao, _) => EhAutenticacao(requisicao)
            ? RespostasDoSigaa.ComToken(token)
            : RespostasDoSigaa.ColecaoVazia());

        RegistroDeLog registro = new();
        ISigaaVinculoDiscenteClient cliente = Montar(rede, out _, registro);

        await cliente.ObterPaginaAsync(new FiltroDeVinculos("G"), 1);

        registro.TudoQueFoiRegistrado.Should().NotContain(
            token,
            "o registrador do cliente HTTP grava todos os cabeçalhos no nível mais detalhado; "
            + "sem a omissão explícita do cabeçalho de autorização, o token sairia inteiro");
    }

    private static bool EhAutenticacao(HttpRequestMessage requisicao) =>
        requisicao.RequestUri?.AbsolutePath.Contains("authentication_token", StringComparison.Ordinal) == true;

    private ISigaaVinculoDiscenteClient Montar(
        RedeSimulada rede,
        out ServiceProvider provedor,
        RegistroDeLog? registro = null,
        TimeProvider? relogio = null)
    {
        ServiceCollection servicos = new();

        servicos.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SigaaOptions.SectionName}:{nameof(SigaaOptions.BaseUrl)}"] = Endereco,
                [$"{SigaaOptions.SectionName}:{nameof(SigaaOptions.Usuario)}"] = "servico",
                [$"{SigaaOptions.SectionName}:{nameof(SigaaOptions.Senha)}"] = "segredo",
                [$"{SigaaOptions.SectionName}:{nameof(SigaaOptions.MaximoDeRetentativas)}"] = "1",

                // Sem espera entre tentativas. O teste prova quantas vezes se tenta, não
                // quanto tempo se espera: medir tempo de parede tornaria o teste lento e
                // sujeito à variação da máquina. E com o relógio sob controle do teste,
                // uma espera real nunca terminaria, porque o tempo só avança quando o
                // teste manda.
                [$"{SigaaOptions.SectionName}:{nameof(SigaaOptions.EsperaBaseEntreTentativasEmMilissegundos)}"] = "0",
            })
            .Build());

        if (relogio is not null)
        {
            // Registrado antes do cliente: o registro usa acréscimo condicional para o
            // relógio, então o do teste prevalece sobre o do sistema.
            servicos.AddSingleton(relogio);
        }

        servicos.AddLogging(construtor =>
        {
            construtor.SetMinimumLevel(LogLevel.Trace);
            if (registro is not null)
            {
                construtor.AddProvider(registro);
            }
        });

        IConfiguration configuracao = servicos.BuildServiceProvider().GetRequiredService<IConfiguration>();
        servicos.AddSigaaVinculoDiscenteClient(configuracao);

        // Substitui só a rede: resiliência e autenticação continuam sendo as reais.
        servicos.ConfigureAll<HttpClientFactoryOptions>(opcoes =>
            opcoes.HttpMessageHandlerBuilderActions.Add(construtor =>
                construtor.PrimaryHandler = rede));

        provedor = servicos.BuildServiceProvider();
        _descartaveis.Add(provedor);

        return provedor.GetRequiredService<ISigaaVinculoDiscenteClient>();
    }
}
