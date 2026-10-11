namespace Unifesspa.UniPlus.Portal.UnitTests.Origens;

using System.Net;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Portal.Application.Certames;
using Unifesspa.UniPlus.Portal.Application.Certames.Origens;

public sealed class ContratoPublicoDoCertameHttpTests
{
    private const string MediaType = "application/vnd.uniplus.certame.v1+json";

    private static readonly Guid ProcessoId = Guid.Parse("0192a000-0000-7000-8000-000000000001");
    private static readonly Guid Criador = Guid.Parse("0192a000-0000-7000-8000-0000000000a1");

    private static readonly string CorpoDoCertame = $$"""
        {
          "processoSeletivoId": "{{ProcessoId}}",
          "atoCriadorId": "{{Criador}}",
          "nome": "Medicina 2027",
          "versaoProjecao": "1",
          "hashConfiguracao": "abc",
          "periodo": { "numero": "012/2026", "inicio": "2026-12-10T00:00:00-03:00", "fim": "2026-12-20T23:59:59-03:00" },
          "localidade": { "codigoIbge": "1505536", "nome": "Marabá", "uf": "PA", "fusoHorario": "America/Belem" },
          "situacao": "inscricoesAbertas",
          "documentoEdital": { "documentoEditalId": "0192a000-0000-7000-8000-0000000000d1", "hashSha256": "abc", "urlDownload": "https://acervo.example/editais/abc.pdf" }
        }
        """;

    [Fact]
    public async Task Le_o_certame_pedindo_o_media_type_da_origem()
    {
        RedeSimulada rede = new((_, _) => RedeSimulada.Json(CorpoDoCertame, MediaType));

        ConsultaDoCertame consulta = await Consultar(rede, c => c.ObterPorIdentificadorAsync("medicina-2027", CancellationToken.None));

        consulta.Should().BeEquivalentTo(new ConsultaDoCertame.Encontrado(
            new CertameDaOrigem(
                ProcessoId, Criador, "Medicina 2027", "012/2026", new Uri("https://acervo.example/editais/abc.pdf"), SituacaoDaInscricao.InscricoesAbertas)));

        HttpRequestMessage requisicao = rede.Requisicoes.Single();
        requisicao.RequestUri!.ToString().Should().Be($"{OrigensMontagem.EnderecoSelecao}/api/selecao/certames/medicina-2027");
        requisicao.Headers.Accept.Select(a => a.MediaType).Should().Equal(MediaType);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("/editais/abc.pdf")]
    [InlineData("ftp://acervo.example/editais/abc.pdf")]
    public async Task Endereco_do_edital_fora_de_http_absoluto_nao_vira_link(string endereco)
    {
        string corpo = CorpoDoCertame.Replace("https://acervo.example/editais/abc.pdf", endereco, StringComparison.Ordinal);
        RedeSimulada rede = new((_, _) => RedeSimulada.Json(corpo, MediaType));

        ConsultaDoCertame consulta = await Consultar(rede, c => c.ObterPorIdentificadorAsync("medicina-2027", CancellationToken.None));

        consulta.Should().BeOfType<ConsultaDoCertame.Encontrado>()
            .Which.Certame.DocumentoEditalUrl.Should().BeNull();
    }

    [Fact]
    public async Task Consulta_sai_sem_credencial_nem_cookie()
    {
        RedeSimulada rede = new((_, _) => RedeSimulada.Json(CorpoDoCertame, MediaType));

        await Consultar(rede, c => c.ObterPorIdentificadorAsync("medicina-2027", CancellationToken.None));

        HttpRequestMessage requisicao = rede.Requisicoes.Single();
        requisicao.Headers.Authorization.Should().BeNull();
        requisicao.Headers.Contains("Cookie").Should().BeFalse();
    }

    [Fact]
    public async Task Pede_pelo_identificador_legivel_no_mesmo_recurso()
    {
        RedeSimulada rede = new((_, _) => RedeSimulada.Json(CorpoDoCertame, MediaType));

        ConsultaDoCertame consulta = await Consultar(
            rede,
            c => c.ObterPorIdentificadorAsync("medicina-2027", CancellationToken.None));

        consulta.Should().BeOfType<ConsultaDoCertame.Encontrado>();
        rede.Requisicoes.Single().RequestUri!.AbsolutePath.Should().Be("/api/selecao/certames/medicina-2027");
    }

    [Fact]
    public async Task Nao_encontrado_da_origem_chega_como_nao_encontrado_sem_repeticao()
    {
        RedeSimulada rede = new((_, _) => RedeSimulada.Status(HttpStatusCode.NotFound));

        ConsultaDoCertame consulta = await Consultar(rede, c => c.ObterPorIdentificadorAsync("medicina-2027", CancellationToken.None));

        consulta.Should().BeOfType<ConsultaDoCertame.NaoEncontrado>();
        rede.Chamadas.Should().Be(1, "não encontrado é resposta, não falha transitória");
    }

