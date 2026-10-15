namespace Unifesspa.UniPlus.Portal.UnitTests.Origens;

using System.Net;
using System.Web;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Portal.Application.Certames.Origens;

public sealed class RegistroDeAtosDoCertameHttpTests
{
    private const string MediaType = "application/vnd.uniplus.ato-normativo.v1+json";

    private static readonly Guid ProcessoId = Guid.Parse("0192a000-0000-7000-8000-000000000001");

    [Fact]
    public async Task Percorre_as_paginas_pedindo_ao_endereco_configurado_so_com_o_cursor_do_link()
    {
        Guid primeiro = Guid.NewGuid();
        Guid segundo = Guid.NewGuid();

        RedeSimulada rede = new((_, numero) => numero == 1
            ? PaginaCom([Ato(primeiro)], proximoCursor: "c2")
            : PaginaCom([Ato(segundo)], proximoCursor: null));

        LeituraDosAtos leitura = await Ler(rede);

        leitura.Falha.Should().BeNull();
        leitura.Atos.Select(a => a.Id).Should().Equal(primeiro, segundo);

        HttpRequestMessage[] requisicoes = [.. rede.Requisicoes];
        requisicoes[0].RequestUri!.GetLeftPart(UriPartial.Path).Should().Be(
            $"{OrigensMontagem.EnderecoPublicacoes}/api/publicacoes/entidades/PROCESSO_SELETIVO/{ProcessoId}/atos");
        HttpUtility.ParseQueryString(requisicoes[0].RequestUri!.Query)["limit"].Should().Be("100");
        HttpUtility.ParseQueryString(requisicoes[0].RequestUri!.Query)["cursor"].Should().BeNull();

        // O link da origem aponta para outro host; a segunda página vai ao endereço configurado.
        requisicoes[1].RequestUri!.Host.Should().Be(new Uri(OrigensMontagem.EnderecoPublicacoes).Host);
        HttpUtility.ParseQueryString(requisicoes[1].RequestUri!.Query)["cursor"].Should().Be("c2");
        HttpUtility.ParseQueryString(requisicoes[1].RequestUri!.Query)["limit"].Should().BeNull(
            "o cursor já carrega a janela");

        requisicoes.Should().AllSatisfy(r =>
        {
            r.Headers.Accept.Select(a => a.MediaType).Should().Equal(MediaType);
            r.Headers.Authorization.Should().BeNull();
            r.Headers.Contains("Cookie").Should().BeFalse();
        });
    }

    [Fact]
    public async Task Teto_de_paginas_atingido_interrompe_e_informa()
    {
        RedeSimulada rede = new((_, numero) => PaginaCom([Ato(Guid.NewGuid())], proximoCursor: $"c{numero + 1}"));

        LeituraDosAtos leitura = await Ler(rede, new Dictionary<string, string?> { ["TetoDePaginasDeAtos"] = "2" });

        leitura.Falha.Should().Be(FalhaNaLeituraDosAtos.TetoDePaginas);
        leitura.Atos.Should().HaveCount(2);
        rede.Chamadas.Should().Be(2);
    }

    [Fact]
    public async Task Cursor_recusado_no_meio_do_percurso_e_falha_na_paginacao_com_o_que_ja_foi_lido()
    {
        Guid primeiro = Guid.NewGuid();

        RedeSimulada rede = new((_, numero) => numero == 1
            ? PaginaCom([Ato(primeiro)], proximoCursor: "c2")
            : RedeSimulada.Status(HttpStatusCode.Gone));

        LeituraDosAtos leitura = await Ler(rede);

        leitura.Falha.Should().Be(FalhaNaLeituraDosAtos.FalhaNaPaginacao);
        leitura.Atos.Select(a => a.Id).Should().Equal(primeiro);
    }

    [Fact]
    public async Task Origem_fora_do_ar_na_primeira_pagina_e_origem_indisponivel()
    {
        RedeSimulada rede = new((_, _) => RedeSimulada.Status(HttpStatusCode.ServiceUnavailable));

        LeituraDosAtos leitura = await Ler(rede);

        leitura.Falha.Should().Be(FalhaNaLeituraDosAtos.OrigemIndisponivel);
        leitura.Atos.Should().BeEmpty();
    }

    [Fact]
    public async Task Falha_de_rede_vira_origem_indisponivel_em_vez_de_excecao()
    {
        RedeSimulada rede = new((_, _) => throw new HttpRequestException("conexão recusada"));

        LeituraDosAtos leitura = await Ler(rede);

        leitura.Falha.Should().Be(FalhaNaLeituraDosAtos.OrigemIndisponivel);
    }

