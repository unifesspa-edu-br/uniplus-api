namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

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
                "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, ordem: 1).Value!,
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
                [modalidade]).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'), 1, [], baseadoEmEnem: false).Value!,
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
                produzResultado: true,
                resultadoDefinitivo: true,
                coletaInscricao: true,
                inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
                atoProduzidoCodigo: "RESULTADO_FINAL",
                atoProduzidoEfeitoIrreversivel: false,
                bancasRequeridas: [],
                regraRecurso: null).Value!],
            [],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null, confirmacaoFundamentos: false).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados([
            FatoColetado.Criar("COR_RACA", 0, "Cor ou raça", TipoRenderizacao.SelecaoUnica, obrigatorio: true, null).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirFormulario(
            "Formulário de Inscrição", "Declaro que as informações prestadas são verdadeiras.",
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    [Fact(DisplayName = "O formulário lido do banco real reproduz os valores selecionáveis congelados na publicação — leituras independentes e repetidas são idênticas")]
    public async Task Handle_ComPostgresReal_ReproduzOsValoresCongeladosEmLeiturasIndependentes()
    {
        ProcessoSeletivo processo = NovoProcessoComFatoDeSelecao(
            nameof(Handle_ComPostgresReal_ReproduzOsValoresCongeladosEmLeiturasIndependentes));

        const string hashDocumento = "2222222222222222222222222222222222222222222222222222222222222222";
        DadosEdital dados = DadosEdital.Criar(
            "001/2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.CreateVersion7()).Value!;

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
            processo, dados, hashDocumento, FusoInstitucional.ZoneId, ValoresSelecionaveisCongelados: valoresSelecionaveis));

        Result<VersaoConfiguracao> publicar = processo.Publicar(
            dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            hashDocumento, "integration-test-user", TimeProvider.System, CorpusEnvelope.ContextoRico());
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);

        Guid processoId = processo.Id;

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await repository.AdicionarVersaoConfiguracaoAsync(publicar.Value!, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        // Duas leituras INDEPENDENTES — dois DbContext distintos, a mesma separação que existe
        // entre duas requisições HTTP de fato diferentes. Nenhum leitor de catálogo é injetado:
        // se a resposta dependesse de qualquer estado externo ao byte persistido — cache, campo
        // estático, ordem de execução — as duas leituras poderiam divergir entre si.
        async Task<FormularioRenderizavelDto> LerAsync()
        {
            await using SelecaoDbContext readContext = _fixture.CreateDbContext();
            ProcessoSeletivoRepository repository = new(readContext, TimeProvider.System);
            Result<FormularioRenderizavelDto> resultado = await ObterFormularioRenderizavelQueryHandler.Handle(
                new ObterFormularioRenderizavelQuery(processoId), repository, RegistroCodecs, TimeProvider.System, CancellationToken.None);
            resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
            return resultado.Value!;
        }

        FormularioRenderizavelDto primeira = await LerAsync();
        FormularioRenderizavelDto segunda = await LerAsync();

        FatoFormularioRenderizavelDto corRaca = primeira.FatosColetados.Single(f => f.FatoCodigo == "COR_RACA");
        corRaca.ValoresSelecionaveis.Should().NotBeNull();
        corRaca.ValoresSelecionaveis!.Select(v => v.Codigo).Should().Equal(
            ["BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_DECLARADO"],
            "o formulário lido do banco traz os SEIS valores congelados na publicação, na ordem canônica — " +
            "os mesmos que o catálogo declarava íntegro no instante em que o certame foi publicado");

        segunda.Should().BeEquivalentTo(primeira,
            "duas leituras independentes da MESMA versão publicada — sem catálogo algum na assinatura do " +
            "handler — não têm de onde divergir: o formulário é função pura dos bytes persistidos");
    }
}