    [Fact]
    public async Task Origem_fora_do_ar_e_repetida_e_depois_vira_indisponivel()
    {
        RedeSimulada rede = new((_, _) => RedeSimulada.Status(HttpStatusCode.ServiceUnavailable));

        ConsultaDoCertame consulta = await Consultar(rede, c => c.ObterPorIdentificadorAsync("medicina-2027", CancellationToken.None));

        consulta.Should().BeOfType<ConsultaDoCertame.Indisponivel>();
        rede.Chamadas.Should().Be(2, "uma tentativa e uma repetição, conforme configurado");
    }

    [Fact]
    public async Task Falha_de_rede_vira_indisponivel_em_vez_de_excecao()
    {
        RedeSimulada rede = new((_, _) => throw new HttpRequestException("conexão recusada"));

        ConsultaDoCertame consulta = await Consultar(rede, c => c.ObterPorIdentificadorAsync("medicina-2027", CancellationToken.None));

        consulta.Should().BeOfType<ConsultaDoCertame.Indisponivel>();
    }

    [Fact]
    public async Task Circuito_aberto_vira_indisponivel_sem_chegar_a_rede()
    {
        RedeSimulada rede = new((_, _) => RedeSimulada.Status(HttpStatusCode.ServiceUnavailable));

        using ServiceProvider provedor = OrigensMontagem.Montar(rede, new Dictionary<string, string?>
        {
            ["MaximoDeRetentativas"] = "0",
            ["ChamadasMinimasParaAvaliarCorte"] = "2",
        });

        // Duas falhas na janela abrem o circuito.
        for (int i = 0; i < 2; i++)
        {
            using IServiceScope escopo = provedor.CreateScope();
            await escopo.ServiceProvider.GetRequiredService<IContratoPublicoDoCertame>()
                .ObterPorIdentificadorAsync("medicina-2027", CancellationToken.None);
        }

        int chamadasAntes = rede.Chamadas;

        using IServiceScope seguinte = provedor.CreateScope();
        ConsultaDoCertame consulta = await seguinte.ServiceProvider.GetRequiredService<IContratoPublicoDoCertame>()
            .ObterPorIdentificadorAsync("medicina-2027", CancellationToken.None);

        consulta.Should().BeOfType<ConsultaDoCertame.Indisponivel>(
            "o circuito aberto não pode escapar como exceção e virar 500 no tratador global");
        rede.Chamadas.Should().Be(chamadasAntes, "com o circuito aberto a consulta nem chega à origem");
    }

    [Fact]
    public async Task Resposta_sem_os_campos_obrigatorios_vira_indisponivel()
    {
        RedeSimulada rede = new((_, _) => RedeSimulada.Json(
            $$"""{ "processoSeletivoId": "{{ProcessoId}}", "atoCriadorId": "{{Criador}}" }""",
            MediaType));

        ConsultaDoCertame consulta = await Consultar(rede, c => c.ObterPorIdentificadorAsync("medicina-2027", CancellationToken.None));

        consulta.Should().BeOfType<ConsultaDoCertame.Indisponivel>();
    }

    [Fact]
    public async Task Cancelamento_pedido_por_quem_chama_se_propaga()
    {
        RedeSimulada rede = new((_, _) => RedeSimulada.Json(CorpoDoCertame, MediaType));
        using CancellationTokenSource cancelamento = new();
        await cancelamento.CancelAsync();

        Func<Task> chamada = () => Consultar(rede, c => c.ObterPorIdentificadorAsync("medicina-2027", cancelamento.Token));

        await chamada.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Cancelamento_no_meio_da_consulta_se_propaga_em_vez_de_virar_indisponivel()
    {
        using CancellationTokenSource cancelamento = new();
        RedeSimulada rede = new((_, _) =>
        {
            // Quem chama desiste enquanto a requisição está em voo.
            cancelamento.Cancel();
            throw new TaskCanceledException();
        });

        Func<Task> chamada = () => Consultar(rede, c => c.ObterPorIdentificadorAsync("medicina-2027", cancelamento.Token));

        await chamada.Should().ThrowAsync<OperationCanceledException>(
            "quem desistiu da leitura não quer uma página marcada como indisponível");
    }

    private static async Task<ConsultaDoCertame> Consultar(
        RedeSimulada rede,
        Func<IContratoPublicoDoCertame, Task<ConsultaDoCertame>> consultar)
    {
        using ServiceProvider provedor = OrigensMontagem.Montar(rede);
        using IServiceScope escopo = provedor.CreateScope();

        return await consultar(escopo.ServiceProvider.GetRequiredService<IContratoPublicoDoCertame>());
    }
}
