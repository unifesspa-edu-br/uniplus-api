namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore.Migrations.Operations;

using Npgsql;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations;
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
        string nome,
        OfertaAtendimentoEspecializado? oferta = null,
        IReadOnlyList<FatoColetado>? camposAdicionais = null,
        IReadOnlyList<ModalidadeSelecionada>? modalidades = null)
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
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        processo.DefinirDistribuicaoVagas(
            [ConfiguracaoDistribuicaoVagas.Criar(
                Guid.CreateVersion7(), 40, 1m,
                ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)).Value!,
                null, null, modalidades ?? [modalidade], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!],
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
    private static async Task<(Guid ProcessoId, DadosEdital Dados)> PublicarEDivulgarAsync(
        SelecaoDbContext db,
        ProcessoSeletivo processo,
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>? valoresDosCampos = null)
    {
        await db.ProcessosSeletivos.AddAsync(processo);
        await db.SaveChangesAsync();

        const string hashDocumento = "3333333333333333333333333333333333333333333333333333333333333333333";
        string hash64 = hashDocumento[..64];
        DadosEdital dados = DadosEdital.Criar(
            "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;

        // O conjunto básico do candidato (sempre presente num formulário de inscrição) traz oito
        // fatos de seleção (nacionalidade, órgão emissor e UF do RG, estado civil, sexo, cor/raça,
        // tipo de localidade e UF de naturalidade) — o canonicalizador exige uma entrada não vazia para cada; os
        // valores em si não importam para o que este teste prova (a resolução da versão divulgada e
        // a cifra dos documentos).
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresDoConjuntoBasico = new(StringComparer.Ordinal)
        {
            ["NACIONALIDADE"] = [new ValorDominioDeclaradoCongelado("BRASILEIRA", "Nacionalidade brasileira.", 0)],
            ["ESTADO_CIVIL"] = [new ValorDominioDeclaradoCongelado("SOLTEIRO", "Solteiro(a).", 0)],
            ["SEXO"] = [new ValorDominioDeclaradoCongelado("FEMININO", "Feminino.", 0)],
            ["COR_RACA"] = [new ValorDominioDeclaradoCongelado("NAO_DECLARADO", "Prefere não declarar.", 0)],
            ["TIPO_ENDERECO"] = [new ValorDominioDeclaradoCongelado("URBANO", "Endereço urbano.", 0)],
            ["RG_ORGAO_EMISSOR"] = [new ValorDominioDeclaradoCongelado("SSP", "Secretaria de Segurança Pública (SSP)", 0)],
            ["RG_UF"] = [new ValorDominioDeclaradoCongelado("PA", "Pará.", 0)],
            ["NATURALIDADE_UF"] = [new ValorDominioDeclaradoCongelado("PA", "Pará.", 0)],
        };

        foreach ((string campo, IReadOnlyList<ValorDominioDeclaradoCongelado>? valores) in valoresDosCampos ?? new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>())
        {
            valoresDoConjuntoBasico[campo] = valores;
        }

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
            inscricaoRepository, divulgadoRepository, processoRepository, RegistroCodecs, Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), DentroDoPeriodo, writeContext,
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

    [Fact(DisplayName = "A migração regrava o formulário gravado antes da faixa: as datas do conjunto básico passam a declará-la")]
    public async Task Migracao_FormularioGravadoAntes_RecebeAFaixaNasDatasDoConjuntoBasico()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricao(nameof(Migracao_FormularioGravadoAntes_RecebeAFaixaNasDatasDoConjuntoBasico));
        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            await writeContext.ProcessosSeletivos.AddAsync(processo);
            await writeContext.SaveChangesAsync();
        }

        await using NpgsqlConnection connection = new(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using (NpgsqlCommand antes = connection.CreateCommand())
        {
            antes.CommandText = """
                UPDATE selecao.fatos_coletados SET restricoes = '[]'::jsonb
                WHERE processo_seletivo_id = @id AND fato_codigo IN ('DATA_NASCIMENTO', 'RG_DATA_EMISSAO')
                """;
            antes.Parameters.AddWithValue("id", processo.Id);
            (await antes.ExecuteNonQueryAsync()).Should().Be(2, "o formulário gravado antes da faixa tem as duas datas sem restrição");
        }

        foreach (SqlOperation operacao in new LimitaAsDatasDoConjuntoBasico().UpOperations.OfType<SqlOperation>())
        {
            await using NpgsqlCommand migracao = connection.CreateCommand();
            migracao.CommandText = operacao.Sql;
            await migracao.ExecuteNonQueryAsync();
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo lido = (await new ProcessoSeletivoRepository(readContext, TimeProvider.System).ObterComConfiguracaoAsync(processo.Id))!;
        foreach (string fato in new[] { "DATA_NASCIMENTO", "RG_DATA_EMISSAO" })
        {
            FaixaDeData faixa = lido.FatosColetados.Single(f => f.FatoCodigo == fato).Restricoes.OfType<FaixaDeData>().Single();
            faixa.Minima.Should().Be(LimiteDeData.NoEnvio(anos: -120, dias: 0));
            faixa.Maxima.Should().Be(LimiteDeData.NoEnvio(anos: 0, dias: 0));
        }
    }

    [Fact(DisplayName = "A migração regrava o formulário gravado com o órgão emissor em texto: a lista e o nome do outro órgão entram, e o que vinha depois desce")]
    public async Task Migracao_FormularioComOrgaoEmissorEmTexto_RecebeAListaEONomeDoOutro()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricao(nameof(Migracao_FormularioComOrgaoEmissorEmTexto_RecebeAListaEONomeDoOutro));
        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            await writeContext.ProcessosSeletivos.AddAsync(processo);
            await writeContext.SaveChangesAsync();
        }

        await using NpgsqlConnection connection = new(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using (NpgsqlCommand antes = connection.CreateCommand())
        {
            // O formulário como era gravado antes da lista: o órgão emissor em texto livre, sem o nome
            // do outro órgão, e o que vem depois dele uma posição acima.
            antes.CommandText = """
                CREATE TEMPORARY TABLE rg_antes AS
                SELECT ordem FROM selecao.fatos_coletados WHERE processo_seletivo_id = @id AND fato_codigo = 'RG_ORGAO_EMISSOR';
                DELETE FROM selecao.fatos_coletados WHERE processo_seletivo_id = @id AND fato_codigo = 'RG_ORGAO_EMISSOR_OUTRO';
                UPDATE selecao.fatos_coletados SET tipo_renderizacao = 5, formato = 'LIVRE'
                WHERE processo_seletivo_id = @id AND fato_codigo = 'RG_ORGAO_EMISSOR';
                UPDATE selecao.fatos_coletados SET ordem = ordem + 1000000
                WHERE processo_seletivo_id = @id AND grupo_coletado_id IS NULL AND ordem > (SELECT ordem FROM rg_antes);
                UPDATE selecao.fatos_coletados SET ordem = ordem - 1000001
                WHERE processo_seletivo_id = @id AND grupo_coletado_id IS NULL AND ordem > 1000000;
                UPDATE selecao.grupos_coletados SET ordem = ordem - 1
                WHERE processo_seletivo_id = @id AND ordem > (SELECT ordem FROM rg_antes);
                DROP TABLE rg_antes;
                """;
            antes.Parameters.AddWithValue("id", processo.Id);
            await antes.ExecuteNonQueryAsync();
        }

        foreach (SqlOperation operacao in new EscolheOOrgaoEmissorDoRgDeUmaLista().UpOperations.OfType<SqlOperation>())
        {
            await using NpgsqlCommand migracao = connection.CreateCommand();
            migracao.CommandText = operacao.Sql;
            await migracao.ExecuteNonQueryAsync();
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo lido = (await new ProcessoSeletivoRepository(readContext, TimeProvider.System).ObterComConfiguracaoAsync(processo.Id))!;
        Forma(lido).Should().BeEquivalentTo(Forma(processo), static o => o.WithStrictOrdering());
        lido.GruposColetados.Select(static g => (g.Codigo, g.Ordem)).Should().BeEquivalentTo(processo.GruposColetados.Select(static g => (g.Codigo, g.Ordem)));

        static IEnumerable<object> Forma(ProcessoSeletivo p) => p.FatosColetados
            .Where(static f => f.GrupoColetadoId is null)
            .OrderBy(static f => f.Ordem)
            .Select(static f => new
            {
                f.FatoCodigo,
                f.Ordem,
                f.EtapaCodigo,
                f.Rotulo,
                f.TipoRenderizacao,
                f.Formato,
                f.ClassificacaoProtecao,
                f.OrigemValores,
                Obrigatoriedade = f.Obrigatoriedade.Tipo,
                f.Restricoes,
                Precondicoes = f.Precondicoes.Select(static c => (c.Clausula, c.Fato, c.Operador, JsonSerializer.Serialize(c.Valor))).ToList(),
            });
    }

    [Fact(DisplayName = "O conjunto básico publicado recusa o nascimento depois do envio com os limites do dia, sem gravar o rascunho")]
    public async Task Salvar_SobreVersaoFixadaReal_RecusaONascimentoNoFuturo()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricao(nameof(Salvar_SobreVersaoFixadaReal_RecusaONascimentoNoFuturo));

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(writeContext, processo);

        InscricaoRepository inscricaoRepository = new(writeContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        ProcessoSeletivoRepository processoRepository = new(writeContext, TimeProvider.System);
        CertameDivulgadoRepository divulgadoRepository = new(writeContext);
        IUserContext usuario = UsuarioComCpf(PersonasDeTeste.FernandaDrumond);
        Result<InscricaoDto> iniciar = await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(processoId),
            inscricaoRepository, divulgadoRepository, processoRepository, RegistroCodecs, Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(),
            DentroDoPeriodo, writeContext, usuario, CancellationToken.None);
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);

        InscricaoRequest request = new(
            null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["DATA_NASCIMENTO"] = JsonSerializer.SerializeToElement("2027-01-10") },
            null,
            null);
        Result<InscricaoDto> salvar = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(processoId, request, PrecondicaoIfMatch.DeTags([iniciar.Value!.ETag])),
            inscricaoRepository, processoRepository, divulgadoRepository, RegistroCodecs, Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(),
            writeContext, usuario, DentroDoPeriodo, CancellationToken.None);

        salvar.Errors.Should().ContainSingle(static e => e.Field == "DATA_NASCIMENTO" && e.Error.Code == InscricaoErrorCodes.DataForaDaFaixa)
            .Which.Error.Message.Should().Be("Informe uma data entre 15/01/1906 e 15/01/2026.");

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        Inscricao? gravada = await new InscricaoRepository(readContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System)
            .ObterPorIdAsync(iniciar.Value!.Id);
        gravada!.Respostas.Should().NotContainKey("DATA_NASCIMENTO", "a resposta recusada não é gravada no rascunho");
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
            RegistroCodecs, Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), DentroDoPeriodo, writeContext, UsuarioComCpf(PersonasDeTeste.KevinCarvalho), CancellationToken.None);
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
            RegistroCodecs, Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), DentroDoPeriodo, writeContext, UsuarioComCpf(PersonasDeTeste.FernandaDrumond), CancellationToken.None);
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

    [Fact(DisplayName = "A versão fixada oferece ao órgão emissor do RG a lista congelada, e a submissão recusa o órgão fora dela")]
    public async Task OrgaoEmissor_ForaDaListaCongelada_ERecusado()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricao(nameof(OrgaoEmissor_ForaDaListaCongelada_ERecusado));

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(writeContext, processo);

        Result<InscricaoDto> iniciar = await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(processoId),
            new InscricaoRepository(writeContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System),
            new CertameDivulgadoRepository(writeContext), new ProcessoSeletivoRepository(writeContext, TimeProvider.System),
            RegistroCodecs, Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), DentroDoPeriodo, writeContext, UsuarioComCpf(PersonasDeTeste.FernandaDrumond), CancellationToken.None);
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        Result<ContextoDaVersaoFixada> contexto = await ContextoDaVersaoFixadaResolver.ResolverAsync(
            iniciar.Value!.VersaoFormularioId, processoId, new ProcessoSeletivoRepository(readContext, TimeProvider.System), RegistroCodecs, CancellationToken.None);
        contexto.IsSuccess.Should().BeTrue(contexto.Error?.Message);

        IReadOnlyList<FieldError> RecusasDe(string orgao) => VeredictoDeSubmissao.RecusasDeOpcaoForaDaOferta(
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["RG_ORGAO_EMISSOR"] = JsonSerializer.SerializeToElement(orgao) },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal),
            contexto.Value!.Ofertas);

        RecusasDe("SSP").Should().BeEmpty();
        RecusasDe("PA").Should().ContainSingle(static e => e.Field == "RG_ORGAO_EMISSOR" && e.Error.Code == InscricaoErrorCodes.RespostaInvalida,
            "a UF não é órgão emissor");
    }

    [Fact(DisplayName = "Sobre a versão fixada, a inscrição grava o telefone do conjunto básico só com os dígitos")]
    public async Task Telefone_SobreVersaoFixada_GravaSoOsDigitos()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricao(nameof(Telefone_SobreVersaoFixada_GravaSoOsDigitos));

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(writeContext, processo);

        Result<InscricaoDto> iniciar = await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(processoId),
            new InscricaoRepository(writeContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System),
            new CertameDivulgadoRepository(writeContext), new ProcessoSeletivoRepository(writeContext, TimeProvider.System),
            RegistroCodecs, Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), DentroDoPeriodo, writeContext, UsuarioComCpf(PersonasDeTeste.FernandaDrumond), CancellationToken.None);
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        Result<ContextoDaVersaoFixada> contexto = await ContextoDaVersaoFixadaResolver.ResolverAsync(
            iniciar.Value!.VersaoFormularioId, processoId, new ProcessoSeletivoRepository(readContext, TimeProvider.System), RegistroCodecs, CancellationToken.None);
        contexto.IsSuccess.Should().BeTrue(contexto.Error?.Message);

        InscricaoRepository repositorio = new(readContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        Inscricao inscricao = (await repositorio.ObterPorIdAsync(iniciar.Value!.Id))!;
        inscricao.Salvar(
            null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["TELEFONE"] = JsonSerializer.SerializeToElement("(94) 3322-1234") },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal),
            [], contexto.Value!.Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag])).IsSuccess.Should().BeTrue();

        inscricao.Respostas["TELEFONE"].GetString().Should().Be("9433221234",
            "o formato do telefone congelado na versão chega à definição da resposta, que grava a forma canônica");
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
            RegistroCodecs, Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), DentroDoPeriodo, writeContext, UsuarioComCpf(PersonasDeTeste.FernandaDrumond), CancellationToken.None);
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

        ResultadoDaAvaliacaoDaInscricao avaliacao = AvaliacaoDaInscricao.Avaliar(contexto.Configuracao, new EntradaDaAvaliacaoDaInscricao(
            rascunho.Respostas,
            new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal),
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal),
            AvaliacaoDaInscricao.CodigosDasEtapasDaInscricao(contexto.Configuracao.Definicao),
            Envio: null)).Value!;
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

    /// <summary>
    /// Um processo publicado em que a modalidade deriva de uma resposta: a ampla concorrência só a quem
    /// declara a deficiência, para as três situações caberem numa oferta só.
    /// </summary>
    private static ProcessoSeletivo ProcessoQueDerivaAModalidade(string nome)
    {
        FatoColetado concorrerPcd = FatoColetado.Criar(
            "CONCORRER_PCD", 1, "Você quer concorrer às vagas para pessoa com deficiência?", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "SENSIVEL", etapaCodigo: FormularioDeTeste.Secao, finalidade: FinalidadeFormulario.Inscricao).Value!;
        ModalidadeSelecionada ampla = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, Clausulas([("CONCORRER_PCD", true)]), null, "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        return NovoProcessoDeInscricao(nome, camposAdicionais: [concorrerPcd], modalidades: [ampla]);
    }

    private async Task<Result<AvaliacaoDaInscricaoDto>> AvaliarAsync(
        SelecaoDbContext db, Guid processoId, IUserContext usuario, Dictionary<string, JsonElement>? respostas) =>
        await AvaliarInscricaoQueryHandler.Handle(
            new AvaliarInscricaoQuery(processoId, new AvaliacaoDaInscricaoRequest(respostas, null)),
            new InscricaoRepository(db, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System),
            new ProcessoSeletivoRepository(db, TimeProvider.System), new CertameDivulgadoRepository(db),
            RegistroCodecs, Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), usuario, DentroDoPeriodo, CancellationToken.None);

    [Fact(DisplayName = "A consulta da inscrição sobre o processo publicado devolve as modalidades das respostas, o que falta e o nome congelado, sem gravar nada")]
    public async Task Consulta_SobreVersaoFixadaReal_DevolveAsModalidadesSemGravar()
    {
        await using SelecaoDbContext db = _fixture.CreateDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, ProcessoQueDerivaAModalidade(nameof(Consulta_SobreVersaoFixadaReal_DevolveAsModalidadesSemGravar)));
        IUserContext fernanda = UsuarioComCpf(PersonasDeTeste.FernandaDrumond);
        Result<InscricaoDto> iniciar = await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(processoId),
            new InscricaoRepository(db, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System), new CertameDivulgadoRepository(db),
            new ProcessoSeletivoRepository(db, TimeProvider.System), RegistroCodecs, Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(),
            DentroDoPeriodo, db, fernanda, CancellationToken.None);
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);
        iniciar.Value!.Avaliacao.Modalidades.Should().BeEquivalentTo(new ModalidadesDaInscricaoDto(
            true, "FALTA_RESPONDER", [],
            [new PendenciaDaAvaliacaoDto("CONCORRER_PCD", "Você quer concorrer às vagas para pessoa com deficiência?", FormularioDeTeste.Secao)]));

        Result<AvaliacaoDaInscricaoDto> concorre = await AvaliarAsync(db, processoId, fernanda, new(StringComparer.Ordinal)
        {
            ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(true),
        });
        Result<AvaliacaoDaInscricaoDto> naoConcorre = await AvaliarAsync(db, processoId, fernanda, new(StringComparer.Ordinal)
        {
            ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(false),
        });

        concorre.IsSuccess.Should().BeTrue(concorre.Error?.Message);
        concorre.Value!.Modalidades.Situacao.Should().Be("CONCORRE");
        concorre.Value!.Modalidades.Itens.Should().Equal(new ModalidadeDaInscricaoDto("AC", "AC", 0));
        naoConcorre.Value!.Modalidades.Situacao.Should().Be("NAO_CONCORRE");
        naoConcorre.Value!.Modalidades.Itens.Should().BeEmpty();

        await using SelecaoDbContext leitura = _fixture.CreateDbContext();
        Inscricao gravada = (await new InscricaoRepository(leitura, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System)
            .ObterPorIdAsync(iniciar.Value!.Id))!;
        gravada.Respostas.Should().NotContainKey("CONCORRER_PCD", "a consulta não grava nada");
        gravada.ETag.Should().Be(iniciar.Value!.ETag, "nenhuma consulta muda a revisão da inscrição");
    }

    [Fact(DisplayName = "Sobre o processo que só oferta a ampla, incondicional, a consulta diz que o candidato concorre a ela")]
    public async Task Consulta_SoAmpla_ConcorreAAmpla()
    {
        await using SelecaoDbContext db = _fixture.CreateDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, NovoProcessoDeInscricao(nameof(Consulta_SoAmpla_ConcorreAAmpla)));
        IUserContext fernanda = UsuarioComCpf(PersonasDeTeste.FernandaDrumond);
        (await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(processoId),
            new InscricaoRepository(db, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System), new CertameDivulgadoRepository(db),
            new ProcessoSeletivoRepository(db, TimeProvider.System), RegistroCodecs, Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(),
            DentroDoPeriodo, db, fernanda, CancellationToken.None)).IsSuccess.Should().BeTrue();

        Result<AvaliacaoDaInscricaoDto> avaliacao = await AvaliarAsync(db, processoId, fernanda, null);

        avaliacao.Value!.Modalidades.Derivada.Should().BeTrue("a modalidade sai das modalidades ofertadas");
        avaliacao.Value.Modalidades.Situacao.Should().Be("CONCORRE");
        avaliacao.Value.Modalidades.Itens.Select(static m => m.Codigo).Should().Equal(["AC"]);
    }

    [Fact(DisplayName = "A consulta só responde ao titular: sem inscrição no processo é não encontrada, e o sub divergente é dono divergente")]
    public async Task Consulta_SoAoTitular()
    {
        await using SelecaoDbContext db = _fixture.CreateDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, ProcessoQueDerivaAModalidade(nameof(Consulta_SoAoTitular)));
        (await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(processoId),
            new InscricaoRepository(db, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System), new CertameDivulgadoRepository(db),
            new ProcessoSeletivoRepository(db, TimeProvider.System), RegistroCodecs, Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(),
            DentroDoPeriodo, db, UsuarioComCpf(PersonasDeTeste.FernandaDrumond), CancellationToken.None)).IsSuccess.Should().BeTrue();

        IUserContext mesmoCpfOutroSub = UsuarioComCpf(PersonasDeTeste.FernandaDrumond);
        mesmoCpfOutroSub.UserId.Returns("sub-de-outra-conta");

        (await AvaliarAsync(db, processoId, UsuarioComCpf(PersonasDeTeste.KevinCarvalho), null)).Error!.Code
            .Should().Be(InscricaoErrorCodes.NaoEncontrada);
        (await AvaliarAsync(db, processoId, mesmoCpfOutroSub, null)).Error!.Code
            .Should().Be(InscricaoErrorCodes.DonoDivergente);
    }

    [Fact(DisplayName = "O formulário da inscrição publicado diz, só pelo código, de que campos a avaliação depende, sem regra nem valor")]
    public async Task FormularioDaInscricao_TrazAsDependenciasDaAvaliacaoSoPeloCodigo()
    {
        await using SelecaoDbContext db = _fixture.CreateDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, ProcessoQueDerivaAModalidade(nameof(FormularioDaInscricao_TrazAsDependenciasDaAvaliacaoSoPeloCodigo)));
        IUserContext fernanda = UsuarioComCpf(PersonasDeTeste.FernandaDrumond);
        (await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(processoId),
            new InscricaoRepository(db, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System), new CertameDivulgadoRepository(db),
            new ProcessoSeletivoRepository(db, TimeProvider.System), RegistroCodecs, Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(),
            DentroDoPeriodo, db, fernanda, CancellationToken.None)).IsSuccess.Should().BeTrue();

        Result<FormularioRenderizavelDto> formulario = await ObterFormularioDaInscricaoQueryHandler.Handle(
            new ObterFormularioDaInscricaoQuery(processoId),
            new InscricaoRepository(db, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System),
            new ProcessoSeletivoRepository(db, TimeProvider.System), RegistroCodecs, Substitute.For<IEnderecoNoAcervoPublico>(), fernanda,
            CancellationToken.None);

        formulario.IsSuccess.Should().BeTrue(formulario.Error?.Message);
        formulario.Value!.DependenciasDaAvaliacao.Should().BeEquivalentTo(new DependenciasDaAvaliacaoDto(["CONCORRER_PCD"], []));
        JsonSerializer.Serialize(formulario.Value!.DependenciasDaAvaliacao).Should().NotContain("AC", "a matriz e o que ela contribui não saem");
        formulario.Value!.Regras.Derivacoes.Should().NotContain(d => d.FatoCodigo == AvaliacaoDaInscricao.FatoDaModalidade,
            "a derivação da modalidade não vai ao navegador");
    }

    [Fact(DisplayName = "Sobre o processo publicado, a consulta diz por fase os documentos do candidato: o geral, o do gatilho, um por membro e o que depende da convocação")]
    public async Task Consulta_DocumentosDaInscricao_PorFase()
    {
        const string declaraRenda = "DECLARA_RENDA";
        const string familia = "COMPOSICAO_FAMILIAR";
        ProcessoSeletivo processo = NovoProcessoDeInscricao(
            nameof(Consulta_DocumentosDaInscricao_PorFase),
            camposAdicionais:
            [
                FatoColetado.Criar(
                    declaraRenda, 1, "Você declara renda?", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null,
                    classificacaoProtecao: "PESSOAL", etapaCodigo: FormularioDeTeste.Secao, finalidade: FinalidadeFormulario.Inscricao).Value!,
            ]);
        Guid fase = processo.CronogramaFases.Single().Id;
        DocumentoExigido identidade = Exigencia(fase, "RG", []);
        DocumentoExigido renda = Exigencia(fase, "COMPROVANTE_RENDA", [(declaraRenda, JsonSerializer.SerializeToElement(true))]);
        DocumentoExigido doMembro = Exigencia(fase, "RG_DO_MEMBRO", []);
        DocumentoExigido daConvocacao = Exigencia(fase, "CERTIDAO", [("MODALIDADE_CONVOCACAO", JsonSerializer.SerializeToElement("AC"))]);
        processo.DefinirDocumentosExigidos(
            [
                NoExigencia.CriarFolha(identidade, 0).Value!,
                NoExigencia.CriarFolha(renda, 1).Value!,
                NoExigencia.CriarFolha(doMembro, 2, repetePorEntidade: familia).Value!,
                NoExigencia.CriarFolha(daConvocacao, 3).Value!,
            ],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        await using SelecaoDbContext db = _fixture.CreateDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo);
        IUserContext fernanda = UsuarioComCpf(PersonasDeTeste.FernandaDrumond);
        (await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(processoId),
            new InscricaoRepository(db, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System), new CertameDivulgadoRepository(db),
            new ProcessoSeletivoRepository(db, TimeProvider.System), RegistroCodecs, Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(),
            DentroDoPeriodo, db, fernanda, CancellationToken.None)).IsSuccess.Should().BeTrue();

        DocumentosDaInscricaoDto pendente = (await AvaliarDocumentosAsync(db, processoId, fernanda, null, [], fase)).Value!.Documentos;
        DocumentosDaInscricaoDto enquadra = (await AvaliarDocumentosAsync(db, processoId, fernanda, true, ["m1", "m2"], fase)).Value!.Documentos;
        DocumentosDaInscricaoDto naoEnquadra = (await AvaliarDocumentosAsync(db, processoId, fernanda, false, [], fase)).Value!.Documentos;
        Result<AvaliacaoDaInscricaoDto> faseDesconhecida = await AvaliarDocumentosAsync(db, processoId, fernanda, null, [], Guid.CreateVersion7());

        Situacoes(pendente).Should().Equal(
            [("RG", null, "EXIGIDO"), ("COMPROVANTE_RENDA", null, "PODE_SER_EXIGIDO"), ("CERTIDAO", null, "DEPENDE_DE_FASE_POSTERIOR")],
            "sem a resposta o comprovante pode ser exigido; sem membro, nada por membro; a convocação só existe depois da inscrição");
        pendente.Fases.Single().Pendencias.Select(static p => p.FatoCodigo).Should().Equal(declaraRenda);
        Situacoes(enquadra).Should().Equal(
            [("RG", null, "EXIGIDO"), ("COMPROVANTE_RENDA", null, "EXIGIDO"), ("RG_DO_MEMBRO", "m1", "EXIGIDO"), ("RG_DO_MEMBRO", "m2", "EXIGIDO"),
             ("CERTIDAO", null, "DEPENDE_DE_FASE_POSTERIOR")]);
        enquadra.Fases.Single().Documentos.Where(static d => d.TipoDocumentoCodigo == "RG_DO_MEMBRO")
            .Should().OnlyContain(d => d.GrupoCodigo == familia);
        Situacoes(naoEnquadra).Select(static d => d.Tipo).Should().NotContain("COMPROVANTE_RENDA", "quem não declara renda não apresenta o comprovante");
        faseDesconhecida.Error!.Code.Should().Be(InscricaoErrorCodes.FaseNaoEncontrada);

        Result<FormularioRenderizavelDto> formulario = await ObterFormularioDaInscricaoQueryHandler.Handle(
            new ObterFormularioDaInscricaoQuery(processoId),
            new InscricaoRepository(db, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System),
            new ProcessoSeletivoRepository(db, TimeProvider.System), RegistroCodecs, Substitute.For<IEnderecoNoAcervoPublico>(), fernanda,
            CancellationToken.None);
        formulario.Value!.DependenciasDaAvaliacao!.Fatos.Should().Contain(declaraRenda, "a resposta que decide o gatilho muda a avaliação");
    }

    private static IEnumerable<(string Tipo, string? Ocorrencia, string Situacao)> Situacoes(DocumentosDaInscricaoDto documentos) =>
        documentos.Fases.Single().Documentos.Select(static d => (d.TipoDocumentoCodigo, d.OcorrenciaId, d.Situacao));

    private async Task<Result<AvaliacaoDaInscricaoDto>> AvaliarDocumentosAsync(
        SelecaoDbContext db, Guid processoId, IUserContext usuario, bool? declaraRenda, string[] membros, Guid faseId)
    {
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal);
        if (declaraRenda is { } valor)
        {
            respostas["DECLARA_RENDA"] = JsonSerializer.SerializeToElement(valor);
        }

        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>> grupos = new(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] = [.. membros.Select(static m => new OcorrenciaDeGrupoInscricaoInput(m, new Dictionary<string, JsonElement>(StringComparer.Ordinal)))],
        };
        return await AvaliarInscricaoQueryHandler.Handle(
            new AvaliarInscricaoQuery(processoId, new AvaliacaoDaInscricaoRequest(respostas, grupos), faseId),
            new InscricaoRepository(db, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System),
            new ProcessoSeletivoRepository(db, TimeProvider.System), new CertameDivulgadoRepository(db),
            RegistroCodecs, Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), usuario, DentroDoPeriodo, CancellationToken.None);
    }

    private static DocumentoExigido Exigencia(Guid fase, string tipo, (string Fato, JsonElement Valor)[] gatilho) =>
        DocumentoExigido.Criar(
            fase, Guid.CreateVersion7(), tipo, $"Documento {tipo}", "PESSOAL",
            gatilho.Length == 0 ? Aplicabilidade.Geral : Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [.. gatilho.Select(static g => CondicaoGatilho.Criar(0, g.Fato, Operador.Igual, g.Valor).Value!)],
            basesLegais: [DocumentoExigidoBaseLegal.Criar("Res. Unifesspa 532/2021, art. 12", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, null).Value!],
            idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null,
            finalidade: FinalidadeFormulario.Inscricao).Value!;

    /// <summary>
    /// Um processo publicado com as dez modalidades da Lei 12.711 ofertadas, cada uma com a regra de
    /// aplicabilidade da matriz da lei (UNI-REQ-0076), as opções de concorrência e a origem escolar
    /// perguntadas, e o egresso de escola pública derivado da origem escolar.
    /// </summary>
    private static ProcessoSeletivo ProcessoComAMatrizNormativa(string nome)
    {
        FatoColetado Booleano(string codigo, int ordem) => FatoColetado.Criar(
            codigo, ordem, codigo, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "SENSIVEL",
            etapaCodigo: FormularioDeTeste.Secao, finalidade: FinalidadeFormulario.Inscricao).Value!;
        FatoColetado Selecao(string codigo, int ordem) => FatoColetado.Criar(
            codigo, ordem, $"Rótulo de {codigo}", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL",
            etapaCodigo: FormularioDeTeste.Secao, finalidade: FinalidadeFormulario.Inscricao).Value!;

        ProcessoSeletivo processo = NovoProcessoDeInscricao(
            nome,
            camposAdicionais:
            [
                Selecao(OrigemEscolar.FatoFormaDeConclusao, 1), Selecao(OrigemEscolar.FatoOndeCursou, 2),
                Booleano("CONCORRER_PCD", 3), Booleano("CONCORRER_EP", 4), Booleano("CONCORRER_PPI", 5),
                Booleano("CONCORRER_Q", 6), Booleano("CONCORRER_RENDA", 7),
            ],
            modalidades: [.. RegrasDaLei.Select(static r => ModalidadeDaLei(r.Key, r.Value))]);
        processo.DefinirRegrasDerivacao(
        [
            Configurada(RegrasDerivacaoFato.CriarBooleana(
                OrigemEscolar.FatoEgresso, OrigemEscolar.RegrasDoEgresso, OrigemEscolar.DependenciasDoEgresso).Value!),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return processo;
    }

    /// <summary>As opções da origem escolar que a publicação congela para as duas perguntas.</summary>
    private static Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> OpcoesDaOrigemEscolar() => new(StringComparer.Ordinal)
    {
        [OrigemEscolar.FatoFormaDeConclusao] = [.. new[] { OrigemEscolar.Regular, OrigemEscolar.Eja, OrigemEscolar.Encceja }
            .Select(static (c, i) => new ValorDominioDeclaradoCongelado(c, c, i))],
        [OrigemEscolar.FatoOndeCursou] = [.. OrigemEscolar.OndeCursouSemCertificacao.Append(OrigemEscolar.ConcluiuPorCertificacao)
            .Select(static (c, i) => new ValorDominioDeclaradoCongelado(c, c, i))],
    };

    /// <summary>
    /// As regras da matriz da lei sobre a opção de cada dimensão: optar pelas cotas é ser egresso de
    /// escola pública e optar pelas vagas de escola pública, a cota de renda acrescenta a renda, e a
    /// reserva de pessoa com deficiência vale para quem não optou pelas cotas.
    /// </summary>
    private static readonly Dictionary<string, PredicadoDnf> RegrasDaLei = new(StringComparer.Ordinal)
    {
        ["AC"] = PredicadoDnf.SemClausula,
        ["AC_PCD"] = Clausulas(
            [(OpcaoPelasCotasDaLei.Fato, false), ("CONCORRER_PCD", true), (OrigemEscolar.FatoEgresso, false)],
            [(OpcaoPelasCotasDaLei.Fato, false), ("CONCORRER_PCD", true), ("CONCORRER_EP", false)]),
        ["LI_EP"] = Clausulas([.. OptouPelasCotas]),
        ["LB_EP"] = Clausulas([.. OptouPelasCotas, ("CONCORRER_RENDA", true)]),
        ["LI_PPI"] = Clausulas([.. OptouPelasCotas, ("CONCORRER_PPI", true)]),
        ["LB_PPI"] = Clausulas([.. OptouPelasCotas, ("CONCORRER_PPI", true), ("CONCORRER_RENDA", true)]),
        ["LI_Q"] = Clausulas([.. OptouPelasCotas, ("CONCORRER_Q", true)]),
        ["LB_Q"] = Clausulas([.. OptouPelasCotas, ("CONCORRER_Q", true), ("CONCORRER_RENDA", true)]),
        ["LI_PCD"] = Clausulas([.. OptouPelasCotas, ("CONCORRER_PCD", true)]),
        ["LB_PCD"] = Clausulas([.. OptouPelasCotas, ("CONCORRER_PCD", true), ("CONCORRER_RENDA", true)]),
    };

    private static (string, bool)[] OptouPelasCotas => [(OrigemEscolar.FatoEgresso, true), ("CONCORRER_EP", true)];

    private static PredicadoDnf Clausulas(params (string Fato, bool Valor)[][] clausulas) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([.. clausulas.SelectMany(static (atomos, indice) => atomos.Select(a =>
            (indice, CondicaoDnf.Criar(a.Fato, Operador.Igual, JsonSerializer.SerializeToElement(a.Valor)).Value!)))]).Value!;

    /// <summary>
    /// A modalidade ofertada, num quadro institucional fixo: a ampla concorrência e as demais retirando
    /// vagas dela. O que se prova aqui é a derivação das modalidades, e não o cálculo do quadro da lei
    /// nem a cascata das cotas, então só a reserva PcD tem a natureza de ação afirmativa, sem cota da
    /// lei ofertada.
    /// </summary>
    private static ModalidadeSelecionada ModalidadeDaLei(string codigo, PredicadoDnf regra) => codigo == "AC"
        ? ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, "Ampla concorrência", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, regra, null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 22).Value!
        : codigo == "AC_PCD"
            ? ModalidadeSelecionada.Criar(
                Guid.CreateVersion7(), codigo, $"Modalidade {codigo}", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiraDe,
                "AC", RegraRemanejamentoModalidade.DestinoUnico, "AC", null, null, regra, null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 2).Value!
            : ModalidadeSelecionada.Criar(
                Guid.CreateVersion7(), codigo, $"Modalidade {codigo}", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RetiraDe,
                "AC", RegraRemanejamentoModalidade.Nenhuma, null, null, null, regra, null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 2).Value!;

    /// <summary>A derivação na forma que o processo grava: cada cláusula do predicado vira um grupo de condições.</summary>
    private static ConfiguracaoDerivacaoFato Configurada(RegrasDerivacaoFato derivacao) =>
        ConfiguracaoDerivacaoFato.Criar(derivacao.CodigoFato,
        [
            .. derivacao.Regras.Select((regra, ordem) => RegraDerivacaoConfigurada.Criar(
                ordem,
                regra.Contribui,
                regra.EhAncora
                    ? null
                    : [.. regra.Quando.Clausulas.SelectMany((clausula, indice) => clausula.Condicoes.Select(c =>
                        CondicaoRegraDerivacao.Criar(indice + 1, c.Fato, c.Operador, c.Valor).Value!))]).Value!),
        ]).Value!;

    private static Dictionary<string, JsonElement> RespostasDaOrigemEDasCotas(string? ondeCursou, bool ep, bool ppi, bool renda)
    {
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(false),
            ["CONCORRER_EP"] = JsonSerializer.SerializeToElement(ep),
            ["CONCORRER_PPI"] = JsonSerializer.SerializeToElement(ppi),
            ["CONCORRER_Q"] = JsonSerializer.SerializeToElement(false),
            ["CONCORRER_RENDA"] = JsonSerializer.SerializeToElement(renda),
        };
        if (ondeCursou is not null)
        {
            respostas[OrigemEscolar.FatoFormaDeConclusao] = JsonSerializer.SerializeToElement(OrigemEscolar.Regular);
            respostas[OrigemEscolar.FatoOndeCursou] = JsonSerializer.SerializeToElement(ondeCursou);
        }

        return respostas;
    }

    [Fact(DisplayName = "Sobre a versão publicada com a matriz da Lei 12.711, a consulta deriva as modalidades da origem escolar e da opção pelas cotas")]
    public async Task Consulta_ComAMatrizNormativaPublicada_DerivaAsModalidadesDaLei()
    {
        await using SelecaoDbContext db = _fixture.CreateDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(
            db, ProcessoComAMatrizNormativa(nameof(Consulta_ComAMatrizNormativaPublicada_DerivaAsModalidadesDaLei)), OpcoesDaOrigemEscolar());
        IUserContext fernanda = UsuarioComCpf(PersonasDeTeste.FernandaDrumond);
        Result<InscricaoDto> iniciar = await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(processoId),
            new InscricaoRepository(db, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System), new CertameDivulgadoRepository(db),
            new ProcessoSeletivoRepository(db, TimeProvider.System), RegistroCodecs, Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(),
            DentroDoPeriodo, db, fernanda, CancellationToken.None);
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);

        ModalidadesDaInscricaoDto comCotas = (await AvaliarAsync(db, processoId, fernanda,
            RespostasDaOrigemEDasCotas(OrigemEscolar.SomentePublica, ep: true, ppi: true, renda: true))).Value!.Modalidades;
        ModalidadesDaInscricaoDto semCotas = (await AvaliarAsync(db, processoId, fernanda,
            RespostasDaOrigemEDasCotas(OrigemEscolar.SomentePublica, ep: false, ppi: false, renda: false))).Value!.Modalidades;
        ModalidadesDaInscricaoDto semOrigem = (await AvaliarAsync(db, processoId, fernanda,
            RespostasDaOrigemEDasCotas(ondeCursou: null, ep: true, ppi: false, renda: false))).Value!.Modalidades;

        comCotas.Situacao.Should().Be("CONCORRE");
        comCotas.Itens.Select(static m => m.Codigo).Should().Equal("AC", "LB_EP", "LB_PPI", "LI_EP", "LI_PPI");
        comCotas.Itens.Single(static m => m.Codigo == "AC").Nome.Should().Be("Ampla concorrência");
        semCotas.Itens.Select(static m => m.Codigo).Should().Equal("AC");
        semOrigem.Situacao.Should().Be("FALTA_RESPONDER");
        semOrigem.Pendencias.Select(static p => p.FatoCodigo).Should().Equal(OrigemEscolar.FatoFormaDeConclusao, OrigemEscolar.FatoOndeCursou);
    }

    /// <summary>
    /// O conjunto básico respondido com os valores que a publicação deste teste congela, o membro da
    /// composição familiar e as respostas próprias do processo: o envio só passa com todo obrigatório
    /// respondido.
    /// </summary>
    private static InscricaoRequest PedidoDeEnvio(ProcessoSeletivo processo, IReadOnlyDictionary<string, JsonElement> doProcesso)
    {
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["NOME"] = JsonSerializer.SerializeToElement("Fernanda Drumond"),
            ["DESEJA_NOME_SOCIAL"] = JsonSerializer.SerializeToElement(false),
            ["NACIONALIDADE"] = JsonSerializer.SerializeToElement("BRASILEIRA"),
            ["CPF"] = JsonSerializer.SerializeToElement(PersonasDeTeste.FernandaDrumond.Valor),
            ["RG_NUMERO"] = JsonSerializer.SerializeToElement("1234567"),
            ["RG_ORGAO_EMISSOR"] = JsonSerializer.SerializeToElement("SSP"),
            ["RG_UF"] = JsonSerializer.SerializeToElement("PA"),
            ["RG_DATA_EMISSAO"] = JsonSerializer.SerializeToElement("2010-01-01"),
            ["DATA_NASCIMENTO"] = JsonSerializer.SerializeToElement("2000-01-01"),
            ["NATURALIDADE_UF"] = JsonSerializer.SerializeToElement("PA"),
            ["NATURALIDADE_MUNICIPIO"] = JsonSerializer.SerializeToElement("1504208"),
            ["NOME_MAE"] = JsonSerializer.SerializeToElement("Maria Drumond"),
            ["ESTADO_CIVIL"] = JsonSerializer.SerializeToElement("SOLTEIRO"),
            ["SEXO"] = JsonSerializer.SerializeToElement("FEMININO"),
            ["COR_RACA"] = JsonSerializer.SerializeToElement("NAO_DECLARADO"),
            ["EMAIL"] = JsonSerializer.SerializeToElement("fernanda.drumond@example.com"),
            ["TELEFONE"] = JsonSerializer.SerializeToElement("94999999999"),
            ["TIPO_ENDERECO"] = JsonSerializer.SerializeToElement("URBANO"),
            ["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new { cep = (string?)null, cidade = new { codigoIbge = "1504208", uf = "PA" } }),
        };
        foreach ((string fato, JsonElement valor) in doProcesso)
        {
            respostas[fato] = valor;
        }

        TermoExigidoFormulario termo = processo.TermosExigidos.Single();
        return new InscricaoRequest(
            processo.DistribuicaoVagas.Single().OfertaCursoOrigemId,
            respostas,
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal)
            {
                ["COMPOSICAO_FAMILIAR"] =
                [
                    new OcorrenciaDeGrupoInscricaoInput("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["RG_NUMERO_MEMBRO"] = JsonSerializer.SerializeToElement("7654321"),
                        ["PARENTESCO"] = JsonSerializer.SerializeToElement("Mãe"),
                    }),
                ],
            },
            [new AceiteDeTermoInput(termo.Codigo, termo.VersaoId, termo.HashVersao)]);
    }

    private static IConferenciaEnderecoGeo GeoQueConfirma()
    {
        IConferenciaEnderecoGeo geo = Substitute.For<IConferenciaEnderecoGeo>();
        geo.ConferirMunicipioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        geo.ConferirCepAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        return geo;
    }

    /// <summary>Inicia a inscrição da Fernanda no processo publicado e devolve o ETag com que ela é enviada.</summary>
    private async Task<string> IniciarAsync(SelecaoDbContext db, Guid processoId)
    {
        Result<InscricaoDto> iniciar = await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(processoId),
            new InscricaoRepository(db, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System), new CertameDivulgadoRepository(db),
            new ProcessoSeletivoRepository(db, TimeProvider.System), RegistroCodecs, Fuso(), GeoQueConfirma(), Substitute.For<IEnderecoNoAcervoPublico>(),
            DentroDoPeriodo, db, UsuarioComCpf(PersonasDeTeste.FernandaDrumond), CancellationToken.None);
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);
        return iniciar.Value!.ETag;
    }

    private async Task<Result<ComprovanteInscricaoDto>> EnviarAsync(SelecaoDbContext db, Guid processoId, InscricaoRequest pedido, string etag, string numero) =>
        await SubmeterInscricaoCommandHandler.Handle(
            new SubmeterInscricaoCommand(processoId, pedido, PrecondicaoIfMatch.DeTags([etag])),
            new InscricaoRepository(db, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System),
            new ProcessoSeletivoRepository(db, TimeProvider.System), new CertameDivulgadoRepository(db), RegistroCodecs, Fuso(), GeoQueConfirma(),
            GeradorQueDevolve(numero), db, UsuarioComCpf(PersonasDeTeste.FernandaDrumond), DentroDoPeriodo, CancellationToken.None);

    private static IGeradorDeNumeroInscricao GeradorQueDevolve(string numero)
    {
        IGeradorDeNumeroInscricao gerador = Substitute.For<IGeradorDeNumeroInscricao>();
        gerador.ProximoAsync(Arg.Any<CancellationToken>()).Returns(numero);
        return gerador;
    }

    private async Task<(Inscricao Inscricao, IReadOnlyList<ComprovanteInscricao> Comprovantes)> RelerAsync(Guid processoId)
    {
        await using SelecaoDbContext leitura = _fixture.CreateDbContext();
        InscricaoRepository repositorio = new(leitura, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        Inscricao inscricao = (await repositorio.ObterPorProcessoECpfAsync(processoId, PersonasDeTeste.FernandaDrumond, CancellationToken.None))!;
        return (inscricao, await repositorio.ObterComprovantesAsync([inscricao.Id], CancellationToken.None));
    }

    private async Task<List<(int Posicao, int Origem, string Codigo)>> LinhasDaModalidadeAsync(Guid inscricaoId)
    {
        await using NpgsqlConnection connection = new(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT posicao, origem, valor_codigo FROM selecao.respostas_da_inscricao
            WHERE inscricao_id = @id AND fato_codigo = 'MODALIDADE' ORDER BY posicao
            """;
        command.Parameters.AddWithValue("id", inscricaoId);
        List<(int, int, string)> linhas = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            linhas.Add((reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2)));
        }

        return linhas;
    }

    private async Task<Result<InscricaoDto>> SalvarAsync(SelecaoDbContext db, Guid processoId, InscricaoRequest pedido, string etag) =>
        await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(processoId, pedido, PrecondicaoIfMatch.DeTags([etag])),
            new InscricaoRepository(db, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System),
            new ProcessoSeletivoRepository(db, TimeProvider.System), new CertameDivulgadoRepository(db), RegistroCodecs, Fuso(), GeoQueConfirma(),
            Substitute.For<IEnderecoNoAcervoPublico>(), db, UsuarioComCpf(PersonasDeTeste.FernandaDrumond), DentroDoPeriodo, CancellationToken.None);

    [Fact(DisplayName = "Salvar e enviar sobre a versão publicada recusam, no nível superior, o fato que a versão só coleta dentro de um grupo")]
    public async Task Resposta_DeSubitemDeGrupoNoNivelSuperior_ERecusada()
    {
        await using SelecaoDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = ProcessoComAMatrizNormativa(nameof(Resposta_DeSubitemDeGrupoNoNivelSuperior_ERecusada));
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo, OpcoesDaOrigemEscolar());
        string etag = await IniciarAsync(db, processoId);
        InscricaoRequest pedido = PedidoDeEnvio(processo, RespostasDaOrigemEDasCotas(OrigemEscolar.SomentePublica, ep: true, ppi: true, renda: true));
        Dictionary<string, JsonElement> comSubitemSolto = new(pedido.Respostas!, StringComparer.Ordinal)
        {
            ["PARENTESCO"] = JsonSerializer.SerializeToElement("Mãe"),
        };
        pedido = pedido with { Respostas = comSubitemSolto };

        Result<InscricaoDto> salvar = await SalvarAsync(db, processoId, pedido, etag);
        Result<ComprovanteInscricaoDto> envio = await EnviarAsync(db, processoId, pedido, etag, "00000103");

        salvar.Errors.Should().ContainSingle().Which.Field.Should().Be("PARENTESCO");
        salvar.Errors[0].Error.Code.Should().Be(InscricaoErrorCodes.FatoSoDeGrupo);
        envio.Errors.Should().ContainSingle().Which.Field.Should().Be("PARENTESCO");
        envio.Errors[0].Error.Code.Should().Be(InscricaoErrorCodes.FatoSoDeGrupo);
    }

    [Fact(DisplayName = "Salvar e enviar recusam a lista de grupo nula e a ocorrência nula com o caminho do campo, sem lançar")]
    public async Task Grupo_ComListaOuOcorrenciaNula_ERecusado()
    {
        await using SelecaoDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = ProcessoComAMatrizNormativa(nameof(Grupo_ComListaOuOcorrenciaNula_ERecusado));
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo, OpcoesDaOrigemEscolar());
        string etag = await IniciarAsync(db, processoId);
        InscricaoRequest pedido = PedidoDeEnvio(processo, RespostasDaOrigemEDasCotas(OrigemEscolar.SomentePublica, ep: true, ppi: true, renda: true));
        InscricaoRequest listaNula = pedido with
        {
            Grupos = new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal) { ["COMPOSICAO_FAMILIAR"] = null! },
        };
        InscricaoRequest ocorrenciaNula = pedido with
        {
            Grupos = new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal) { ["COMPOSICAO_FAMILIAR"] = [null!] },
        };

        Result<InscricaoDto> salvarListaNula = await SalvarAsync(db, processoId, listaNula, etag);
        Result<InscricaoDto> salvarOcorrenciaNula = await SalvarAsync(db, processoId, ocorrenciaNula, etag);
        Result<ComprovanteInscricaoDto> enviarListaNula = await EnviarAsync(db, processoId, listaNula, etag, "00000104");
        Result<ComprovanteInscricaoDto> enviarOcorrenciaNula = await EnviarAsync(db, processoId, ocorrenciaNula, etag, "00000105");

        salvarListaNula.Errors.Should().ContainSingle().Which.Field.Should().Be("COMPOSICAO_FAMILIAR");
        enviarListaNula.Errors.Should().ContainSingle().Which.Field.Should().Be("COMPOSICAO_FAMILIAR");
        salvarOcorrenciaNula.Errors.Should().ContainSingle().Which.Field.Should().Be("COMPOSICAO_FAMILIAR[0]");
        enviarOcorrenciaNula.Errors.Should().ContainSingle().Which.Field.Should().Be("COMPOSICAO_FAMILIAR[0]");
        new[] { salvarListaNula.Error, salvarOcorrenciaNula.Error, enviarListaNula.Error, enviarOcorrenciaNula.Error }
            .Should().OnlyContain(static e => e!.Code == InscricaoErrorCodes.GrupoMalFormado);
    }

    [Fact(DisplayName = "O envio sobre a versão publicada grava as modalidades em que o candidato concorre e o comprovante relido as traz com o nome congelado")]
    public async Task Envio_SobreAVersaoPublicada_GravaAsModalidadesEComprova()
    {
        await using SelecaoDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = ProcessoComAMatrizNormativa(nameof(Envio_SobreAVersaoPublicada_GravaAsModalidadesEComprova));
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo, OpcoesDaOrigemEscolar());
        string etag = await IniciarAsync(db, processoId);

        Result<ComprovanteInscricaoDto> envio = await EnviarAsync(
            db, processoId, PedidoDeEnvio(processo, RespostasDaOrigemEDasCotas(OrigemEscolar.SomentePublica, ep: true, ppi: true, renda: true)),
            etag, "00000101");

        envio.IsSuccess.Should().BeTrue(envio.Error?.Message ?? string.Join("; ", envio.Errors.Select(static e => $"{e.Field}: {e.Error.Message}")));
        (Inscricao enviada, IReadOnlyList<ComprovanteInscricao> comprovantes) = await RelerAsync(processoId);
        (await LinhasDaModalidadeAsync(enviada.Id)).Should().Equal(
            (0, (int)OrigemDaResposta.Derivada, "AC"), (1, (int)OrigemDaResposta.Derivada, "LB_EP"), (2, (int)OrigemDaResposta.Derivada, "LB_PPI"),
            (3, (int)OrigemDaResposta.Derivada, "LI_EP"), (4, (int)OrigemDaResposta.Derivada, "LI_PPI"));
        ComprovanteInscricao comprovante = comprovantes.Should().ContainSingle().Subject;
        comprovante.Modalidades!.Select(static m => (m.Codigo, m.Nome)).Should().Equal(
            ("AC", "Ampla concorrência"), ("LB_EP", "Modalidade LB_EP"), ("LB_PPI", "Modalidade LB_PPI"),
            ("LI_EP", "Modalidade LI_EP"), ("LI_PPI", "Modalidade LI_PPI"));
        comprovante.IntegridadeConfere().Should().BeTrue();
    }

    [Fact(DisplayName = "O envio sobre a versão publicada sem nenhuma modalidade aplicável é recusado, sem comprovante, e a inscrição continua em rascunho")]
    public async Task Envio_SemModalidade_RecusaEMantemORascunho()
    {
        await using SelecaoDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = ProcessoQueDerivaAModalidade(nameof(Envio_SemModalidade_RecusaEMantemORascunho));
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo);
        string etag = await IniciarAsync(db, processoId);

        Result<ComprovanteInscricaoDto> envio = await EnviarAsync(
            db, processoId,
            PedidoDeEnvio(processo, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(false) }),
            etag, "00000102");

        envio.Errors.Should().ContainSingle().Which.Field.Should().Be("CONCORRER_PCD");
        envio.Errors[0].Error.Code.Should().Be(InscricaoErrorCodes.NenhumaModalidade);
        (Inscricao inscricao, IReadOnlyList<ComprovanteInscricao> comprovantes) = await RelerAsync(processoId);
        inscricao.Situacao.Should().Be(SituacaoInscricao.Rascunho);
        comprovantes.Should().BeEmpty();
    }

    [Fact(DisplayName = "A correção que troca as modalidades emite um comprovante novo, com hash próprio, e o anterior fica como estava")]
    public async Task Correcao_QueTrocaAsModalidades_EmiteComprovanteNovo()
    {
        await using SelecaoDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = ProcessoComAMatrizNormativa(nameof(Correcao_QueTrocaAsModalidades_EmiteComprovanteNovo));
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo, OpcoesDaOrigemEscolar());
        string etag = await IniciarAsync(db, processoId);
        Result<ComprovanteInscricaoDto> primeiro = await EnviarAsync(
            db, processoId, PedidoDeEnvio(processo, RespostasDaOrigemEDasCotas(OrigemEscolar.SomentePublica, ep: true, ppi: false, renda: false)),
            etag, "00000103");
        primeiro.IsSuccess.Should().BeTrue(primeiro.Error?.Message);
        (Inscricao enviada, _) = await RelerAsync(processoId);

        Result<ComprovanteInscricaoDto> correcao = await EnviarAsync(
            db, processoId, PedidoDeEnvio(processo, RespostasDaOrigemEDasCotas(OrigemEscolar.SomentePublica, ep: false, ppi: false, renda: false)),
            enviada.ETag, "00000103");

        correcao.IsSuccess.Should().BeTrue(correcao.Error?.Message);
        (Inscricao corrigida, IReadOnlyList<ComprovanteInscricao> comprovantes) = await RelerAsync(processoId);
        comprovantes.Should().HaveCount(2);
        ComprovanteInscricao anterior = comprovantes.Single(c => c.HashIntegridade == primeiro.Value!.HashIntegridade);
        ComprovanteInscricao novo = comprovantes.Single(c => c.HashIntegridade == correcao.Value!.HashIntegridade);
        anterior.Modalidades!.Select(static m => m.Codigo).Should().Equal("AC", "LI_EP");
        anterior.IntegridadeConfere().Should().BeTrue();
        novo.Modalidades!.Select(static m => m.Codigo).Should().Equal("AC");
        novo.HashIntegridade.Should().NotBe(anterior.HashIntegridade);
        novo.IntegridadeConfere().Should().BeTrue();
        (await LinhasDaModalidadeAsync(corrigida.Id)).Select(static l => l.Codigo).Should().Equal("AC");
    }

    [Fact(DisplayName = "Salvar e ler a inscrição devolvem a avaliação do que ficou gravado, com as modalidades que a resposta decide")]
    public async Task SalvarELer_DevolvemAAvaliacaoDoGravado()
    {
        await using SelecaoDbContext db = _fixture.CreateDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, ProcessoQueDerivaAModalidade(nameof(SalvarELer_DevolvemAAvaliacaoDoGravado)));
        IUserContext fernanda = UsuarioComCpf(PersonasDeTeste.FernandaDrumond);
        InscricaoRepository inscricoes = new(db, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        ProcessoSeletivoRepository processos = new(db, TimeProvider.System);
        Result<InscricaoDto> iniciar = await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(processoId), inscricoes, new CertameDivulgadoRepository(db), processos, RegistroCodecs, Fuso(),
            Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), DentroDoPeriodo, db, fernanda, CancellationToken.None);
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);

        Result<InscricaoDto> salvar = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(
                processoId,
                new InscricaoRequest(null, new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(true),
                }, null, null),
                PrecondicaoIfMatch.DeTags([iniciar.Value!.ETag])),
            inscricoes, processos, new CertameDivulgadoRepository(db), RegistroCodecs, Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(),
            db, fernanda, DentroDoPeriodo, CancellationToken.None);
        Result<InscricaoDto> ler = await ObterInscricaoQueryHandler.Handle(
            new ObterInscricaoQuery(processoId), inscricoes, processos, RegistroCodecs, Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), fernanda,
            DentroDoPeriodo, CancellationToken.None);

        salvar.IsSuccess.Should().BeTrue(salvar.Error?.Message);
        ModalidadesDaInscricaoDto concorre = new(true, "CONCORRE", [new ModalidadeDaInscricaoDto("AC", "AC", 0)], []);
        salvar.Value!.Avaliacao.Modalidades.Should().BeEquivalentTo(concorre);
        ler.Value!.Avaliacao.Modalidades.Should().BeEquivalentTo(concorre);
    }
}
