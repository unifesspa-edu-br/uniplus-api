namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static CenarioDeFasesDoProcessoPublicado;

/// <summary>
/// A avaliação iniciada no lugar nas fases iniciadas, num Postgres real: o ato inicia a fase produtora pelo instante da
/// divulgação só quando é o ato da fase, e as fontes da avaliação iniciada entram nas fases e nas etapas iniciadas.
/// </summary>
public sealed class AvaliacaoIniciadaNoLugarDasFasesIniciadasTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly CenarioDeFasesDoProcessoPublicado _cenario;

    public AvaliacaoIniciadaNoLugarDasFasesIniciadasTests(ProcessoSeletivoDbFixture fixture) =>
        _cenario = new CenarioDeFasesDoProcessoPublicado(fixture);

    [Fact(DisplayName = "O ato da fase inicia a fase produtora, e o ato da etapa não")]
    public async Task Iniciadas_AtoDaFase_IniciaAFaseProdutora()
    {
        (Guid daFaseId, FaseCronograma fase, _) = await _cenario.PublicadoAsync("PS ato produz a fase", Abertura);
        (Guid daEtapaId, FaseCronograma outraFase, EtapaProcesso etapa) = await _cenario.PublicadoAsync("PS ato da etapa nao produz a fase", Abertura);
        await _cenario.SemearAtoAsync(daFaseId, IdentidadeDoItemDoEdital.DaFase(fase, fase.Produtos.Single()));
        await _cenario.SemearAtoAsync(daEtapaId, IdentidadeDoItemDoEdital.DaEtapa(etapa, ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)));

        FasesIniciadas daFase = await _cenario.IniciadasAsync(daFaseId, DateTimeOffset.UtcNow);
        FasesIniciadas daEtapa = await _cenario.IniciadasAsync(daEtapaId, DateTimeOffset.UtcNow);

        daFase.AvaliacaoComecou(LugarDaDecisao.DaFase(fase.FaseCanonicaOrigemId)).Should().BeTrue();
        daEtapa.Contem(outraFase.FaseCanonicaOrigemId).Should().BeTrue();
        daEtapa.AvaliacaoComecou(LugarDaDecisao.DaFase(outraFase.FaseCanonicaOrigemId)).Should().BeFalse();
    }

    [Fact(DisplayName = "A fonte da avaliação iniciada na fase inicia a avaliação da fase mesmo com o período futuro")]
    public async Task Iniciadas_FonteNoLugarDaFase_IniciaAAvaliacao()
    {
        (Guid processoId, FaseCronograma fase, _) = await _cenario.PublicadoAsync("PS fonte no lugar da fase", Abertura);
        AvaliacaoIniciadaNoLugar avaliacao = new(LugarDaDecisao.DaFase(fase.FaseCanonicaOrigemId), DateTimeOffset.UtcNow);

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow, new FonteFixa(avaliacao));

        iniciadas.AvaliacaoComecou(avaliacao.Lugar).Should().BeTrue();
        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
    }

    [Fact(DisplayName = "A fonte da avaliação iniciada na etapa inicia a etapa e a fase para a autorização, e não a avaliação da fase")]
    public async Task Iniciadas_FonteNoLugarDaEtapa_IniciaAEtapa()
    {
        (Guid processoId, FaseCronograma fase, EtapaProcesso etapa) = await _cenario.PublicadoAsync("PS fonte no lugar da etapa", Abertura);
        AvaliacaoIniciadaNoLugar avaliacao = new(LugarDaDecisao.DaEtapa(etapa.Id), DateTimeOffset.UtcNow);

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow, new FonteFixa(avaliacao));

        iniciadas.AvaliacaoComecou(avaliacao.Lugar).Should().BeTrue();
        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
        iniciadas.AvaliacaoComecou(LugarDaDecisao.DaFase(fase.FaseCanonicaOrigemId)).Should().BeFalse();
    }

    [Fact(DisplayName = "A fonte com várias avaliações iniciadas no lugar inicia a avaliação só a partir do instante do primeiro deles")]
    public async Task Iniciadas_FonteComVariasAvaliacoes_ContaDesdeOPrimeiroInstante()
    {
        (Guid processoId, FaseCronograma fase, _) = await _cenario.PublicadoAsync("PS fonte com varios avaliacoes", Abertura);
        DateTimeOffset primeiro = Abertura.AddDays(-20);
        LugarDaDecisao lugar = LugarDaDecisao.DaFase(fase.FaseCanonicaOrigemId);
        FonteFixa fonte = new(new AvaliacaoIniciadaNoLugar(lugar, primeiro.AddDays(5)), new AvaliacaoIniciadaNoLugar(lugar, primeiro));

        FasesIniciadas antes = await _cenario.IniciadasAsync(processoId, primeiro.AddTicks(-1), fonte);
        FasesIniciadas noInstante = await _cenario.IniciadasAsync(processoId, primeiro, fonte);

        antes.AvaliacaoComecou(lugar).Should().BeFalse("nenhum registro da fonte tinha acontecido");
        noInstante.AvaliacaoComecou(lugar).Should().BeTrue();
    }

    [Fact(DisplayName = "O ato divulgado depois do instante consultado não inicia a fase nesse instante")]
    public async Task Iniciadas_AtoDivulgadoDepoisDoInstante_NaoIniciaAFaseNele()
    {
        (Guid processoId, FaseCronograma fase, _) = await _cenario.PublicadoAsync("PS ato posterior ao instante", Abertura);
        DateTimeOffset antesDoAto = DateTimeOffset.UtcNow;
        await _cenario.SemearAtoAsync(processoId, IdentidadeDoItemDoEdital.DaFase(fase, fase.Produtos.Single()));
        LugarDaDecisao lugar = LugarDaDecisao.DaFase(fase.FaseCanonicaOrigemId);

        FasesIniciadas noPassado = await _cenario.IniciadasAsync(processoId, antesDoAto);
        FasesIniciadas depois = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        noPassado.AvaliacaoComecou(lugar).Should().BeFalse();
        noPassado.Contem(fase.FaseCanonicaOrigemId).Should().BeFalse();
        depois.AvaliacaoComecou(lugar).Should().BeTrue();
    }

    [Fact(DisplayName = "A fonte que falha propaga a falha, e a leitura nunca vale como não iniciado")]
    public async Task Iniciadas_FonteQueFalha_Propaga()
    {
        (Guid processoId, _, _) = await _cenario.PublicadoAsync("PS fonte que falha", Abertura);

        Func<Task> leitura = () => _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow, new FonteQueFalha());

        await leitura.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "A leitura de agora usa o relógio injetado e devolve o instante que usou")]
    public async Task ObterAgora_UsaORelogioInjetado()
    {
        (Guid processoId, FaseCronograma fase, _) = await _cenario.PublicadoAsync("PS leitura de agora", Abertura);
        DateTimeOffset agora = Abertura.AddDays(1);
        await using UniPlusDbContext context = _cenario.CriarDbContext();
        FasesIniciadasDoProcesso leitor = new(context, new RelogioFixo(agora), []);

        FasesIniciadasNoInstante resultado = await leitor.ObterAgoraAsync(processoId);

        resultado.Instante.Should().Be(agora);
        resultado.Fases.AvaliacaoComecou(LugarDaDecisao.DaFase(fase.FaseCanonicaOrigemId)).Should().BeTrue("o período da fase abriu em Abertura");
    }

    private sealed class FonteFixa(params AvaliacaoIniciadaNoLugar[] avaliacoes) : IFonteDaAvaliacaoIniciadaNoLugar
    {
        public Task<IReadOnlyList<AvaliacaoIniciadaNoLugar>> ListarAsync(Guid processoId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AvaliacaoIniciadaNoLugar>>(avaliacoes);
    }

    private sealed class FonteQueFalha : IFonteDaAvaliacaoIniciadaNoLugar
    {
        public Task<IReadOnlyList<AvaliacaoIniciadaNoLugar>> ListarAsync(Guid processoId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("A fonte do início falhou.");
    }
}