    [Fact]
    public async Task Proxima_pagina_anunciada_sem_cursor_legivel_nao_encerra_como_lista_completa()
    {
        RedeSimulada rede = new((_, _) =>
        {
            HttpResponseMessage resposta = RedeSimulada.Json($"[{Ato(Guid.NewGuid())}]", MediaType);
            resposta.Headers.TryAddWithoutValidation("Link", "<http://outro.test/atos>; rel=\"next\"");
            return resposta;
        });

        LeituraDosAtos leitura = await Ler(rede);

        leitura.Falha.Should().Be(FalhaNaLeituraDosAtos.FalhaNaPaginacao);
        leitura.Atos.Should().HaveCount(1);
    }

    [Fact]
    public async Task Ato_nulo_na_pagina_e_ato_fora_do_contrato_e_nao_excecao()
    {
        Guid primeiro = Guid.NewGuid();

        RedeSimulada rede = new((_, numero) => numero == 1
            ? PaginaCom([Ato(primeiro)], proximoCursor: "c2")
            : RedeSimulada.Json("[null]", MediaType));

        LeituraDosAtos leitura = await Ler(rede);

        leitura.Falha.Should().Be(FalhaNaLeituraDosAtos.FalhaNaPaginacao);
        leitura.Atos.Select(a => a.Id).Should().Equal(primeiro);
    }

    [Fact]
    public async Task Le_os_campos_do_ato_e_ignora_o_assinante()
    {
        Guid id = Guid.NewGuid();
        string corpo = $$"""
            [{
              "id": "{{id}}", "orgao": "UNIFESSPA", "serie": "EDITAL", "ano": 2026, "numero": "012/2026",
              "tipoCodigo": "EDITAL_ABERTURA", "tipoNome": "Edital de abertura",
              "congelaConfiguracao": true, "efeitoIrreversivel": false, "unicoPorObjeto": true,
              "dataPublicacao": "2026-12-01", "documentoHash": "ff", "assinante": "Fulano de Tal",
              "registradoEm": "2026-12-01T10:00:00Z", "atoRetificadoId": null, "motivoRetificacao": null
            }]
            """;

        RedeSimulada rede = new((_, _) => RedeSimulada.Json(corpo, MediaType));

        LeituraDosAtos leitura = await Ler(rede);

        leitura.Atos.Single().Should().Be(new AtoDaOrigem(
            id, "EDITAL_ABERTURA", "Edital de abertura", 2026, "012/2026", new DateOnly(2026, 12, 1), true, null));
    }

    [Fact]
    public async Task Cancelamento_no_meio_do_percurso_se_propaga_em_vez_de_degradar()
    {
        using CancellationTokenSource cancelamento = new();
        RedeSimulada rede = new((_, numero) =>
        {
            if (numero == 1)
            {
                return PaginaCom([Ato(Guid.NewGuid())], proximoCursor: "c2");
            }

            cancelamento.Cancel();
            throw new TaskCanceledException();
        });

        using ServiceProvider provedor = OrigensMontagem.Montar(rede);
        using IServiceScope escopo = provedor.CreateScope();

        Func<Task> chamada = () => escopo.ServiceProvider.GetRequiredService<IRegistroDeAtosDoCertame>()
            .LerAtosAsync(ProcessoId, cancelamento.Token);

        await chamada.Should().ThrowAsync<OperationCanceledException>();
    }

    private static async Task<LeituraDosAtos> Ler(RedeSimulada rede, IDictionary<string, string?>? ajustes = null)
    {
        using ServiceProvider provedor = OrigensMontagem.Montar(rede, ajustes);
        using IServiceScope escopo = provedor.CreateScope();

        return await escopo.ServiceProvider.GetRequiredService<IRegistroDeAtosDoCertame>()
            .LerAtosAsync(ProcessoId, CancellationToken.None);
    }

    private static HttpResponseMessage PaginaCom(IEnumerable<string> atos, string? proximoCursor)
    {
        HttpResponseMessage resposta = RedeSimulada.Json($"[{string.Join(',', atos)}]", MediaType);

        string self = "<http://publicacoes.publico.test/api/publicacoes/entidades/PROCESSO_SELETIVO/x/atos>; rel=\"self\"";
        string link = proximoCursor is null
            ? self
            : $"<http://publicacoes.publico.test/api/publicacoes/entidades/PROCESSO_SELETIVO/x/atos?cursor={proximoCursor}&direction=next>; rel=\"next\", {self}";
        resposta.Headers.TryAddWithoutValidation("Link", link);

        return resposta;
    }

    private static string Ato(Guid id) => $$"""
        { "id": "{{id}}", "tipoCodigo": "AVISO", "tipoNome": "Aviso", "ano": 2026, "numero": null,
          "dataPublicacao": "2026-12-01", "congelaConfiguracao": false, "motivoRetificacao": null }
        """;
}
