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

    private static ProcessoSeletivo NovoProcessoDeInscricao(string nome)
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

        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        processo.DefinirDistribuicaoVagas(
            [ConfiguracaoDistribuicaoVagas.Criar(
                Guid.CreateVersion7(), 40, 1m,
                ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)).Value!,
                null, null, [modalidade]).Value!],
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

        processo.DefinirItens(
            [FatoColetado.Criar(
                "RG_NUMERO", 0, "RG", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
                classificacaoProtecao: "IDENTIFICADOR", formato: "LIVRE").Value!],
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

        // O conjunto básico do candidato (sempre presente num formulário de inscrição) traz oito
        // fatos de seleção (nacionalidade, estado civil, sexo, cor/raça, tipo de localidade, UF do
        // RG, UF de naturalidade e tipo de documento do estrangeiro) — o canonicalizador exige uma
        // entrada não vazia para cada; os valores em si não importam para o que este teste prova
        // (a resolução da versão divulgada e a cifra dos documentos).
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresDoConjuntoBasico = new(StringComparer.Ordinal)
        {
            ["NACIONALIDADE"] = [new ValorDominioDeclaradoCongelado("BRASILEIRA", "Nacionalidade brasileira.", 0)],
            ["ESTADO_CIVIL"] = [new ValorDominioDeclaradoCongelado("SOLTEIRO", "Solteiro(a).", 0)],
            ["SEXO"] = [new ValorDominioDeclaradoCongelado("FEMININO", "Feminino.", 0)],
            ["COR_RACA"] = [new ValorDominioDeclaradoCongelado("NAO_DECLARADO", "Prefere não declarar.", 0)],
            ["TIPO_ENDERECO"] = [new ValorDominioDeclaradoCongelado("URBANO", "Endereço urbano.", 0)],
            ["RG_UF"] = [new ValorDominioDeclaradoCongelado("PA", "Pará.", 0)],
            ["NATURALIDADE_UF"] = [new ValorDominioDeclaradoCongelado("PA", "Pará.", 0)],
            ["DOCUMENTO_ESTRANGEIRO_TIPO"] = [new ValorDominioDeclaradoCongelado("PASSAPORTE", "Passaporte.", 0)],
        };

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

    private static IUserContext UsuarioComCpf(Cpf cpf)
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.UserId.Returns("sub-fernanda-drumond");
        contexto.Issuer.Returns("https://idp.unifesspa.edu.br/realms/unifesspa");
        return contexto;
    }

    [Fact(DisplayName = "Iniciar fixa a versão divulgada e os fatos Identificador do grafo reidratado, inclusive o de grupo, e persiste contra Postgres real")]
    public async Task Handle_ComProcessoPublicadoEDivulgadoReal_FixaVersaoEFatosIdentificadorDeclarados()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricao(
            nameof(Handle_ComProcessoPublicadoEDivulgadoReal_FixaVersaoEFatosIdentificadorDeclarados));

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(writeContext, processo);

        InscricaoRepository inscricaoRepository = new(writeContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        ProcessoSeletivoRepository processoRepository = new(writeContext, TimeProvider.System);
        CertameDivulgadoRepository divulgadoRepository = new(writeContext);

        Result<InscricaoDto> iniciar = await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(processoId),
            inscricaoRepository, divulgadoRepository, processoRepository, RegistroCodecs, writeContext,
            UsuarioComCpf(PersonasDeTeste.FernandaDrumond), CancellationToken.None);

        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);
        iniciar.Value!.Respostas.Should().BeEmpty("a inscrição nasce em rascunho, sem resposta nenhuma");

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        InscricaoRepository readRepository = new(readContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        Inscricao? gravada = await readRepository.ObterPorIdAsync(iniciar.Value!.Id);

        gravada.Should().NotBeNull();
        gravada!.FatosIdentificadorDeclarados.Should().BeEquivalentTo(
            ["CPF", "RG_NUMERO", "DOCUMENTO_ESTRANGEIRO_NUMERO", "RG_NUMERO_MEMBRO"],
            "o conjunto junta os fatos Identificador do conjunto básico do candidato (CPF, RG, documento do estrangeiro) " +
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
            RegistroCodecs, writeContext, UsuarioComCpf(PersonasDeTeste.KevinCarvalho), CancellationToken.None);
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);

        Inscricao? inscricao = await inscricaoRepository.ObterPorIdAsync(iniciar.Value!.Id);
        inscricao.Should().NotBeNull();

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
            PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        await inscricaoRepository.AtualizarAsync(inscricao, CancellationToken.None);
        await writeContext.SaveChangesAsync(CancellationToken.None);

        await using NpgsqlConnection connection = new(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT cpf_ciphertext, documentos_cifrados, respostas, grupos FROM selecao.inscricoes WHERE id = @id";
        command.Parameters.AddWithValue("id", inscricao.Id);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();

        byte[] cpfCiphertext = (byte[])reader["cpf_ciphertext"];
        byte[] documentosCifrados = (byte[])reader["documentos_cifrados"];
        string respostas = (string)reader["respostas"];
        string grupos = (string)reader["grupos"];

        Encoding.UTF8.GetString(cpfCiphertext).Should().NotContain(PersonasDeTeste.KevinCarvalho.Valor);
        Encoding.UTF8.GetString(documentosCifrados).Should().NotContain("30.111.222-9").And.NotContain("40.333.444-5");
        respostas.Should().NotContain("30.111.222-9", "o número de documento nunca entra no jsonb em claro, mesmo quando a versão vem de uma publicação real");
        grupos.Should().NotContain("40.333.444-5", "o número de documento do membro do grupo é removido antes de serializar a coluna grupos — é só o que documentos_cifrados guarda");

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        InscricaoRepository readRepository = new(readContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        Inscricao lida = (await readRepository.ObterPorIdAsync(inscricao.Id))!;
        lida.Respostas["RG_NUMERO"].GetString().Should().Be("30.111.222-9");
        lida.Grupos["COMPOSICAO_FAMILIAR"].Single(o => o.Id == "membro-1").Respostas["RG_NUMERO_MEMBRO"].GetString().Should().Be("40.333.444-5");
    }
}
