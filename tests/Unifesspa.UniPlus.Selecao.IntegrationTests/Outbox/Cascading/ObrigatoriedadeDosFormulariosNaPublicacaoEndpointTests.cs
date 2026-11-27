namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Results;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.PublicacaoPeloEndpointDeTeste;

/// <summary>
/// A obrigatoriedade dos formulários por fase na publicação (UNI-REQ-0144): quem cobra taxa tem formulário
/// facultativo no período de isenção, o obrigatório só vale na fase que também coleta inscrição, e o processo de
/// resultado importado não tem formulário na fase que coleta inscrição. Cada regra é provada com uma configuração
/// que a recusa e outra que a atende, pelo código da recusa e não pela fase nem pelo nome do formulário.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class ObrigatoriedadeDosFormulariosNaPublicacaoEndpointTests
{
    private const string Prefixo = "uniplus.selecao.formulario_processo.";
    private const string TaxaSemFormularioDeIsencao = Prefixo + "taxa_sem_formulario_de_isencao";
    private const string FormularioDaIsencaoObrigatorio = Prefixo + "formulario_da_isencao_obrigatorio";
    private const string InscricaoEmResultadoImportado = Prefixo + "inscricao_em_resultado_importado";
    private const string FaseDeIsencao = "SOLICITACAO_DE_ISENCAO";

    private readonly CascadingFixture _fixture;

    public ObrigatoriedadeDosFormulariosNaPublicacaoEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Processo que cobra taxa e não tem formulário facultativo no período de isenção é recusado na publicação")]
    public async Task QuemCobraTaxaSemFormularioDeIsencao_RecusaAPublicacao()
    {
        IReadOnlySet<string> recusas = await PublicarEListarRecusasAsync(
            nameof(QuemCobraTaxaSemFormularioDeIsencao_RecusaAPublicacao),
            new OpcoesDoProcessoSeletivoPublicavel { Nome = string.Empty, ColetaSolicitacaoIsencao = true, Complementar = static p => CobrarTaxa(p) });

        recusas.Should().Contain(TaxaSemFormularioDeIsencao);
    }

    [Fact(DisplayName = "Processo que cobra taxa e tem formulário facultativo no período de isenção não é recusado por falta dele")]
    public async Task QuemCobraTaxaComFormularioFacultativoDeIsencao_NaoRecusaPorFaltaDele()
    {
        IReadOnlySet<string> recusas = await PublicarEListarRecusasAsync(
            nameof(QuemCobraTaxaComFormularioFacultativoDeIsencao_NaoRecusaPorFaltaDele), ProcessoQueCobraTaxaComPedidoDeIsencaoNaFaseUnica());

        recusas.Should().NotContain(TaxaSemFormularioDeIsencao);
        recusas.Should().NotContain(FormularioDaIsencaoObrigatorio);
    }

    [Fact(DisplayName = "Processo que não cobra taxa não é recusado por não ter formulário de isenção")]
    public async Task QuemNaoCobraTaxaSemFormularioDeIsencao_NaoRecusaPorIsso()
    {
        IReadOnlySet<string> recusas = await PublicarEListarRecusasAsync(
            nameof(QuemNaoCobraTaxaSemFormularioDeIsencao_NaoRecusaPorIsso),
            new OpcoesDoProcessoSeletivoPublicavel { Nome = string.Empty, ColetaSolicitacaoIsencao = true });

        recusas.Should().NotContain(TaxaSemFormularioDeIsencao);
    }

    [Fact(DisplayName = "Formulário obrigatório na fase que só recebe pedido de isenção é recusado na publicação")]
    public async Task FormularioObrigatorioNaFaseSoDeIsencao_RecusaAPublicacao()
    {
        IReadOnlySet<string> recusas = await PublicarEListarRecusasAsync(
            nameof(FormularioObrigatorioNaFaseSoDeIsencao_RecusaAPublicacao), ProcessoComFaseSoDeIsencao(obrigatorio: true));

        recusas.Should().Contain(FormularioDaIsencaoObrigatorio);
    }

    [Fact(DisplayName = "Formulário facultativo na fase que só recebe pedido de isenção não é recusado por ser obrigatório")]
    public async Task FormularioFacultativoNaFaseSoDeIsencao_NaoRecusaPorObrigatoriedade()
    {
        IReadOnlySet<string> recusas = await PublicarEListarRecusasAsync(
            nameof(FormularioFacultativoNaFaseSoDeIsencao_NaoRecusaPorObrigatoriedade), ProcessoComFaseSoDeIsencao(obrigatorio: false));

        recusas.Should().NotContain(FormularioDaIsencaoObrigatorio);
        recusas.Should().NotContain(TaxaSemFormularioDeIsencao);
    }

    [Fact(DisplayName = "Formulário obrigatório na fase que coleta inscrição e recebe pedido de isenção não é recusado por ser obrigatório")]
    public async Task FormularioObrigatorioNaFaseQueTambemColetaInscricao_NaoRecusaPorObrigatoriedade()
    {
        IReadOnlySet<string> recusas = await PublicarEListarRecusasAsync(
            nameof(FormularioObrigatorioNaFaseQueTambemColetaInscricao_NaoRecusaPorObrigatoriedade),
            new OpcoesDoProcessoSeletivoPublicavel { Nome = string.Empty, ColetaSolicitacaoIsencao = true });

        recusas.Should().NotContain(FormularioDaIsencaoObrigatorio);
    }

    [Fact(DisplayName = "Processo de resultado importado com formulário na fase que coleta inscrição é recusado na publicação")]
    public async Task ResultadoImportadoComFormularioNaFaseDeInscricao_RecusaAPublicacao()
    {
        IReadOnlySet<string> recusas = await PublicarEListarRecusasAsync(
            nameof(ResultadoImportadoComFormularioNaFaseDeInscricao_RecusaAPublicacao),
            ProcessoImportado(static p => DefinirFormularioDaInscricao(p, p.CronogramaFases.Single().Id)));

        recusas.Should().Contain(InscricaoEmResultadoImportado);
    }

    [Fact(DisplayName = "Processo de resultado importado sem formulário na fase que coleta inscrição não é recusado por ter um")]
    public async Task ResultadoImportadoSemFormularioNaFaseDeInscricao_NaoRecusaPorIsso()
    {
        IReadOnlySet<string> recusas = await PublicarEListarRecusasAsync(
            nameof(ResultadoImportadoSemFormularioNaFaseDeInscricao_NaoRecusaPorIsso), ProcessoImportado(complementar: null));

        recusas.Should().NotContain(InscricaoEmResultadoImportado);
    }

    private static OpcoesDoProcessoSeletivoPublicavel ProcessoImportado(Action<ProcessoSeletivo>? complementar) => new()
    {
        Nome = string.Empty,
        Origem = OrigemCandidatos.ImportacaoExterna,
        ComBancaDeImportacaoDeCandidatos = true,
        Complementar = complementar,
    };

    private static OpcoesDoProcessoSeletivoPublicavel ProcessoQueCobraTaxaComPedidoDeIsencaoNaFaseUnica() => new()
    {
        Nome = string.Empty,
        ColetaSolicitacaoIsencao = true,
        Complementar = static p =>
        {
            CobrarTaxa(p);
            DefinirPedidoDeIsencao(p, p.CronogramaFases.Single().Id, obrigatorio: false);
        },
    };

    private static FaseCronograma FaseSoDeIsencao() => FaseDoCronogramaPublicavelDeTeste.Criar(
        FaseDoCronogramaPublicavelDeTeste.OrigemNoEdital(FaseDeIsencao) with { ColetaSolicitacaoIsencao = true },
        ordem: 1,
        new DateTimeOffset(2025, 11, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2025, 11, 10, 0, 0, 0, TimeSpan.Zero));

    private static OpcoesDoProcessoSeletivoPublicavel ProcessoComFaseSoDeIsencao(bool obrigatorio) => new()
    {
        Nome = string.Empty,
        OrdemDaFaseDeResultado = 2,
        FasesAdicionais = [FaseSoDeIsencao()],
        Complementar = p =>
        {
            CobrarTaxa(p);
            DefinirPedidoDeIsencao(p, p.CronogramaFases.Single(static f => f.ColetaSolicitacaoIsencao && !f.ColetaInscricao).Id, obrigatorio);
        },
    };

    private static void CobrarTaxa(ProcessoSeletivo processo)
    {
        ConfiguracaoTaxaInscricao taxa = ConfiguracaoTaxaInscricao.Criar(
            cobra: true, valor: 100m, fundamentosCodigos: ["CARENCIA_SOCIOECONOMICA"]).Value!;
        Result definida = processo.DefinirTaxaInscricao(taxa);
        definida.IsSuccess.Should().BeTrue(definida.Error?.Message);
    }

    private static void DefinirPedidoDeIsencao(ProcessoSeletivo processo, Guid faseId, bool obrigatorio) =>
        processo.DefinirFormularioSemConjuntoBasico(FormulariosDoProcessoDeTeste.PedidoDeIsencao, faseId, obrigatorio);

    private static void DefinirFormularioDaInscricao(ProcessoSeletivo processo, Guid faseId) =>
        processo.DefinirFormularioSemConjuntoBasico(FormulariosDoProcessoDeTeste.Inscricao, faseId, obrigatorio: true);

    /// <summary>
    /// Publica o processo semeado e devolve o código da recusa e os de <c>errors[]</c>, vazio quando a publicação passa.
    /// O corpo da recusa traz as causas independentes juntas (ADR-0125), então o teste olha só a que isola.
    /// </summary>
    private async Task<IReadOnlySet<string>> PublicarEListarRecusasAsync(string cenario, OpcoesDoProcessoSeletivoPublicavel opcoes)
    {
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        (Guid processoId, Guid documentoId) = await SemearAsync(opcoes with { Nome = $"{cenario} {Guid.CreateVersion7()}" });

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpResponseMessage resposta = await PublicarAsync(client, processoId, documentoId);
        string corpo = await resposta.Content.ReadAsStringAsync();
        if (resposta.StatusCode != HttpStatusCode.UnprocessableEntity)
        {
            resposta.IsSuccessStatusCode.Should().BeTrue(corpo);
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return CodigosDaRecusa(corpo);
    }

    private async Task<(Guid ProcessoId, Guid DocumentoId)> SemearAsync(OpcoesDoProcessoSeletivoPublicavel opcoes)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, opcoes);
        return (processo.Id, documento.Id);
    }

    private static HashSet<string> CodigosDaRecusa(string corpo)
    {
        using JsonDocument problema = JsonDocument.Parse(corpo);
        HashSet<string> codigos = new(StringComparer.Ordinal) { problema.RootElement.GetProperty("code").GetString()! };
        if (problema.RootElement.TryGetProperty("errors", out JsonElement erros))
        {
            codigos.UnionWith(erros.EnumerateArray().Select(static erro => erro.GetProperty("code").GetString()!));
        }

        return codigos;
    }
}
