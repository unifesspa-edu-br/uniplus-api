namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Npgsql;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Issue #1858 — a fixture de integração que o escopo pede: um processo publicado e divulgado,
/// servido por <see cref="IniciarInscricaoCommandHandler"/> e pelo <see cref="InscricaoRepository"/>
/// contra Postgres real, com os repositórios de produção (<see cref="ProcessoSeletivoRepository"/>,
/// <see cref="CertameDivulgadoRepository"/>) e o codec vivo de produção (<see cref="RegistroCodecsEnvelope"/>)
/// — não um grafo fabricado à parte e repositórios substitutos, como a cobertura unitária de
/// <c>IniciarInscricaoCommandHandlerTests</c> já faz para a resolução da versão divulgada, e como
/// <c>InscricaoPersistenceTests</c> já faz para a mecânica de cifra isolada. Aqui as duas peças
/// giram juntas, pela primeira vez, contra um certame de fato publicado e divulgado.
/// </summary>
/// <remarks>
/// O fato <c>Identificador</c> declarado (RG do candidato e do membro do grupo) é genérico — a
/// classificação de proteção não é exclusiva de nenhum modelo de edital — porque o catálogo
/// semeado da Medicina ainda não declara nenhum fato <c>Identificador</c> próprio: os números de
/// documento do conjunto básico do candidato já cobrem o caso, e a Medicina continua sendo um caso
/// de validação da regra genérica, não uma regra à parte.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class InscricaoSobreProcessoPublicadoPersistenciaTests : IClassFixture<InscricaoDbFixture>
{
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();
    private static readonly IRegistroCodecsEnvelope RegistroCodecs = new RegistroCodecsEnvelope();

    private readonly InscricaoDbFixture _fixture;

    public InscricaoSobreProcessoPublicadoPersistenciaTests(InscricaoDbFixture fixture) => _fixture = fixture;

    private static ProcessoSeletivo NovoProcessoDeInscricao(
        string nome, OfertaAtendimentoEspecializado? oferta = null, IReadOnlyList<FatoColetado>? camposAdicionais = null)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            nome, TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                "Prova Objetiva", CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
                peso: 1m, ordem: 1).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(oferta ?? OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        processo.DefinirDistribuicaoVagas(
            [ConfiguracaoDistribuicaoVagas.Criar(
                Guid.CreateVersion7(), 40, 1m,
                ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)).Value!,
                null, null, [modalidade], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!],
            FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", new string('b', 64)).Value!, null, null,
            ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", new string('c', 64)).Value!,
            1, [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirCronogramaFases(
            [FaseCronograma.Criar(
                ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "RESULTADO_FINAL", donoInstitucional: "CEPS",
                origemData: OrigemDataFase.Propria, agrupaEtapas: true, permiteComplementacao: false,
                coletaInscricao: true, coletaSolicitacaoIsencao: false,
                inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
                produtos: [ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)],
                faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null).Value!],
            [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        GrupoColetado composicaoFamiliar = GrupoColetado.Criar(
            "COMPOSICAO_FAMILIAR", 0, etapaCodigo: FormularioDeTeste.Secao, "Composição familiar", minimo: 0, maximo: null, exibicao: null,
            Obrigatoriedade.Sempre,
            [
                FatoColetado.Criar(
                    "RG_NUMERO_MEMBRO", 0, "RG do membro", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
                    classificacaoProtecao: "IDENTIFICADOR", formato: "LIVRE").Value!,
                FatoColetado.Criar(
                    "PARENTESCO", 1, "Parentesco", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
                    classificacaoProtecao: "PESSOAL", formato: "LIVRE").Value!,
            ],
            FinalidadeFormulario.Inscricao).Value!;

        FatoColetado rg = FatoColetado.Criar(
            "RG_NUMERO", 0, "RG", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "IDENTIFICADOR", formato: "LIVRE").Value!;
        processo.DefinirItens(
            [rg, .. camposAdicionais ?? []],
            PrecondicaoIfMatch.Ausente,
            grupos: [composicaoFamiliar]).IsSuccess.Should().BeTrue();

        processo.DefinirTitulo("Formulário de Inscrição").IsSuccess.Should().BeTrue();
        processo.DefinirTermos([CorpusEnvelope.Termo("DECLARACAO_VERACIDADE", 0)]).IsSuccess.Should().BeTrue();

        return processo;
    }

    /// <summary>Publica e divulga o processo com os repositórios de produção, no mesmo <paramref name="db"/>.</summary>
    private static async Task<(Guid ProcessoId, DadosEdital Dados)> PublicarEDivulgarAsync(SelecaoDbContext db, ProcessoSeletivo processo)
    {
        await db.ProcessosSeletivos.AddAsync(processo);
        await db.SaveChangesAsync();

        const string hashDocumento = "3333333333333333333333333333333333333333333333333333333333333333333";
        string hash64 = hashDocumento[..64];
        DadosEdital dados = DadosEdital.Criar(
            "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;

        // O conjunto básico do candidato (sempre presente num formulário de inscrição) traz sete
        // fatos de seleção (nacionalidade, estado civil, sexo, cor/raça, tipo de localidade, UF do
        // RG e UF de naturalidade) — o canonicalizador exige uma entrada não vazia para cada; os
        // valores em si não importam para o que este teste prova (a resolução da versão divulgada e
        // a cifra dos documentos).
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresDoConjuntoBasico = new(StringComparer.Ordinal)
        {
            ["NACIONALIDADE"] = [new ValorDominioDeclaradoCongelado("BRASILEIRA", "Nacionalidade brasileira.", 0)],
            ["ESTADO_CIVIL"] = [new ValorDominioDeclaradoCongelado("SOLTEIRO", "Solteiro(a).", 0)],
            ["SEXO"] = [new ValorDominioDeclaradoCongelado("FEMININO", "Feminino.", 0)],
            ["COR_RACA"] = [new ValorDominioDeclaradoCongelado("NAO_DECLARADO", "Prefere não declarar.", 0)],
            ["TIPO_ENDERECO"] = [new ValorDominioDeclaradoCongelado("URBANO", "Endereço urbano.", 0)],
            ["RG_UF"] = [new ValorDominioDeclaradoCongelado("PA", "Pará.", 0)],
            ["NATURALIDADE_UF"] = [new ValorDominioDeclaradoCongelado("PA", "Pará.", 0)],
        };

        // O campo cujas opções o processo oferta congela essas opções, pela mesma leitura que o
        // resolvedor da publicação usa.
        foreach (FatoColetado campo in processo.Campos.Where(static c => ProcessoSeletivo.OrigemDasOpcoes(c) == OrigemValoresColeta.OpcoesDoProcesso))
        {
            valoresDoConjuntoBasico[campo.FatoCodigo] = [.. processo.OpcoesDoProcesso(campo.FatoCodigo)
                .Select(static o => new ValorDominioDeclaradoCongelado(o.Codigo, o.Rotulo, o.Ordem))];
        }

        SnapshotCanonico congelado = Canonicalizer.Canonicalizar(
            new EntradaCanonicalizacao(processo, dados, hash64, FusoInstitucional.ZoneId, ValoresSelecionaveisCongelados: valoresDoConjuntoBasico));

        Result<VersaoConfiguracao> publicar = processo.Publicar(
            dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash, hash64,
            "integration-test-user", TimeProvider.System, CorpusEnvelope.ContextoRico(), FatosDeModalidadeDeTeste.DoCatalogo);
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);

        ProcessoSeletivoRepository processoRepository = new(db, TimeProvider.System);
        await processoRepository.AdicionarVersaoConfiguracaoAsync(publicar.Value!, CancellationToken.None);
        await new CertameDivulgadoRepository(db).AdicionarAsync(
            CertameDivulgado.Criar(
                processo.Id, publicar.Value!.NumeroVersao, publicar.Value!.AtoCriadorId, hash64, versaoProjecao: "1",
                new FacetasDoCertameDivulgado(
                    processo.IdentificadorLegivel!.Value.Valor, processo.Nome, dados.Numero, ["AC"],
                    dados.PeriodoInscricaoInicio, dados.PeriodoInscricaoFim),
                """{"nome":"documento"}""", TimeProvider.System.GetUtcNow()),
            CancellationToken.None);
        await db.SaveChangesAsync();

        return (processo.Id, dados);
    }

    /// <summary>Dentro da janela da fase que coleta a inscrição no processo publicado por este teste.</summary>
    private static readonly TimeProvider DentroDoPeriodo = new RelogioFixo(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

    private static IResolvedorFusoInstitucional Fuso()
    {
        IResolvedorFusoInstitucional fuso = Substitute.For<IResolvedorFusoInstitucional>();
        fuso.Resolver().Returns(Result<TimeZoneInfo>.Success(TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId)));
        return fuso;
    }

    private static IUserContext UsuarioComCpf(Cpf cpf)
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.UserId.Returns("sub-fernanda-drumond");
        contexto.Issuer.Returns("https://idp.unifesspa.edu.br/realms/unifesspa");
        return contexto;
    }

    [Fact(DisplayName = "As definições das respostas da versão fixada marcam como número de documento os fatos Identificador do grafo reidratado, inclusive o de grupo")]
    public async Task Handle_ComProcessoPublicadoEDivulgadoReal_DefinicoesDasRespostasMarcamOsFatosIdentificador()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricao(
            nameof(Handle_ComProcessoPublicadoEDivulgadoReal_DefinicoesDasRespostasMarcamOsFatosIdentificador));

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(writeContext, processo);

        InscricaoRepository inscricaoRepository = new(writeContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        ProcessoSeletivoRepository processoRepository = new(writeContext, TimeProvider.System);
        CertameDivulgadoRepository divulgadoRepository = new(writeContext);

        Result<InscricaoDto> iniciar = await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(processoId),
            inscricaoRepository, divulgadoRepository, processoRepository, RegistroCodecs, Fuso(), DentroDoPeriodo, writeContext,
            UsuarioComCpf(PersonasDeTeste.FernandaDrumond), CancellationToken.None);

        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);
        iniciar.Value!.Respostas.Should().BeEmpty("a inscrição nasce em rascunho, sem resposta nenhuma");

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        InscricaoRepository readRepository = new(readContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        Inscricao? gravada = await readRepository.ObterPorIdAsync(iniciar.Value!.Id);

        gravada.Should().NotBeNull();
        Result<ContextoDaVersaoFixada> contexto = await ContextoDaVersaoFixadaResolver.ResolverAsync(
            gravada!.VersaoFormularioId, processoId, new ProcessoSeletivoRepository(readContext, TimeProvider.System), RegistroCodecs, CancellationToken.None);
        contexto.IsSuccess.Should().BeTrue(contexto.Error?.Message);
        contexto.Value!.Admitidas.Definicoes.Where(static f => f.Value.Identificador).Select(static f => f.Key).Should().BeEquivalentTo(
            ["CPF", "RG_NUMERO", "PASSAPORTE_NUMERO", "RNM_NUMERO", "RG_NUMERO_MEMBRO"],
            "o conjunto junta os fatos Identificador do conjunto básico do candidato (CPF, RG, passaporte e RNM) " +
            "com o do campo de grupo declarado por este teste — da classificação CONGELADA na publicação, não do catálogo vivo");
    }

    [Fact(DisplayName = "Salvar um rascunho sobre a versão fixada cifra o RG e o RG do membro do grupo, sem expor nada em texto claro na linha crua")]
    public async Task Salvar_SobreVersaoFixadaReal_CifraDocumentosDeNivelSuperiorEDeGrupo()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricao(
            nameof(Salvar_SobreVersaoFixadaReal_CifraDocumentosDeNivelSuperiorEDeGrupo));

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(writeContext, processo);

        InscricaoRepository inscricaoRepository = new(writeContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        Result<InscricaoDto> iniciar = await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(processoId),
            inscricaoRepository, new CertameDivulgadoRepository(writeContext), new ProcessoSeletivoRepository(writeContext, TimeProvider.System),
            RegistroCodecs, Fuso(), DentroDoPeriodo, writeContext, UsuarioComCpf(PersonasDeTeste.KevinCarvalho), CancellationToken.None);
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);

        Inscricao? inscricao = await inscricaoRepository.ObterPorIdAsync(iniciar.Value!.Id);
        inscricao.Should().NotBeNull();
        RespostasAdmitidas admitidas = (await ContextoDaVersaoFixadaResolver.ResolverAsync(
            inscricao!.VersaoFormularioId, processoId, new ProcessoSeletivoRepository(writeContext, TimeProvider.System), RegistroCodecs, CancellationToken.None))
            .Value!.Admitidas;

        inscricao!.Salvar(
            null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["RG_NUMERO"] = JsonSerializer.SerializeToElement("30.111.222-9"),
            },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal)
            {
                ["COMPOSICAO_FAMILIAR"] =
                [
                    new OcorrenciaDeGrupoInscricao("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["RG_NUMERO_MEMBRO"] = JsonSerializer.SerializeToElement("40.333.444-5"),
                        ["PARENTESCO"] = JsonSerializer.SerializeToElement("FILHO"),
                    }),
                ],
            },
            [],
            admitidas,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        await inscricaoRepository.AtualizarAsync(inscricao, CancellationToken.None);
        await writeContext.SaveChangesAsync(CancellationToken.None);

        await using NpgsqlConnection connection = new(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT i.cpf_ciphertext, r.fato_codigo, r.valor_texto, r.valor_cifrado
            FROM selecao.inscricoes i JOIN selecao.respostas_da_inscricao r ON r.inscricao_id = i.id
            WHERE i.id = @id AND r.fato_codigo IN ('RG_NUMERO', 'RG_NUMERO_MEMBRO')
            """;
        command.Parameters.AddWithValue("id", inscricao.Id);
        int linhas = 0;
        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                linhas++;
                Encoding.UTF8.GetString((byte[])reader["cpf_ciphertext"]).Should().NotContain(PersonasDeTeste.KevinCarvalho.Valor);
                reader["valor_texto"].Should().Be(DBNull.Value, "o número de documento nunca fica na coluna de texto em claro");
                Encoding.UTF8.GetString((byte[])reader["valor_cifrado"]).Should().NotContain("30.111.222-9").And.NotContain("40.333.444-5");
            }
        }

        linhas.Should().Be(2, "o RG de nível superior e o do membro do grupo têm cada um a sua linha cifrada");

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        InscricaoRepository readRepository = new(readContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        Inscricao lida = (await readRepository.ObterPorIdAsync(inscricao.Id))!;
        lida.Respostas["RG_NUMERO"].GetString().Should().Be("30.111.222-9");
        lida.Grupos["COMPOSICAO_FAMILIAR"].Single(o => o.Id == "membro-1").Respostas["RG_NUMERO_MEMBRO"].GetString().Should().Be("40.333.444-5");
    }

    [Fact(DisplayName = "A versão fixada oferece ao campo de recurso de acessibilidade só os recursos da oferta do processo, e a submissão recusa o que ela não oferta")]
    public async Task RecursoDeAcessibilidade_ForaDaOfertaDoProcesso_ERecusado()
    {
        FatoColetado campoRecurso = FatoColetado.Criar(
            OfertaAtendimentoEspecializado.FatoRecursoAcessibilidade, 1, "De qual recurso de acessibilidade você vai precisar?",
            TipoRenderizacao.SelecaoMultipla, Obrigatoriedade.Nunca, null, classificacaoProtecao: "SENSIVEL").Value!;
        OfertaAtendimentoEspecializado oferta = OfertaAtendimentoEspecializado.Criar(
            [], [OfertaRecurso.Criar(Guid.CreateVersion7(), "SALA_AMAMENTACAO", "Sala para amamentação")], []).Value!;
        ProcessoSeletivo processo = NovoProcessoDeInscricao(
            nameof(RecursoDeAcessibilidade_ForaDaOfertaDoProcesso_ERecusado), oferta, [campoRecurso]);

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(writeContext, processo);

        Result<InscricaoDto> iniciar = await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(processoId),
            new InscricaoRepository(writeContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System),
            new CertameDivulgadoRepository(writeContext), new ProcessoSeletivoRepository(writeContext, TimeProvider.System),
            RegistroCodecs, Fuso(), DentroDoPeriodo, writeContext, UsuarioComCpf(PersonasDeTeste.FernandaDrumond), CancellationToken.None);
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        Result<ContextoDaVersaoFixada> contexto = await ContextoDaVersaoFixadaResolver.ResolverAsync(
            iniciar.Value!.VersaoFormularioId, processoId, new ProcessoSeletivoRepository(readContext, TimeProvider.System), RegistroCodecs, CancellationToken.None);
        contexto.IsSuccess.Should().BeTrue(contexto.Error?.Message);
        contexto.Value!.Ofertas[OfertaAtendimentoEspecializado.FatoRecursoAcessibilidade].Should().BeEquivalentTo(["SALA_AMAMENTACAO"]);

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.RecusasDeOpcaoForaDaOferta(
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                [OfertaAtendimentoEspecializado.FatoRecursoAcessibilidade] = JsonSerializer.SerializeToElement(new[] { "SALA_AMAMENTACAO", "INTERPRETE_LIBRAS" }),
            },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal),
            contexto.Value.Ofertas);

        recusas.Should().ContainSingle(e => e.Field == OfertaAtendimentoEspecializado.FatoRecursoAcessibilidade
            && e.Error.Code == "Inscricao.RespostaInvalida", "o intérprete de Libras não está na oferta deste processo");
    }

    [Fact(DisplayName = "O rascunho guarda a resposta ao item que outra resposta esconde, e o envio a descarta da inscrição gravada e do comprovante, que continua íntegro")]
    public async Task Envio_SobreVersaoFixadaReal_DescartaARespostaDoItemOculto()
    {
        FatoColetado precisa = FatoColetado.Criar(
            "PRECISA_ATENDIMENTO", 1, "Você precisa de atendimento especializado?", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "SENSIVEL").Value!;
        FatoColetado condicao = FatoColetado.Criar(
            "CONDICAO_DESCRITA", 2, "Descreva a sua condição", TipoRenderizacao.Texto, Obrigatoriedade.Nunca,
            [CondicaoPrecondicaoFato.Criar(0, "PRECISA_ATENDIMENTO", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!],
            classificacaoProtecao: "SENSIVEL", formato: "LIVRE").Value!;
        ProcessoSeletivo processo = NovoProcessoDeInscricao(
            nameof(Envio_SobreVersaoFixadaReal_DescartaARespostaDoItemOculto), camposAdicionais: [precisa, condicao]);

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(writeContext, processo);
        InscricaoRepository inscricaoRepository = new(writeContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        Result<InscricaoDto> iniciar = await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(processoId),
            inscricaoRepository, new CertameDivulgadoRepository(writeContext), new ProcessoSeletivoRepository(writeContext, TimeProvider.System),
            RegistroCodecs, Fuso(), DentroDoPeriodo, writeContext, UsuarioComCpf(PersonasDeTeste.FernandaDrumond), CancellationToken.None);
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);

        Inscricao inscricao = (await inscricaoRepository.ObterPorIdAsync(iniciar.Value!.Id))!;
        ContextoDaVersaoFixada contexto = (await ContextoDaVersaoFixadaResolver.ResolverAsync(
            inscricao.VersaoFormularioId, processoId, new ProcessoSeletivoRepository(writeContext, TimeProvider.System), RegistroCodecs, CancellationToken.None))
            .Value!;

        // O candidato disse "sim", descreveu a condição e voltou para "não": a descrição fica no rascunho.
        inscricao.Salvar(
            null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["PRECISA_ATENDIMENTO"] = JsonSerializer.SerializeToElement(false),
                ["CONDICAO_DESCRITA"] = JsonSerializer.SerializeToElement("Lactante"),
            },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [], contexto.Admitidas,
            PrecondicaoIfMatch.DeTags([inscricao.ETag])).IsSuccess.Should().BeTrue();
        await inscricaoRepository.AtualizarAsync(inscricao, CancellationToken.None);
        await writeContext.SaveChangesAsync(CancellationToken.None);

        await using SelecaoDbContext envioContext = _fixture.CreateDbContext();
        InscricaoRepository envioRepository = new(envioContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        Inscricao rascunho = (await envioRepository.ObterPorIdAsync(inscricao.Id))!;
        rascunho.Respostas.Should().ContainKey("CONDICAO_DESCRITA", "o rascunho guarda a resposta oculta, para o candidato poder voltar atrás");

        AvaliacaoFormulario avaliacao = contexto.Regras.Avaliar(new EntradaAvaliacaoFormulario(
            rascunho.Respostas,
            new HashSet<string>(contexto.Regras.Etapas.Select(static e => e.Codigo), StringComparer.Ordinal),
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal))).Value!;
        ComprovanteInscricao comprovante = rascunho.Submeter(
            "000901", contexto.Admitidas.OfertasDeCurso.Single(), new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [], contexto.Admitidas, avaliacao,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal), DerivadosDoSistemaInscricao.DefinicoesDasRespostas,
            PrecondicaoIfMatch.DeTags([rascunho.ETag]), DentroDoPeriodo.GetUtcNow()).Value!;
        await envioRepository.AtualizarAsync(rascunho, CancellationToken.None);
        await envioRepository.AdicionarComprovanteAsync(comprovante, CancellationToken.None);
        await envioContext.SaveChangesAsync(CancellationToken.None);

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        InscricaoRepository leitura = new(readContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        Inscricao enviada = (await leitura.ObterPorIdAsync(inscricao.Id))!;
        enviada.Respostas.Should().ContainKey("PRECISA_ATENDIMENTO").And.NotContainKey("CONDICAO_DESCRITA");
        ComprovanteInscricao gravado = (await leitura.ObterComprovantesAsync([inscricao.Id], CancellationToken.None)).Single();
        gravado.Respostas.Should().NotContainKey("CONDICAO_DESCRITA");
        gravado.IntegridadeConfere().Should().BeTrue("o hash cobre só o que foi gravado");
    }
}
