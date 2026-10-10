namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

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
    private static readonly IRegistroCodecsEnvelope RegistroCodecs = new RegistroCodecsEnvelope();

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
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, ordem: 1).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        processo.DefinirDistribuicaoVagas(
            [ConfiguracaoDistribuicaoVagas.Criar(
                Guid.CreateVersion7(), 40, 1m, Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'), null, null,
                [modalidade]).Value!], FatosDeModalidadeDeTeste.DoCatalogo,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), 1, [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirCronogramaFases(
            [FaseCronograma.Criar(
                ordem: 1,
                faseCanonicaOrigemId: Guid.CreateVersion7(),
                codigo: "RESULTADO_FINAL",
                donoInstitucional: "CEPS",
                origemData: OrigemDataFase.Propria,
                agrupaEtapas: true,
                permiteComplementacao: false,
                coletaInscricao: true, coletaSolicitacaoIsencao: false,
                inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
                produtos: [ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)],
                faseConcluinteCodigo: null,
                emiteParecerIndividual: false,
                bancasRequeridas: [],
                regraRecurso: null).Value!],
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
            processo, dados, hashDocumento, FusoInstitucional.ZoneId, ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(valoresSelecionaveis)));

        Result<VersaoConfiguracao> publicar = processo.Publicar(
            dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            hashDocumento, "integration-test-user", TimeProvider.System, CorpusEnvelope.ContextoRico(), FatosDeModalidadeDeTeste.DoCatalogo);
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
        // que nenhuma regra da inscrição cita e dois agregados sobre a composição familiar, que o
        // envelope congela e nenhuma regra do formulário cita.
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico(variante: 9);
        EntradaCanonicalizacao entrada = CorpusEnvelope.Entrada(processo);
        SnapshotCanonico congelado = Canonicalizer.Canonicalizar(entrada);
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            entrada.Dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            entrada.HashDocumento, CorpusEnvelope.Ator, TimeProvider.System, CorpusEnvelope.ContextoRico(), FatosDeModalidadeDeTeste.DoCatalogo);
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
                termosExigidos: [.. processo.TermosExigidos], gruposColetados: [.. processo.GruposColetados]),
            entrada.Dados, entrada.HashDocumento, entrada.FusoHorario, retificacao: null, conformidade: null,
            valoresSelecionaveisCongelados: entrada.ValoresSelecionaveisCongelados, agregadosDosGrupos: entrada.AgregadosDosGrupos);
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
        processo.RegrasDerivacao.Should().HaveCount(3);
        lido.Value.Regras.Derivacoes.Should().BeEmpty("nenhuma regra da inscrição cita derivado, e o contrato não publica o que o formulário não usa");
    }

    [Fact(DisplayName = "O pressuposto respondido na inscrição vem com a apresentação congelada dela; o calculado pelo sistema, só com o código")]
    public async Task Handle_Habilitacao_PressupostosComAApresentacaoDaInscricao()
    {
        // A habilitação pede o certificado de quem se declarou preto na inscrição e a declaração de
        // quem é maior de idade, faixa que o sistema calcula da data de nascimento na data de
        // referência dos fatos.
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico(variante: 10);
        FaseCronograma habilitacao = FaseCronograma.Criar(
            processo.CronogramaFases.Max(static f => f.Ordem) + 1, Guid.CreateVersion7(), FormularioProcesso.CodigoFaseHabilitacao, "CEPS",
            OrigemDataFase.Propria, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 5, 10, 18, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar(FormularioProcesso.CodigoFaseHabilitacao, PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null).Value!;
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
            entrada.HashDocumento, CorpusEnvelope.Ator, TimeProvider.System, CorpusEnvelope.ContextoRico(), FatosDeModalidadeDeTeste.DoCatalogo);
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
        faixaEtaria.Rotulo.Should().BeNull("o sistema calcula a faixa etária; nenhum formulário a pergunta");
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
            respostas, new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, FatoResolvido>(StringComparer.Ordinal),
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
