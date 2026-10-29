namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// Issue #1059 (UNI-REQ-0072) — imunidade do formulário público ao que acontece <b>depois</b>
/// da publicação, com Postgres real (Testcontainers).
/// </summary>
/// <remarks>
/// <para>
/// <b>O que esta suíte prova</b>: <c>ObterFormularioRenderizavelQueryHandler.Handle</c> lê
/// SOMENTE <c>VersaoConfiguracao.ConfiguracaoCongelada</c> — nenhum leitor de catálogo entra na
/// sua assinatura (prova estrutural em
/// <see cref="ObterFormularioRenderizavelQueryHandlerTests.Handle_NaoDependeDeLeitorDeCatalogo"/>).
/// O que esta suíte acrescenta é a prova EMPÍRICA, com banco real: os mesmos bytes persistidos
/// no ato de publicação são o que volta em leituras independentes e repetidas — sem cache, sem
/// estado compartilhado entre chamadas, sem qualquer efeito de "o que aconteceu entre uma
/// leitura e outra". As duas provas juntas — assinatura fechada e leitura estável através de
/// Postgres real — são o que sustenta a garantia de imunidade ao catálogo vivo.
/// </para>
/// <para>
/// <b>Decisão de escopo — o que esta suíte NÃO faz</b>: o desenho original desta prova (o
/// plano da issue) previa inativar de fato um valor no catálogo do módulo Configuração e reler
/// o formulário. Reproduzir isso literalmente exigiria uma segunda infraestrutura de banco
/// (<c>ConfiguracaoDbContext</c>, que este projeto de testes não provisiona) e um leitor de
/// catálogo real — escopo maior que esta rodada, e redundante com a garantia estrutural: se o
/// handler não tem PARÂMETRO de leitor de catálogo, inativar um valor no catálogo não tem como
/// alcançar esta leitura, qualquer que seja o valor. Fica registrado como lacuna de cobertura
/// explícita, não como comportamento não verificado.
/// </para>
/// </remarks>
public sealed class FormularioRenderizavelPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();

    // O registro de PRODUÇÃO (não um substituto de teste): a leitura pública real, com
    // Postgres real, tem de passar pelo mesmo gate de versão que o handler usa fora dos
    // testes — reconhecendo hoje somente o codec vivo (ADR-0110 Emenda 2).
    private static readonly RegistroCodecsEnvelope RegistroCodecs = new RegistroCodecsEnvelope();

    private readonly ProcessoSeletivoDbFixture _fixture;

    public FormularioRenderizavelPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    private static ProcessoSeletivo NovoProcessoComFatoDeSelecao(string nome)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            nome, TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar(
                "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, ordem: 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        processo.DefinirDistribuicaoVagas(
            [ConfiguracaoDistribuicaoVagas.Criar(
                Guid.CreateVersion7(), 40, 1m, Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'), null, null,
                [modalidade], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!], FatosDeModalidadeDeTeste.DoCatalogo,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirCronogramaFases(
            [FaseCronograma.Criar(
                ordem: 1,
                faseCanonicaOrigemId: Guid.CreateVersion7(),
                codigo: "RESULTADO_FINAL",
                origemData: OrigemDataFase.NoEdital,
                agrupaEtapas: true,
                permiteComplementacao: false,
                coletaInscricao: true, coletaSolicitacaoIsencao: false,
                inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
                produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
                faseConcluinteCodigo: null,
                emiteParecerIndividual: false,
                bancasRequeridas: [],
                regraRecurso: null,
                unidadeResponsavel: UnidadesDeTeste.Ceps).Value!],
            [],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        processo.DefinirItens([
            FatoColetado.Criar("COR_RACA", 0, "Cor ou raça", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL").Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirTitulo("Formulário de Inscrição", PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirTermos([CorpusEnvelope.Termo("DECLARACAO_VERACIDADE", 0)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        return processo;
    }

    [Fact(DisplayName = "O formulário lido do banco real reproduz os valores selecionáveis congelados na publicação — leituras independentes e repetidas são idênticas")]
    public async Task Handle_ComPostgresReal_ReproduzOsValoresCongeladosEmLeiturasIndependentes()
    {
        ProcessoSeletivo processo = NovoProcessoComFatoDeSelecao(
            nameof(Handle_ComPostgresReal_ReproduzOsValoresCongeladosEmLeiturasIndependentes));

        const string hashDocumento = "2222222222222222222222222222222222222222222222222222222222222222";
        DadosEdital dados = DadosEdital.Criar(
            "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;

        // As SEIS categorias do IBGE — o vocabulário completo que o catálogo declarava íntegro
        // no instante da publicação. É este dicionário, e só ele, que o formulário lido depois
        // tem de reproduzir — nunca uma consulta nova ao catálogo vivo.
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis = new(StringComparer.Ordinal)
        {
            ["COR_RACA"] = [
                new ValorDominioDeclaradoCongelado("BRANCA", "Autodeclaração de cor/raça branca.", 0),
                new ValorDominioDeclaradoCongelado("PRETA", "Autodeclaração de cor/raça preta.", 1),
                new ValorDominioDeclaradoCongelado("PARDA", "Autodeclaração de cor/raça parda.", 2),
                new ValorDominioDeclaradoCongelado("AMARELA", "Autodeclaração de cor/raça amarela.", 3),
                new ValorDominioDeclaradoCongelado("INDIGENA", "Autodeclaração de cor/raça indígena.", 4),
                new ValorDominioDeclaradoCongelado("NAO_DECLARADO", "Prefere não declarar.", 5),
            ],
        };

        SnapshotCanonico congelado = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(
            processo, dados, hashDocumento, FusoInstitucional.ZoneId, ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(valoresSelecionaveis),
            TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo)));

        Result<VersaoConfiguracao> publicar = processo.Publicar(
            dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            hashDocumento, "integration-test-user", TimeProvider.System, CorpusEnvelope.ContextoRico(), FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);

        Guid processoId = processo.Id;

        await PersistirDivulgadoAsync(processo, publicar.Value!, dados);

        // Duas leituras INDEPENDENTES — dois DbContext distintos, a mesma separação que existe
        // entre duas requisições HTTP de fato diferentes. Nenhum leitor de catálogo é injetado:
        // se a resposta dependesse de qualquer estado externo ao byte persistido — cache, campo
        // estático, ordem de execução — as duas leituras poderiam divergir entre si.
        async Task<FormularioRenderizavelDto> LerAsync()
        {
            await using SelecaoDbContext readContext = _fixture.CreateDbContext();
            ProcessoSeletivoRepository repository = new(readContext, TimeProvider.System);
            Result<FormularioRenderizavelDto> resultado = await ObterFormularioRenderizavelQueryHandler.Handle(
                new ObterFormularioRenderizavelQuery(processoId, FinalidadeFormulario.Inscricao), repository,
                new CertameDivulgadoRepository(readContext), RegistroCodecs, AcervoDeTeste.Endereco, CancellationToken.None);
            resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
            return resultado.Value!;
        }

        FormularioRenderizavelDto primeira = await LerAsync();
        FormularioRenderizavelDto segunda = await LerAsync();

        CampoRenderizavel corRaca = primeira.FatosColetados.Single(f => f.FatoCodigo == "COR_RACA");
        corRaca.ValoresSelecionaveis.Should().NotBeNull();
        corRaca.ValoresSelecionaveis!.Select(v => v.Codigo).Should().Equal(
            ["BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_DECLARADO"],
            "o formulário lido do banco traz os SEIS valores congelados na publicação, na ordem canônica — " +
            "os mesmos que o catálogo declarava íntegro no instante em que o certame foi publicado");

        // O valor de uma condição é JSON: as duas leituras se comparam pelo texto dele.
        segunda.Should().BeEquivalentTo(primeira, opcoes => opcoes
                .Using<JsonElement>(c => c.Subject.GetRawText().Should().Be(c.Expectation.GetRawText())).WhenTypeIs<JsonElement>(),
            "duas leituras independentes da MESMA versão publicada — sem catálogo algum na assinatura do " +
            "handler — não têm de onde divergir: o formulário é função pura dos bytes persistidos");
    }

    [Fact(DisplayName = "As regras do formulário público são as da configuração publicada, recortadas para a finalidade, sem derivado que o formulário não cita")]
    public async Task Handle_Regras_SaoAsDaConfiguracaoPublicadaRecortadas()
    {
        // O corpus rico tem seção, termo e grupo exibidos por condição, impedimento, três derivações
        // configuradas e as duas da oferta, que nenhuma regra da inscrição cita e dois agregados sobre a composição familiar, que o
        // envelope congela e nenhuma regra do formulário cita.
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico(variante: 9);
        EntradaCanonicalizacao entrada = CorpusEnvelope.Entrada(processo);
        SnapshotCanonico congelado = Canonicalizer.Canonicalizar(entrada);
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            entrada.Dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            entrada.HashDocumento, CorpusEnvelope.Ator, TimeProvider.System, CorpusEnvelope.ContextoRico(), FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);
        await PersistirDivulgadoAsync(processo, publicar.Value!, entrada.Dados);

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        Result<FormularioRenderizavelDto> lido = await ObterFormularioRenderizavelQueryHandler.Handle(
            new ObterFormularioRenderizavelQuery(processo.Id, FinalidadeFormulario.Inscricao), new ProcessoSeletivoRepository(readContext, TimeProvider.System),
            new CertameDivulgadoRepository(readContext), RegistroCodecs, AcervoDeTeste.Endereco, CancellationToken.None);
        lido.IsSuccess.Should().BeTrue(lido.Error?.Message);

        // A mesma montagem sobre a configuração viva, antes de passar pelo envelope: o que o front
        // interpreta é o que a configuração declarou.
        EnvelopeReidratado vivo = new(
            new GrafoConfiguracao(
                [.. processo.Etapas], processo.OfertaAtendimento!, [.. processo.DistribuicaoVagas], processo.BonusRegional,
                [.. processo.CriteriosDesempate], processo.Classificacao!, [.. processo.CronogramaFases], [.. processo.DocumentosExigidos], [], null,
                fatosColetados: [.. processo.FatosColetados], regrasDerivacao: [.. processo.RegrasDerivacao], formularios: [.. processo.Formularios],
                termosExigidos: [.. processo.TermosExigidos], gruposColetados: [.. processo.GruposColetados],
            numeroDeOpcoesDeCurso: 1),
            entrada.Dados, entrada.HashDocumento, entrada.FusoHorario, retificacao: null, conformidade: null,
            valoresSelecionaveisCongelados: entrada.ValoresSelecionaveisCongelados, agregadosDosGrupos: entrada.AgregadosDosGrupos, apresentacaoDosPressupostos: entrada.ApresentacaoDosPressupostos);
        DefinicaoAvaliavel daConfiguracao = DefinicaoAvaliavelDoProcesso.DoCongelado(vivo).Value!;
        RecorteDaFinalidade esperado = RecorteDaFinalidade.De(
            daConfiguracao.Definicao, DefinicaoDoProcesso.CodigoDaEtapa(FinalidadeFormulario.Inscricao, null), daConfiguracao.Ofertas);

        // O envelope guarda as condições na ordem canônica: a equivalência é a da avaliação, em cada
        // combinação das respostas que as regras da inscrição citam.
        foreach ((string? corRaca, string? renda, bool rural) in
            from cor in new[] { "PRETA", "BRANCA", null }
            from faixa in new[] { "ATE_1_SM", "ACIMA_1_SM", null }
            from trabalhaNoCampo in new[] { true, false }
            select (cor, faixa, trabalhaNoCampo))
        {
            EntradaAvaliacaoFormulario entradaDaAvaliacao = Respostas(corRaca, renda, rural);
            JsonSerializer.Serialize(lido.Value!.Regras.Avaliar(entradaDaAvaliacao).Value, JsonSerializerOptions.Web).Should().Be(
                JsonSerializer.Serialize(esperado.Regras.Avaliar(entradaDaAvaliacao).Value, JsonSerializerOptions.Web),
                $"o envelope preserva cada regra, a oferta e os agregados (cor {corRaca ?? "sem resposta"}, renda {renda ?? "sem resposta"}, campo {rural})");
        }

        lido.Value!.Pressupostos.Select(static p => p.FatoCodigo).Should().Equal(esperado.Pressupostos);
        lido.Value.Regras.Agregados.Should().BeEmpty("nenhuma regra da inscrição cita os agregados da composição familiar");
        lido.Value.Regras.Etapas.Should().Contain(e => e.Exibicao != null, "a seção de pertencimento só aparece para quem se declarou preto");
        processo.DerivacoesEfetivas().Should().HaveCount(5);
        lido.Value.Regras.Derivacoes.Should().BeEmpty("nenhuma regra da inscrição cita derivado, e o contrato não publica o que o formulário não usa");
    }

    [Fact(DisplayName = "O município de residência sai com o rótulo e o tipo do catálogo congelados na publicação; renomear o fato depois muda o rascunho, não o publicado")]
    public async Task Handle_PressupostoDerivado_ApresentacaoCongeladaNaPublicacao()
    {
        ProcessoSeletivo processo = NovoProcessoComFatoDeSelecao(nameof(Handle_PressupostoDerivado_ApresentacaoCongeladaNaPublicacao));
        processo.DefinirItens(
        [
            FatoColetado.Criar("COR_RACA", 0, "Cor ou raça", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL").Value!,
            FatoColetado.Criar("ATENDIMENTO_NO_MUNICIPIO", 1, "Deseja atendimento no município?", TipoRenderizacao.Booleano, Obrigatoriedade.Nunca, [
                CondicaoPrecondicaoFato.Criar(0, "MUNICIPIO_RESIDENCIA", Operador.Igual, JsonSerializer.SerializeToElement("1504208")).Value!,
            ], classificacaoProtecao: "PESSOAL").Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DefinicaoAvaliavel naPublicacao = DefinicaoAvaliavelDoProcesso.DaConfiguracaoViva(processo, CatalogoComOMunicipio("Município de residência")).Value!;
        const string hashDocumento = "3333333333333333333333333333333333333333333333333333333333333333";
        DadosEdital dados = DadosEdital.Criar(
            "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;
        SnapshotCanonico congelado = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(
            processo, dados, hashDocumento, FusoInstitucional.ZoneId,
            ValoresSelecionaveisCongelados: naPublicacao.ValoresSelecionaveis,
            ApresentacaoDosPressupostos: naPublicacao.ApresentacaoDosPressupostos,
            TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo)));
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            hashDocumento, "integration-test-user", TimeProvider.System, CorpusEnvelope.ContextoRico(), FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);
        await PersistirDivulgadoAsync(processo, publicar.Value!, dados);

        PressupostoRenderizavel publicado = Municipio(await LerPublicadoAsync(processo.Id));
        (publicado.Rotulo, publicado.TipoRenderizacao).Should().Be(("Município de residência", "MUNICIPIO"));
        publicado.CalculadoDe.Should().Equal(["ENDERECO_RESIDENCIAL"]);

        IFatoCandidatoReader renomeado = Substitute.For<IFatoCandidatoReader>();
        renomeado.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoComOMunicipio("Cidade onde mora"));
        IResolvedorFusoInstitucional fuso = Substitute.For<IResolvedorFusoInstitucional>();
        fuso.Resolver().Returns(Result<TimeZoneInfo>.Success(TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId)));
        await using SelecaoDbContext rascunhoContext = _fixture.CreateDbContext();
        Result<FormularioRenderizavelDto> rascunho = await ObterFormularioRenderizavelDoRascunhoQueryHandler.Handle(
            new ObterFormularioRenderizavelDoRascunhoQuery(processo.Id, FinalidadeFormulario.Inscricao),
            new ProcessoSeletivoRepository(rascunhoContext, TimeProvider.System), renomeado, fuso, CancellationToken.None);

        rascunho.IsSuccess.Should().BeTrue(rascunho.Error?.Message);
        Municipio(rascunho.Value!).Rotulo.Should().Be("Cidade onde mora", "o rascunho lê o catálogo vivo");
        Municipio(await LerPublicadoAsync(processo.Id)).Rotulo.Should().Be("Município de residência", "o publicado lê o rótulo congelado na versão");
    }

    private static IReadOnlyList<FatoCandidatoView> CatalogoComCorRaca(params FatoValorDominioViewItem[] valores) => CatalogoDoConjuntoBasico.Com(
    [
        new FatoCandidatoView(
            Guid.CreateVersion7(), "COR_RACA", "Cor ou raça", null, "CATEGORICO", "DECLARADO", "ESCALAR",
            [.. valores.Select(static v => v.Codigo)], "INSCRICAO", "CAMPO_FORMULARIO:COR_RACA", [.. valores], "GLOBAL",
            Ativo: true, ClassificacaoProtecao: "PESSOAL"),
    ]);

    private static IReadOnlyList<FatoValorDominioViewItem> CorRacaComoCadastrada() =>
    [
        new("BRANCA", "Branca.", 0, true),
        new("PRETA", "Preta.", 1, true),
    ];

    private static IReadOnlyList<FatoValorDominioViewItem> CorRacaCorrigida() =>
    [
        new("PRETA", "Preta ou negra.", 0, true, "Escolha conforme a sua autodeclaração."),
        new("BRANCA", "Branca (corrigida).", 1, true),
    ];

    private static (string Codigo, string? Descricao, int Ordem, string? Orientacao)[] Projetados(IEnumerable<ValorDominioDeclaradoCongelado> valores) =>
        [.. valores.OrderBy(static v => v.Ordem).Select(static v => (v.Codigo, v.Descricao, v.Ordem, v.Orientacao))];

    private async Task<(ProcessoSeletivo Processo, VersaoConfiguracao Versao, DadosEdital Dados)> PublicarComCorRacaAsync(string nome)
    {
        ProcessoSeletivo processo = NovoProcessoComFatoDeSelecao(nome);
        const string hashDocumento = "4444444444444444444444444444444444444444444444444444444444444444";
        DadosEdital dados = DadosEdital.Criar(
            "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;
        DefinicaoAvaliavel naPublicacao = DefinicaoAvaliavelDoProcesso.DaConfiguracaoViva(processo, CatalogoComCorRaca([.. CorRacaComoCadastrada()])).Value!;
        SnapshotCanonico congelado = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(
            processo, dados, hashDocumento, FusoInstitucional.ZoneId, ValoresSelecionaveisCongelados: naPublicacao.ValoresSelecionaveis,
            TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo)));
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            hashDocumento, "integration-test-user", TimeProvider.System, CorpusEnvelope.ContextoRico(), FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);
        await PersistirDivulgadoAsync(processo, publicar.Value!, dados);
        return (processo, publicar.Value!, dados);
    }

    [Fact(DisplayName = "Corrigir o texto de um valor no catálogo muda o rascunho, e a versão publicada mantém o texto congelado")]
    public async Task Handle_ValorCorrigidoNoCatalogo_RascunhoLeOVivoEPublicadoOCongelado()
    {
        (ProcessoSeletivo processo, _, _) = await PublicarComCorRacaAsync(nameof(Handle_ValorCorrigidoNoCatalogo_RascunhoLeOVivoEPublicadoOCongelado));

        IFatoCandidatoReader catalogoVivo = Substitute.For<IFatoCandidatoReader>();
        catalogoVivo.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoComCorRaca([.. CorRacaCorrigida()]));
        IResolvedorFusoInstitucional fuso = Substitute.For<IResolvedorFusoInstitucional>();
        fuso.Resolver().Returns(Result<TimeZoneInfo>.Success(TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId)));
        await using SelecaoDbContext rascunhoContext = _fixture.CreateDbContext();
        Result<FormularioRenderizavelDto> rascunho = await ObterFormularioRenderizavelDoRascunhoQueryHandler.Handle(
            new ObterFormularioRenderizavelDoRascunhoQuery(processo.Id, FinalidadeFormulario.Inscricao),
            new ProcessoSeletivoRepository(rascunhoContext, TimeProvider.System), catalogoVivo, fuso, CancellationToken.None);

        rascunho.IsSuccess.Should().BeTrue(rascunho.Error?.Message);
        rascunho.Value!.FatosColetados.Single(static f => f.FatoCodigo == "COR_RACA").ValoresSelecionaveis!
            .Select(static v => (v.Codigo, v.Descricao, v.Ordem, v.Orientacao)).Should().Equal(
                ("PRETA", "Preta ou negra.", 0, "Escolha conforme a sua autodeclaração."),
                ("BRANCA", "Branca (corrigida).", 1, null));

        FormularioRenderizavelDto publicado = await LerPublicadoAsync(processo.Id);
        publicado.FatosColetados.Single(static f => f.FatoCodigo == "COR_RACA").ValoresSelecionaveis!
            .Select(static v => (v.Codigo, v.Descricao, v.Ordem, v.Orientacao)).Should().Equal(
                ("BRANCA", "Branca.", 0, null),
                ("PRETA", "Preta.", 1, null));
    }

    [Fact(DisplayName = "A retificação congela a descrição, a orientação e a ordem que o catálogo tem na hora; a versão anterior não muda")]
    public async Task Retificar_ValorCorrigidoNoCatalogo_VersaoNovaCongelaOTextoEAOrdemNovos()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao abertura, _) =
            await PublicarComCorRacaAsync(nameof(Retificar_ValorCorrigidoNoCatalogo_VersaoNovaCongelaOTextoEAOrdemNovos));

        VersaoConfiguracao retificacao;
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, string.Concat(Enumerable.Repeat("ab01234567", 7))[..64], TimeProvider.System).IsSuccess.Should().BeTrue();
        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            ProcessoSeletivo carregado = (await repository.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;
            VersaoConfiguracao versaoAtual = (await repository.ObterVersaoAtualAsync(processo.Id, CancellationToken.None))!;
            DadosEdital dados = DadosEdital.Criar(
                "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), documento.Id).Value!;

            // O catálogo já traz a correção quando a retificação congela.
            DefinicaoAvaliavel naRetificacao = DefinicaoAvaliavelDoProcesso.DaConfiguracaoViva(carregado, CatalogoComCorRaca([.. CorRacaCorrigida()])).Value!;
            SnapshotCanonico congelado = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(
                carregado, dados, documento.HashSha256!, FusoInstitucional.ZoneId,
                new RetificacaoInfo(versaoAtual.AtoCriadorId, "Correção do texto das opções"),
                ValoresSelecionaveisCongelados: naRetificacao.ValoresSelecionaveis,
                TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(carregado)));
            Result<VersaoConfiguracao> retificar = carregado.Retificar(
                dados, versaoAtual, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
                documento.HashSha256!, "integration-test-user", "Correção do texto das opções", TimeProvider.System, CorpusEnvelope.ContextoRico(), FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
            retificar.IsSuccess.Should().BeTrue(retificar.Error?.Message);
            retificacao = retificar.Value!;

            await writeContext.DocumentosAPublicar.AddAsync(documento, CancellationToken.None);
            await repository.AdicionarVersaoConfiguracaoAsync(retificacao, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        VersaoConfiguracao aberturaLida = await readContext.VersoesConfiguracao.AsNoTracking().SingleAsync(v => v.Id == abertura.Id, CancellationToken.None);
        VersaoConfiguracao retificacaoLida = await readContext.VersoesConfiguracao.AsNoTracking().SingleAsync(v => v.Id == retificacao.Id, CancellationToken.None);

        Projetados(RegistroCodecs.Reidratar(retificacaoLida).Value!.ValoresSelecionaveisCongelados!["COR_RACA"]!).Should().Equal(
            ("PRETA", "Preta ou negra.", 0, "Escolha conforme a sua autodeclaração."),
            ("BRANCA", "Branca (corrigida).", 1, null));
        Projetados(RegistroCodecs.Reidratar(aberturaLida).Value!.ValoresSelecionaveisCongelados!["COR_RACA"]!).Should().Equal(
            ("BRANCA", "Branca.", 0, null),
            ("PRETA", "Preta.", 1, null));
    }

    private static PressupostoRenderizavel Municipio(FormularioRenderizavelDto formulario) =>
        formulario.Pressupostos.Single(static p => p.FatoCodigo == "MUNICIPIO_RESIDENCIA");

    private async Task<FormularioRenderizavelDto> LerPublicadoAsync(Guid processoId)
    {
        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        Result<FormularioRenderizavelDto> lido = await ObterFormularioRenderizavelQueryHandler.Handle(
            new ObterFormularioRenderizavelQuery(processoId, FinalidadeFormulario.Inscricao), new ProcessoSeletivoRepository(readContext, TimeProvider.System),
            new CertameDivulgadoRepository(readContext), RegistroCodecs, AcervoDeTeste.Endereco, CancellationToken.None);
        lido.IsSuccess.Should().BeTrue(lido.Error?.Message);
        return lido.Value!;
    }

    private static IReadOnlyList<FatoCandidatoView> CatalogoComOMunicipio(string nomeDoMunicipio) => CatalogoDoConjuntoBasico.Com(
    [
        new FatoCandidatoView(
            Guid.CreateVersion7(), "ATENDIMENTO_NO_MUNICIPIO", "Atendimento no município", null, "BOOLEANO", "DECLARADO", "ESCALAR", null,
            "INSCRICAO", "CAMPO_FORMULARIO:ATENDIMENTO_NO_MUNICIPIO", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL"),
        new FatoCandidatoView(
            Guid.CreateVersion7(), "MUNICIPIO_RESIDENCIA", nomeDoMunicipio, null, "CATEGORICO", "DERIVADO", "ESCALAR", null,
            "INSCRICAO", "SISTEMA:MUNICIPIO_RESIDENCIA", null, "GEO_MUNICIPIO", Ativo: true, ClassificacaoProtecao: "PESSOAL"),
    ]);

    [Fact(DisplayName = "O pressuposto respondido na inscrição vem com a apresentação congelada dela; o calculado pelo sistema, com o rótulo e o tipo congelados do catálogo")]
    public async Task Handle_Habilitacao_PressupostosComAApresentacaoDaInscricao()
    {
        // A habilitação pede o certificado de quem se declarou preto na inscrição e a declaração de
        // quem é maior de idade, faixa que o sistema calcula da data de nascimento na data de
        // referência dos fatos.
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico(variante: 10);
        FaseCronograma habilitacao = FaseCronograma.Criar(
            processo.CronogramaFases.Max(static f => f.Ordem) + 1, Guid.CreateVersion7(), FormularioProcesso.CodigoFaseHabilitacao, OrigemDataFase.NoEdital, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 5, 10, 18, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar(FormularioProcesso.CodigoFaseHabilitacao, PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;
        processo.DefinirCronogramaFases([.. processo.CronogramaFases, habilitacao], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, habilitacao.Id, "Habilitação", FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.DataEspecifica, new DateOnly(2026, 1, 31), null).Value!, PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        Result itens = processo.DefinirItens(
        [
            FatoColetado.Criar("CERTIFICADO_EMITIDO", 0, "Certificado emitido", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, [
                CondicaoPrecondicaoFato.Criar(0, "COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!,
            ], classificacaoProtecao: "PESSOAL").Value!,
            FatoColetado.Criar("DECLARACAO_MAIORIDADE", 1, "Declaração de maioridade", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, [
                CondicaoPrecondicaoFato.Criar(0, "FAIXA_ETARIA", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!,
            ], classificacaoProtecao: "PESSOAL").Value!,
        ], finalidade: FinalidadeFormulario.Habilitacao);
        itens.IsSuccess.Should().BeTrue(itens.Error?.Message);

        EntradaCanonicalizacao entrada = CorpusEnvelope.Entrada(processo);
        SnapshotCanonico congelado = Canonicalizer.Canonicalizar(entrada);
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            entrada.Dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            entrada.HashDocumento, CorpusEnvelope.Ator, TimeProvider.System, CorpusEnvelope.ContextoRico(), FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);
        await PersistirDivulgadoAsync(processo, publicar.Value!, entrada.Dados);

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        Result<FormularioRenderizavelDto> lido = await ObterFormularioRenderizavelQueryHandler.Handle(
            new ObterFormularioRenderizavelQuery(processo.Id, FinalidadeFormulario.Habilitacao), new ProcessoSeletivoRepository(readContext, TimeProvider.System),
            new CertameDivulgadoRepository(readContext), RegistroCodecs, AcervoDeTeste.Endereco, CancellationToken.None);

        lido.IsSuccess.Should().BeTrue(lido.Error?.Message);
        lido.Value!.Pressupostos.Select(static p => p.FatoCodigo).Should().Equal("COR_RACA", "FAIXA_ETARIA");
        PressupostoRenderizavel corRaca = lido.Value.Pressupostos[0];
        corRaca.Rotulo.Should().Be("Cor ou raça");
        corRaca.TipoRenderizacao.Should().Be("SELECAO_UNICA");
        corRaca.ValoresSelecionaveis!.Select(static v => v.Codigo).Should().Equal(["BRANCA", "PRETA", "PARDA"], "a simulação pergunta o dado anterior com os valores congelados");
        PressupostoRenderizavel faixaEtaria = lido.Value.Pressupostos[1];
        faixaEtaria.Rotulo.Should().Be("Faixa etária", "nenhum formulário a pergunta, e o rótulo é o do catálogo congelado na versão");
        faixaEtaria.TipoRenderizacao.Should().Be("NUMERO");
        faixaEtaria.CalculadoDe.Should().Equal(["DATA_NASCIMENTO"]);
        lido.Value.DataReferenciaFatos.Should().Be(new DateOnly(2026, 1, 31), "a âncora da faixa etária é a congelada na publicação");
    }

    private static EntradaAvaliacaoFormulario Respostas(string? corRaca, string? renda, bool trabalhaNoCampo)
    {
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal);
        if (corRaca is not null)
        {
            respostas["COR_RACA"] = JsonSerializer.SerializeToElement(corRaca);
        }

        if (renda is not null)
        {
            respostas["RENDA"] = JsonSerializer.SerializeToElement(renda);
        }

        return new EntradaAvaliacaoFormulario(
            respostas, new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), Envio: null,
            new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal)
            {
                ["COMPOSICAO_FAMILIAR"] =
                [
                    new("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["TRABALHADOR_RURAL"] = JsonSerializer.SerializeToElement(trabalhaNoCampo),
                    }),
                ],
            });
    }

    /// <summary>
    /// Grava o processo, a versão publicada e a divulgação. No caminho real a divulgação nasce quando
    /// o ato normativo se confirma no registro central; aqui é semeada direto, porque o que se
    /// exercita é a projeção.
    /// </summary>
    private async Task PersistirDivulgadoAsync(ProcessoSeletivo processo, VersaoConfiguracao versao, DadosEdital dados)
    {
        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
        await repository.AdicionarAsync(processo, CancellationToken.None);
        await repository.AdicionarVersaoConfiguracaoAsync(versao, CancellationToken.None);
        await new CertameDivulgadoRepository(writeContext).AdicionarAsync(
            CertameDivulgado.Criar(
                processo.Id,
                versao.NumeroVersao,
                versao.AtoCriadorId,
                new string('a', 64),
                versaoProjecao: "1",
                new FacetasDoCertameDivulgado(
                    processo.IdentificadorLegivel!.Value.Valor, processo.Nome, dados.Numero, ["AC"], dados.PeriodoInscricaoInicio, dados.PeriodoInscricaoFim),
                """{"nome":"documento"}""",
                TimeProvider.System.GetUtcNow()),
            CancellationToken.None);
        await writeContext.SaveChangesAsync(CancellationToken.None);
    }
}
