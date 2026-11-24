namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Sementes;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os modelos de inscrição (com e sem prova) e de habilitação do PSR, semeados pela migration da
/// Configuração, são aplicados a um processo PSR em rascunho, com bônus regional, o
/// atendimento especializado e cota da lei ofertada, pelo passo Formulários, sem recusa.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class AplicarModelosPsrMedicina2027Tests
{
    private readonly CascadingFixture _fixture;

    public AplicarModelosPsrMedicina2027Tests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "A inscrição com prova e a habilitação são aplicadas ao processo PSR com bônus regional e atendimento especializado, com os municípios do bônus, as condições e os recursos, os documentos dele e a composição familiar")]
    public async Task ModelosComProva_SaoAplicadosAoProcessoPsr()
    {
        ProcessoSeletivo processo = await AplicarInscricaoEHabilitacaoAsync(SementePsrMedicina2027.ModeloDeInscricaoComProva);

        processo.Formularios.Select(static f => f.Finalidade)
            .Should().BeEquivalentTo([FinalidadeFormulario.Inscricao, FinalidadeFormulario.Habilitacao]);
        processo.GruposColetados.Should().ContainSingle(static g => g.Codigo == "COMPOSICAO_FAMILIAR");
        AfirmarDocumentosDoAtendimento(processo);
        AfirmarCamposDoBonusEDoAtendimento(processo);
    }

    [Fact]
    public async Task ModeloComProva_Aplicado_AtendeConferenciaDaCidadeDeProva()
    {
        ProcessoSeletivo processo = await AplicarInscricaoEHabilitacaoAsync(SementePsrMedicina2027.ModeloDeInscricaoComProva);
        processo.DefinirProvaPresencial(true, [new("1504208", "PA")], 100).IsSuccess.Should().BeTrue();

        processo.Campos.Should().ContainSingle(static c => c.FatoCodigo == "CIDADE_DE_PROVA")
            .Which.OrigemValores.Should().Be(OrigemValoresColeta.MunicipiosDaProvaPresencial);
        CidadeDeProvaNosFormularios.Conferir(processo.Declaracao()).Should().BeEmpty();
    }

    [Fact(DisplayName = "A inscrição sem prova e a habilitação são aplicadas ao processo PSR que oferta atendimento, sem pergunta de atendimento nem documento do atendimento")]
    public async Task ModelosSemProva_SaoAplicadosSemAtendimentoEspecializado()
    {
        ProcessoSeletivo processo = await AplicarInscricaoEHabilitacaoAsync(SementePsrMedicina2027.ModeloDeInscricaoSemProva);

        processo.Formularios.Select(static f => f.Finalidade)
            .Should().BeEquivalentTo([FinalidadeFormulario.Inscricao, FinalidadeFormulario.Habilitacao]);
        processo.DocumentosExigidos.Should().BeEmpty();
        processo.Campos.Select(static c => c.FatoCodigo).Should().NotContain(
            ["SOLICITA_ATENDIMENTO_ESPECIALIZADO", "CONDICAO_ATENDIMENTO", "DESCRICAO_OUTRA_CONDICAO_ATENDIMENTO", "RECURSO_ACESSIBILIDADE"]);
        processo.Campos.Select(static c => c.FatoCodigo).Should().Contain("TIPO_DEFICIENCIA");
    }

    [Fact(DisplayName = "No processo sem prova e sem atendimento especializado, o tipo de deficiência da inscrição oferece os tipos ativos do cadastro")]
    public async Task ModeloSemProva_SemAtendimentoEspecializado_TipoDeficienciaOfereceOsTiposDoCadastro()
    {
        (TipoDeficiencia ativo, TipoDeficiencia removido) = await SemearUmTipoAtivoEUmRemovidoAsync();
        try
        {
            ProcessoSeletivo processo = await AplicarInscricaoEHabilitacaoAsync(SementePsrMedicina2027.ModeloDeInscricaoSemProva, ofertarAtendimento: false);
            processo.AtendimentoEspecializado.Should().BeNull();
            string[] tiposAtivos = await ListarCodigosDosTiposAtivosAsync();
            string[] opcoes = await ListarOpcoesDoTipoDeficienciaDaInscricaoAsync(processo.Id);
            opcoes.Should().Equal(tiposAtivos);
            opcoes.Should().Contain(ativo.Codigo.Valor).And.NotContain(removido.Codigo.Valor);
        }
        finally
        {
            await ApagarTiposDeDeficienciaAsync(ativo.Id, removido.Id);
        }
    }

    private static void AfirmarDocumentosDoAtendimento(ProcessoSeletivo processo)
    {
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
    }

    private static void AfirmarCamposDoBonusEDoAtendimento(ProcessoSeletivo processo)
    {
        processo.Campos.Select(static c => c.FatoCodigo).Should().Contain(
            ["CAMINHO_FATOR_ATENUACAO", "MUNICIPIO_ENSINO_MEDIO_FATOR_ATENUACAO", "MUNICIPIO_RESIDENCIA_FATOR_ATENUACAO"]);
        processo.Campos.Select(static c => c.FatoCodigo).Should().Contain(
            ["SOLICITA_ATENDIMENTO_ESPECIALIZADO", "CONDICAO_ATENDIMENTO", "DESCRICAO_OUTRA_CONDICAO_ATENDIMENTO", "RECURSO_ACESSIBILIDADE"]);
    }

    private async Task<(TipoDeficiencia Ativo, TipoDeficiencia Removido)> SemearUmTipoAtivoEUmRemovidoAsync()
    {
        string prefixo = $"DEF_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
        TipoDeficiencia ativo = TipoDeficiencia.Criar($"{prefixo}_ATIVO", "Tipo ativo", "Descrição de teste").Value!;
        TipoDeficiencia removido = TipoDeficiencia.Criar($"{prefixo}_REMOVIDO", "Tipo removido", "Descrição de teste").Value!;
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        db.TiposDeficiencia.AddRange(ativo, removido);
        await db.SaveChangesAsync();
        db.TiposDeficiencia.Remove(removido);
        await db.SaveChangesAsync();
        return (ativo, removido);
    }

    private async Task<string[]> ListarCodigosDosTiposAtivosAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return [.. (await db.TiposDeficiencia.AsNoTracking().ToListAsync())
            .Select(static t => t.Codigo.Valor).Order(StringComparer.Ordinal)];
    }

    private async Task<string[]> ListarOpcoesDoTipoDeficienciaDaInscricaoAsync(Guid processoId)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Get, new Uri($"/api/selecao/admin/processos-seletivos/{processoId}/formularios/INSCRICAO/renderizavel", UriKind.Relative));
        Autenticar(request);
        HttpResponseMessage resposta = await client.SendAsync(request);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());

        JsonElement campo = (await resposta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("formulario").GetProperty("coleta").GetProperty("itens").EnumerateArray()
            .Single(static c => c.GetProperty("fatoCodigo").GetString() == "TIPO_DEFICIENCIA");
        return [.. campo.GetProperty("valoresSelecionaveis").EnumerateArray().Select(static v => v.GetProperty("codigo").GetString()!)];
    }

    private async Task ApagarTiposDeDeficienciaAsync(Guid ativoId, Guid removidoId)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await db.TiposDeficiencia.IgnoreQueryFilters().Where(t => t.Id == ativoId || t.Id == removidoId).ExecuteDeleteAsync();
    }

    private async Task<ProcessoSeletivo> AplicarInscricaoEHabilitacaoAsync(string modeloDeInscricao, bool ofertarAtendimento = true)
    {
        Guid processoId = await SemearProcessoPsrAsync(ofertarAtendimento);

        // A habilitação pressupõe a forma de conclusão do ensino médio, que a inscrição coleta.
        await AplicarModeloEAfirmarSucessoAsync(processoId, modeloDeInscricao);
        await AplicarModeloEAfirmarSucessoAsync(processoId, SementePsrMedicina2027.ModeloDeHabilitacao);

        return await CarregarProcessoComFormulariosAsync(processoId);
    }

    private async Task AplicarModeloEAfirmarSucessoAsync(Guid processoId, string codigoDoModelo)
    {
        HttpResponseMessage resposta = await AplicarAsync(processoId, await ModeloAsync(codigoDoModelo));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
    }

    private async Task<ProcessoSeletivo> CarregarProcessoComFormulariosAsync(Guid processoId)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await db.ProcessosSeletivos.AsNoTracking()
            .Include(static p => p.Formularios)
            .Include(static p => p.Campos)
            .Include(static p => p.GruposColetados)
            .Include(static p => p.DocumentosExigidos).ThenInclude(static d => d.Condicoes)
            .SingleAsync(p => p.Id == processoId);
    }

    private async Task<Guid> SemearProcessoPsrAsync(bool ofertarAtendimento)
    {
        TipoProcessoSnapshot psr = TipoProcessoSnapshot.Criar(
            SementePsrMedicina2027.TipoProcessoId, SementePsrMedicina2027.TipoProcessoCodigo, "Processo Seletivo Regular Unificado").Value!;
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"Medicina 2027 {Guid.CreateVersion7()}", Complementar = p => ComBonusRegionalEAtendimento(p, ofertarAtendimento), Tipo = psr });
        return processo.Id;
    }

    private static FaseCronograma FaseDeInscricao() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: true, ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(2, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    /// <summary>
    /// O bônus regional com um município, de onde o campo do município do bônus tira as opções; a
    /// atendimento especializado com a condição fora da lista, cuja escolha a inscrição cita para pedir a
    /// descrição, uma condição que o modelo não conhece, a lactante, a pessoa idosa e um recurso; e uma cota da lei ofertada, porque a inscrição pergunta pela opção
    /// pelas cotas.
    /// </summary>
    private static void ComBonusRegionalEAtendimento(ProcessoSeletivo processo, bool ofertarAtendimento)
    {
        DefinirDistribuicaoComCotaDaLei(processo);
        DefinirBonusRegionalEmMaraba(processo);
        DefinirClassificacaoPelasNotasDoEnem(processo);

        if (ofertarAtendimento)
        {
            DefinirAtendimentoEspecializadoOfertado(processo);
        }

        DefinirFaseDeInscricaoNoCronograma(processo);
    }

    private static void DefinirDistribuicaoComCotaDaLei(ProcessoSeletivo processo)
    {
        Result distribuida = processo.DefinirDistribuicaoVagas(
            [DistribuicaoComAmplaEComCotaDaLei()], Testes.Compartilhado.FatosDeModalidadeDeTeste.DoCatalogo);
        distribuida.IsSuccess.Should().BeTrue(distribuida.Error?.Message);
    }

    private static ConfiguracaoDistribuicaoVagas DistribuicaoComAmplaEComCotaDaLei() =>
        ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada
            {
                VagasOfertadasBase = 40,
                PercentualDaReservaLegal = 1m,
                RegraDeDistribuicao = ReferenciaRegra.De(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1", new string('a', 64)),
                Modalidades = [ModalidadeAmpla(), ModalidadeDeCotaDaLei()],
            },
            new FatosDaOfertaNoCadastro { OfertaCurso = Testes.Compartilhado.OfertaCursoDeTeste.Medicina }).Value!;

    private static ModalidadeSelecionada ModalidadeAmpla() =>
        Modalidade("AC", NaturezaLegalModalidade.Ampla, PredicadoDnf.SemClausula, 30);

    private static ModalidadeSelecionada ModalidadeDeCotaDaLei() =>
        Modalidade("LI_EP", NaturezaLegalModalidade.CotaReservada, PredicadoDnf.CriarDeCondicoesAgrupadas([(0, OpcaoPelasCotasDaLeiMarcada())]).Value!, 10);

    private static CondicaoDnf OpcaoPelasCotasDaLeiMarcada() =>
        CondicaoDnf.Criar(OpcaoPelasCotasDaLei.Fato, Operador.Igual, System.Text.Json.JsonSerializer.SerializeToElement(true)).Value!;

    private static void DefinirBonusRegionalEmMaraba(ProcessoSeletivo processo)
    {
        ConfiguracaoBonusRegional bonus = ConfiguracaoBonusRegional.Criar(ReferenciaRegra.De(RegraDeBonus.Multiplicativo.Codigo(), "v1", new string('e', 64)), 1.20m, null, null, new BaseLegalCongelada(Guid.CreateVersion7(), "PORTARIA_UNIFESSPA_NO_2514_2023", "PORTARIA", "Portaria Unifesspa nº 2514/2023", "Institui inclusão regional", [("1504208", "Marabá", "PA")])).Value!;
        Result definido = processo.DefinirBonusRegional(aplica: true, bonus: bonus);
        definido.IsSuccess.Should().BeTrue(definido.Error?.Message);
    }

    // A classificação pelas notas do ENEM e a edição que a inscrição pergunta: sem ela o formulário
    // não teria o que oferecer.
    private static void DefinirClassificacaoPelasNotasDoEnem(ProcessoSeletivo processo)
    {
        Result classificada = processo.DefinirClassificacao(ClassificacaoPelasNotasDoEnem());
        classificada.IsSuccess.Should().BeTrue(classificada.Error?.Message);
        Result edicoes = processo.DefinirOpcoesDeclaradas(
            EdicaoDoEnem.Fato, [OpcaoDeclaradaFato.Criar(EdicaoDoEnem.Fato, "2025", "ENEM 2025", 0).Value!]);
        edicoes.IsSuccess.Should().BeTrue(edicoes.Error?.Message);
    }

    private static ConfiguracaoClassificacao ClassificacaoPelasNotasDoEnem() =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ReferenciaRegra.De(RegraDeCalculoDaNota.MediaPonderada.Codigo(), "v1", new string('c', 64)),
            RegraArredondamento = ReferenciaRegra.De(RegraDeArredondamentoDaNota.Truncar.Codigo(), "v1", new string('d', 64)),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = ReferenciaRegra.De(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", new string('f', 64)),
            BaseadoEmEnem = true,
            ResolucaoPesoAreaEnem = Testes.Compartilhado.QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnem = Testes.Compartilhado.QuadroPesoAreaEnemDeTeste.Completo(),
            EscalaNotaFinal = 100m,
        }).Value!;

    private static void DefinirAtendimentoEspecializadoOfertado(ProcessoSeletivo processo)
    {
        AtendimentoEspecializado atendimento = AtendimentoEspecializado.Criar(
            [
                CondicaoOfertada.Criar(Guid.CreateVersion7(), SementePsrMedicina2027.CondicaoForaDaLista, "Outra condição específica"),
                CondicaoOfertada.Criar(Guid.CreateVersion7(), "VISAO_MONOCULAR", "Visão monocular"),
                CondicaoOfertada.Criar(Guid.CreateVersion7(), "LACTANTE", "Lactante"),
                CondicaoOfertada.Criar(Guid.CreateVersion7(), "PESSOA_IDOSA", "Pessoa idosa"),
            ],
            [RecursoOfertado.Criar(Guid.CreateVersion7(), "SALA_DE_FACIL_ACESSO", "Sala de fácil acesso")],
            []).Value!;
        Result ofertada = processo.DefinirAtendimentoEspecializado(atendimento);
        ofertada.IsSuccess.Should().BeTrue(ofertada.Error?.Message);
    }

    // O modelo exige os documentos do atendimento na fase de inscrição do cronograma.
    private static void DefinirFaseDeInscricaoNoCronograma(ProcessoSeletivo processo)
    {
        FaseCronograma inscricao = FaseDeInscricao();
        Result cronograma = processo.DefinirCronogramaFases([.. processo.CronogramaFases, inscricao], []);
        cronograma.IsSuccess.Should().BeTrue(string.Join("; ", cronograma.Errors.Select(static e => e.Error.Code)) + cronograma.Error?.Message);
    }

    /// <summary>A condição de atendimento cuja declaração torna o documento exigido.</summary>
    private static string CondicaoDe(DocumentoExigido documento) =>
        documento.Condicoes.Single(static c => c.Fato == "CONDICAO_ATENDIMENTO").Valor.GetString()!;

    private static ModalidadeSelecionada Modalidade(string codigo, NaturezaLegalModalidade natureza, PredicadoDnf regra, int vagas) =>
        ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da(codigo, natureza, natureza == NaturezaLegalModalidade.Ampla ? ComposicaoVagasModalidade.RestanteDasVagasOfertadas : ComposicaoVagasModalidade.SuplementarAoTotal) with { RegraRemanejamento = natureza == NaturezaLegalModalidade.CotaReservada ? RegraRemanejamentoModalidade.SegueOrdemDeRemanejamento : null, RegraDeAplicabilidade = regra }, vagas).Value!;

    private async Task<Guid> ModeloAsync(string codigo)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
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
        Autenticar(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }

    private static void Autenticar(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
    }
}
