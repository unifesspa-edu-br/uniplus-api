namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDosDocumentosExigidos;

/// <summary>
/// O gatilho da exigência quanto à fase: o fato citado não pode ser resolvido numa fase posterior à da exigência, a folha repetida cita o campo do grupo e a habilitação cita o grupo em que o candidato foi convocado.
/// </summary>
public sealed class DefinirDocumentosExigidosGatilhoPorFaseCommandHandlerTests
{
    [Theory(DisplayName = "O gatilho da folha repetida cita o campo do grupo, marcada na folha ou herdada do grupo ancestral")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_GatilhoPorCampoDoGrupo_AceitoNaSubarvoreRepetida(bool herdadaDoAncestral)
    {
        (ProcessoSeletivo processo, FaseCronograma fase, Mocks mocks, Guid tipoDocumentoId) = ProcessoComComposicaoFamiliar();
        NoExigenciaDeTeste folha = NoExigenciaDeTeste.Folha(ItemDoMembroMaiorDeIdade(fase.Id, tipoDocumentoId));
        RegraDeComprovacaoInformada raiz = herdadaDoAncestral
            ? NoExigenciaDeTeste.Grupo("E", folha.Construir()).RepetidoPor("COMPOSICAO_FAMILIAR").Construir()
            : folha.RepetidoPor("COMPOSICAO_FAMILIAR").Construir();

        Result resultado = await HandleAsync(mocks, new DefinirDocumentosExigidosCommand(processo.Id, [raiz]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().ContainSingle(d => d.Condicoes.Any(c => c.Fato == "MAIOR_IDADE"));
    }

    [Fact(DisplayName = "Fora da subárvore repetida, o campo do grupo não é citável")]
    public async Task Handle_CampoDoGrupoForaDaRepeticao_Recusa()
    {
        (ProcessoSeletivo processo, FaseCronograma fase, Mocks mocks, Guid tipoDocumentoId) = ProcessoComComposicaoFamiliar();
        ItemDocumentoExigidoDeTeste item = ItemDoMembroMaiorDeIdade(fase.Id, tipoDocumentoId);

        Result resultado = await HandleAsync(mocks, new DefinirDocumentosExigidosCommand(
            processo.Id, [NoExigenciaDeTeste.Folha(item).Construir()]));

        resultado.Error!.Code.Should().Be("PredicadoDnf.FatoNaoColetadoPeloProcesso");
    }

    [Fact(DisplayName = "Repetir por grupo inexistente é recusado pelo grupo, antes do gatilho que cita o campo dele")]
    public async Task Handle_RepeticaoPorGrupoInexistente_RecusaPeloGrupo()
    {
        (ProcessoSeletivo processo, FaseCronograma fase, Mocks mocks, Guid tipoDocumentoId) = ProcessoComComposicaoFamiliar();
        ItemDocumentoExigidoDeTeste item = ItemDoMembroMaiorDeIdade(fase.Id, tipoDocumentoId);

        Result resultado = await HandleAsync(mocks, new DefinirDocumentosExigidosCommand(
            processo.Id, [NoExigenciaDeTeste.Folha(item).RepetidoPor("PESSOAS_JURIDICAS").Construir()]));

        resultado.Error!.Code.Should().Be("NoExigencia.TipoEntidadeInvalido");
    }

    [Fact(DisplayName = "Gate de fase: condição sobre fato cujo PontoResolucao é uma fase POSTERIOR à da exigência é recusada")]
    public async Task Handle_GatilhoFatoDeFasePosterior_RetornaErroNomeado()
    {
        FaseCronograma inscricao = FaseComOrdemECodigo(1, "INSCRICAO");
        ProcessoSeletivo processo = ProcessoComCronograma(inscricao, FaseComOrdemECodigo(2, "HOMOLOGACAO_INSCRICOES"));

        // SEXO só é conhecido na fase HOMOLOGACAO_INSCRICOES (ordem 2), mas o documento é exigido na
        // fase INSCRICAO (ordem 1) — anterior. O gatilho nunca teria como já ter sido
        // resolvido para o candidato.
        Result resultado = await DefinirExigenciaPeloSexoAsync(processo, inscricao.Id, "HOMOLOGACAO_INSCRICOES");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.FatoResolvidoEmFasePosterior");
    }

    [Theory(DisplayName = "Gate de fase: condição sobre fato cujo PontoResolucao é a mesma fase ou uma fase ANTERIOR à da exigência é aceita")]
    [InlineData("INSCRICAO")]
    [InlineData("HOMOLOGACAO_INSCRICOES")]
    public async Task Handle_GatilhoFatoDeFaseIgualOuAnterior_Aceita(string pontoResolucao)
    {
        FaseCronograma homologacao = FaseComOrdemECodigo(2, "HOMOLOGACAO_INSCRICOES");
        ProcessoSeletivo processo = ProcessoComCronograma(FaseComOrdemECodigo(1, "INSCRICAO"), homologacao);

        // O documento é exigido na fase HOMOLOGACAO_INSCRICOES (ordem 2) — SEXO conhecido na própria
        // fase ou numa fase anterior (INSCRICAO, ordem 1) satisfaz o gate.
        Result resultado = await DefinirExigenciaPeloSexoAsync(processo, homologacao.Id, pontoResolucao);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Gate de fase: PontoResolucao do fato citado não pertence ao cronograma deste processo é recusado")]
    public async Task Handle_GatilhoComPontoResolucaoForaDoCronograma_RetornaErroNomeado()
    {
        FaseCronograma inscricao = FaseComOrdemECodigo(1, "INSCRICAO");
        ProcessoSeletivo processo = ProcessoComCronograma(inscricao);

        // SEXO resolve numa fase ("HOMOLOGACAO_INSCRICOES") que não existe no cronograma deste processo.
        Result resultado = await DefinirExigenciaPeloSexoAsync(processo, inscricao.Id, "HOMOLOGACAO_INSCRICOES");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.PontoResolucaoForaDoCronograma");
    }

    private static ProcessoSeletivo ProcessoComCronograma(params FaseCronograma[] fases)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirCronogramaFases(fases, []).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static async Task<Result> DefinirExigenciaPeloSexoAsync(
        ProcessoSeletivo processo, Guid faseDaExigenciaId, string pontoResolucaoDoSexo)
    {
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoSexoComPontoResolucao(pontoResolucaoDoSexo)]));
        ColetarFato(processo, "SEXO", TipoRenderizacao.SelecaoUnica);
        CondicaoGatilhoInput condicao = new(0, "SEXO", "IGUAL", "\"MASCULINO\"");
        ItemDocumentoExigidoDeTeste item = ItemDocumentoExigidoDeTeste.Geral(faseDaExigenciaId, tipoDocumentoId).Condicional(condicao);

        return await HandleAsync(mocks, new(processo.Id, [NoExigenciaDeTeste.Folha(item).Construir()]));
    }

    [Fact(DisplayName = "Exigência da habilitação cita o grupo em que o candidato foi convocado")]
    public async Task Handle_GatilhoPeloGrupoDaConvocacaoNaHabilitacao_Aceita()
    {
        (ProcessoSeletivo processo, Mocks mocks, Guid tipoDocumentoId) = ProcessoComAmplaConcorrencia(
            FaseComOrdemECodigo(1, "RESULTADO_FINAL"), FaseComOrdemECodigo(2, "HABILITACAO"));
        Guid habilitacao = processo.CronogramaFases.Single(f => f.Codigo == "HABILITACAO").Id;

        Result resultado = await HandleAsync(mocks, ExigenciaPeloGrupoDaConvocacao(processo, habilitacao, tipoDocumentoId));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Sem a fase do resultado final no cronograma, o grupo da convocação não é citado")]
    public async Task Handle_GatilhoPeloGrupoDaConvocacaoSemResultadoFinal_Recusa()
    {
        (ProcessoSeletivo processo, Mocks mocks, Guid tipoDocumentoId) = ProcessoComAmplaConcorrencia(FaseComOrdemECodigo(1, "HABILITACAO"));

        Result resultado = await HandleAsync(
            mocks, ExigenciaPeloGrupoDaConvocacao(processo, processo.CronogramaFases.Single().Id, tipoDocumentoId));

        resultado.Error!.Code.Should().Be("DocumentoExigido.PontoResolucaoForaDoCronograma");
    }

    private static FatoCandidatoView FatoSexoComPontoResolucao(string pontoResolucao) => new(
        Guid.CreateVersion7(), "SEXO", "Sexo", "Descrição do fato.", "CATEGORICO", "DECLARADO", "ESCALAR",
        ["MASCULINO", "FEMININO", "INTERSEXO"], pontoResolucao, "CAMPO_FORMULARIO:SEXO", null, "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

    private static FaseCronograma FaseComOrdemECodigo(int ordem, string codigo) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static DefinirDocumentosExigidosCommand ExigenciaPeloGrupoDaConvocacao(ProcessoSeletivo processo, Guid faseId, Guid tipoDocumentoId)
    {
        CondicaoGatilhoInput condicao = new(0, "MODALIDADE_CONVOCACAO", "IGUAL", "\"AC\"");
        ItemDocumentoExigidoDeTeste item = ItemDocumentoExigidoDeTeste.Geral(faseId, tipoDocumentoId).Condicional(condicao).NaFinalidade("HABILITACAO");
        return new(processo.Id, [NoExigenciaDeTeste.Folha(item).Construir()]);
    }

    private static ItemDocumentoExigidoDeTeste ItemDoMembroMaiorDeIdade(Guid faseId, Guid tipoDocumentoId) =>
        ItemDocumentoExigidoDeTeste.Geral(faseId, tipoDocumentoId)
            .Condicional(new CondicaoGatilhoInput(0, "MAIOR_IDADE", "IGUAL", "true"))
            .Facultativa()
            .ComConsequencia("ELIMINA");
}
