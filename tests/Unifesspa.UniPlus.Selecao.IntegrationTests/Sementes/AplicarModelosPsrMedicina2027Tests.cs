namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Sementes;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

/// <summary>
/// Os modelos de inscrição e de habilitação do edital de Medicina 2027, semeados pela migration da
/// Configuração, são aplicados a um processo PSR em rascunho, com bônus regional, a oferta de
/// atendimento especializado e cota da lei ofertada, pelo passo Formulários, sem recusa.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class AplicarModelosPsrMedicina2027Tests
{
    private readonly CascadingFixture _fixture;

    public AplicarModelosPsrMedicina2027Tests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "A inscrição e a habilitação de Medicina são aplicadas ao processo PSR com bônus regional e oferta de atendimento, com os municípios do bônus, o atendimento especializado e a composição familiar")]
    public async Task ModelosDeMedicina_SaoAplicadosAoProcessoPsr()
    {
        Guid processoId = await SemearProcessoPsrAsync();

        // A habilitação pressupõe a forma de conclusão do ensino médio, que a inscrição coleta.
        HttpResponseMessage inscricao = await AplicarAsync(processoId, await ModeloAsync(SementePsrMedicina2027.ModeloDeInscricao));
        inscricao.StatusCode.Should().Be(HttpStatusCode.OK, await inscricao.Content.ReadAsStringAsync());
        HttpResponseMessage habilitacao = await AplicarAsync(processoId, await ModeloAsync(SementePsrMedicina2027.ModeloDeHabilitacao));
        habilitacao.StatusCode.Should().Be(HttpStatusCode.OK, await habilitacao.Content.ReadAsStringAsync());

        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.AsNoTracking()
            .Include(static p => p.Formularios)
            .Include(static p => p.Campos)
            .Include(static p => p.GruposColetados)
            .Include(static p => p.DocumentosExigidos).ThenInclude(static d => d.Condicoes)
            .SingleAsync(p => p.Id == processoId);
        processo.Formularios.Select(static f => f.Finalidade)
            .Should().BeEquivalentTo([FinalidadeFormulario.Inscricao, FinalidadeFormulario.Habilitacao]);
        processo.GruposColetados.Should().ContainSingle(static g => g.Codigo == "COMPOSICAO_FAMILIAR");

        // Cada condição ofertada pede o seu documento a quem solicita atendimento: o laudo, inclusive para
        // a condição que o modelo não conhece, e a certidão da criança para a lactante; a pessoa idosa não
        // apresenta nada. Todos obrigatórios.
        processo.DocumentosExigidos.Select(static d => (d.TipoDocumentoCodigo, CondicaoDe(d))).Should().BeEquivalentTo(
        [
            ("LAUDO_MEDICO", SementePsrMedicina2027.CondicaoForaDaLista),
            ("LAUDO_MEDICO", "VISAO_MONOCULAR"),
            ("CERTIDAO_NASCIMENTO", "LACTANTE"),
        ]);
        processo.DocumentosExigidos.Should().OnlyContain(static d => d.Obrigatorio && d.Finalidade == FinalidadeFormulario.Inscricao);
        processo.DocumentosExigidos.SelectMany(static d => d.Condicoes).Where(static c => c.Fato == "SOLICITA_ATENDIMENTO_ESPECIALIZADO")
            .Should().HaveCount(3, "sem pedir atendimento não há exigência");
        processo.Campos.Select(static c => c.FatoCodigo).Should().Contain(
            ["CAMINHO_FATOR_ATENUACAO", "MUNICIPIO_ENSINO_MEDIO_FATOR_ATENUACAO", "MUNICIPIO_RESIDENCIA_FATOR_ATENUACAO"]);
        processo.Campos.Select(static c => c.FatoCodigo).Should().Contain(
            ["SOLICITA_ATENDIMENTO_ESPECIALIZADO", "CONDICAO_ATENDIMENTO", "DESCRICAO_OUTRA_CONDICAO_ATENDIMENTO", "RECURSO_ACESSIBILIDADE"]);
    }

    private async Task<Guid> SemearProcessoPsrAsync()
    {
        TipoProcessoSnapshot psr = TipoProcessoSnapshot.Criar(
            SementePsrMedicina2027.TipoProcessoId, SementePsrMedicina2027.TipoProcessoCodigo, "Processo Seletivo Regular Unificado").Value!;
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
            db, $"Medicina 2027 {Guid.CreateVersion7()}", complementar: ComBonusRegionalEAtendimento, tipo: psr);
        return processo.Id;
    }

    /// <summary>
    /// O bônus regional com um município, de onde o campo do município do bônus tira as opções; a
    /// oferta de atendimento com a condição fora da lista, cuja escolha a inscrição cita para pedir a
    /// descrição, uma condição que o modelo não conhece, a lactante, a pessoa idosa e um recurso; e uma cota da lei ofertada, porque a inscrição pergunta pela opção
    /// pelas cotas.
    /// </summary>
    private static void ComBonusRegionalEAtendimento(ProcessoSeletivo processo)
    {
        Result distribuida = processo.DefinirDistribuicaoVagas(
            [ConfiguracaoDistribuicaoVagas.Criar(
                Guid.CreateVersion7(), 40, 1m,
                ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)).Value!,
                null, null,
                [
                    Modalidade("AC", NaturezaLegalModalidade.Ampla, PredicadoDnf.SemClausula, 30),
                    Modalidade("LI_EP", NaturezaLegalModalidade.CotaReservada, PredicadoDnf.CriarDeCondicoesAgrupadas(
                        [(0, CondicaoDnf.Criar(OpcaoPelasCotasDaLei.Fato, Operador.Igual, System.Text.Json.JsonSerializer.SerializeToElement(true)).Value!)]).Value!, 10),
                ],
                ofertaCurso: Testes.Compartilhado.OfertaCursoDeTeste.Medicina).Value!],
            Testes.Compartilhado.FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);
        distribuida.IsSuccess.Should().BeTrue(distribuida.Error?.Message);

        ConfiguracaoBonusRegional bonus = ConfiguracaoBonusRegional.Criar(
            ReferenciaRegra.Criar(RegraBonusCodigo.Multiplicativo, "v1", new string('e', 64)).Value!, 1.20m, null, null,
            Guid.CreateVersion7(), "PORTARIA", "Portaria Unifesspa nº 2514/2023", "Institui inclusão regional",
            [("1504208", "Marabá", "PA")]).Value!;
        Result definido = processo.DefinirBonusRegional(aplica: true, bonus: bonus, PrecondicaoIfMatch.Ausente);
        definido.IsSuccess.Should().BeTrue(definido.Error?.Message);

        OfertaAtendimentoEspecializado oferta = OfertaAtendimentoEspecializado.Criar(
            [
                OfertaCondicao.Criar(Guid.CreateVersion7(), SementePsrMedicina2027.CondicaoForaDaLista, "Outra condição específica"),
                OfertaCondicao.Criar(Guid.CreateVersion7(), "VISAO_MONOCULAR", "Visão monocular"),
                OfertaCondicao.Criar(Guid.CreateVersion7(), "LACTANTE", "Lactante"),
                OfertaCondicao.Criar(Guid.CreateVersion7(), "PESSOA_IDOSA", "Pessoa idosa"),
            ],
            [OfertaRecurso.Criar(Guid.CreateVersion7(), "SALA_DE_FACIL_ACESSO", "Sala de fácil acesso")],
            []).Value!;
        Result ofertada = processo.DefinirOfertaAtendimento(oferta, PrecondicaoIfMatch.Ausente);
        ofertada.IsSuccess.Should().BeTrue(ofertada.Error?.Message);

        // O modelo exige os documentos do atendimento na fase de inscrição do cronograma.
        FaseCronograma inscricao = FaseCronograma.Criar(
            2, Guid.CreateVersion7(), "INSCRICAO", "CEPS", OrigemDataFase.Delegada,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: true, coletaSolicitacaoIsencao: false, inicio: null, fim: null,
            produtos: [], faseConcluinteCodigo: null, emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null).Value!;
        Result cronograma = processo.DefinirCronogramaFases([.. processo.CronogramaFases, inscricao], [], PrecondicaoIfMatch.Ausente);
        cronograma.IsSuccess.Should().BeTrue(string.Join("; ", cronograma.Errors.Select(static e => e.Error.Code)) + cronograma.Error?.Message);
    }

    /// <summary>A condição de atendimento cuja declaração torna o documento exigido.</summary>
    private static string CondicaoDe(DocumentoExigido documento) =>
        documento.Condicoes.Single(static c => c.Fato == "CONDICAO_ATENDIMENTO").Valor.GetString()!;

    private static ModalidadeSelecionada Modalidade(string codigo, NaturezaLegalModalidade natureza, PredicadoDnf regra, int vagas) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null, natureza,
            natureza == NaturezaLegalModalidade.Ampla ? ComposicaoVagasModalidade.ResidualDoVo : ComposicaoVagasModalidade.SuplementarAoTotal, null,
            natureza == NaturezaLegalModalidade.CotaReservada ? RegraRemanejamentoModalidade.SegueCascata : RegraRemanejamentoModalidade.Nenhuma,
            null, null, null, regra, null, "Lei 12.711/2012", quantidadeDeclarada: vagas).Value!;

    private async Task<Guid> ModeloAsync(string codigo)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext db = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        return await db.ModelosFormulario.Where(m => m.Codigo == codigo).Select(static m => m.Id).SingleAsync();
    }

    private async Task<HttpResponseMessage> AplicarAsync(Guid processoId, Guid modeloId)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri($"/api/selecao/admin/processos-seletivos/{processoId}/formularios/aplicacoes-de-modelo", UriKind.Relative))
        {
            Content = JsonContent.Create(new { modeloId }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }
}
