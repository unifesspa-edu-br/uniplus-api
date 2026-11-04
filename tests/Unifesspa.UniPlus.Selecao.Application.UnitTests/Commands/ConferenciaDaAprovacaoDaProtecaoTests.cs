namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A publicação e a retificação só congelam proteção que a proteção de dados aprovou (ADR-0153): o item pendente é
/// recusado no próprio campo, ainda que tenha voltado a pendente depois de vinculado ao processo.
/// </summary>
public sealed class ConferenciaDaAprovacaoDaProtecaoTests
{
    private static readonly JsonElement Qualquer = JsonSerializer.SerializeToElement("QUALQUER");

    [Fact(DisplayName = "O tipo da exigência e o fato que ela comprova, voltados a pendente, barram a publicação, cada um no seu campo")]
    public async Task TipoEFatoComprovadoPendentes_SaoRecusadosJuntos()
    {
        (ProcessoSeletivo processo, TipoDocumentoView tipo) = await ProcessoComExigenciaAsync(fatosComprovados: ["MODALIDADE"]);

        Result resultado = ConferenciaDaAprovacaoDaProtecao.Conferir(
            processo, Vivos(tipo with { ProtecaoAprovada = false }), [Fato("MODALIDADE", "DERIVADO", aprovada: false), Fato("SEXO", "DECLARADO", aprovada: false)]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("documentosExigidos.LAUDO_MEDICO", "ProcessoSeletivo.TipoDocumentoComProtecaoPendente"),
            ("fatos.MODALIDADE", "ProcessoSeletivo.FatoComProtecaoPendente"));
        resultado.Errors[0].Error.Message.Should().Contain("Laudo médico").And.Contain("aprovação");
    }

    [Fact(DisplayName = "O fato derivado pendente que o processo não usa não bloqueia; o que o processo usa bloqueia")]
    public async Task DerivadoPendente_SoBloqueiaQuandoOProcessoOUsa()
    {
        (ProcessoSeletivo semUso, TipoDocumentoView tipoSemUso) = await ProcessoComExigenciaAsync(fatosComprovados: []);
        (ProcessoSeletivo comUso, TipoDocumentoView tipoComUso) = await ProcessoComExigenciaAsync(fatosComprovados: ["MODALIDADE"]);
        FatoCandidatoView[] catalogo = [Fato("MODALIDADE", "DERIVADO", aprovada: false), Fato("SEXO", "DECLARADO", aprovada: false)];

        ConferenciaDaAprovacaoDaProtecao.Conferir(semUso, Vivos(tipoSemUso), catalogo).IsSuccess.Should().BeTrue();
        ConferenciaDaAprovacaoDaProtecao.Conferir(comUso, Vivos(tipoComUso), catalogo).Errors
            .Should().ContainSingle().Which.Error.Code.Should().Be("ProcessoSeletivo.FatoComProtecaoPendente");
    }

    [Fact(DisplayName = "Tipo e fatos aprovados deixam a publicação seguir")]
    public async Task TudoAprovado_Segue()
    {
        (ProcessoSeletivo processo, TipoDocumentoView tipo) = await ProcessoComExigenciaAsync(fatosComprovados: ["MODALIDADE"]);

        ConferenciaDaAprovacaoDaProtecao.Conferir(processo, Vivos(tipo), [Fato("MODALIDADE", "DERIVADO", aprovada: true)]).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "O tipo exigido que o cadastro não devolve mais (excluído) é recusado em vez de passar como aprovado")]
    public async Task TipoExcluido_E_Recusado()
    {
        (ProcessoSeletivo processo, TipoDocumentoView tipo) = await ProcessoComExigenciaAsync(fatosComprovados: []);

        Result resultado = ConferenciaDaAprovacaoDaProtecao.Conferir(processo, new Dictionary<Guid, TipoDocumentoView>(), []);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("DocumentoExigido.TipoDocumentoNaoEncontrado");
        resultado.Errors[0].Field.Should().Be($"documentosExigidos.{tipo.Id}");
    }

    private static Dictionary<Guid, TipoDocumentoView> Vivos(TipoDocumentoView tipo) => new() { [tipo.Id] = tipo };

    private static async Task<(ProcessoSeletivo Processo, TipoDocumentoView Tipo)> ProcessoComExigenciaAsync(string[] fatosComprovados)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Medicina", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseCronograma.Criar(
            1, Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NaExecucao,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false, inicio: null, fim: null,
            produtos: [], faseConcluinteCodigo: null, emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        TipoDocumentoView tipo = new(Guid.CreateVersion7(), "LAUDO_MEDICO", "Laudo médico", "SAUDE", ClassificacaoDeProtecao.Sensivel, "Comprovação do atendimento especializado.", "TUTELA_DA_SAUDE", ProtecaoAprovada: true);
        ITipoDocumentoReader leitor = Substitute.For<ITipoDocumentoReader>();
        leitor.ObterPorIdAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);
        ItemDocumentoExigidoInput item = new(fase.Id, tipo.Id, "GERAL", true, null, [], [], null, Qualquer, null, FatosComprovados: fatosComprovados);
        Result definido = await DefinicaoDosDocumentosExigidos.DefinirAsync(
            processo, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], [], PrecondicaoIfMatch.Ausente,
            new CadastrosDosDocumentosExigidos(leitor, Substitute.For<IFatoCandidatoReader>(), Substitute.For<Unifesspa.UniPlus.Selecao.Domain.Interfaces.IModeloDeDocumentoRepository>()),
            CancellationToken.None);
        definido.IsSuccess.Should().BeTrue(definido.Error?.Message);

        return (processo, tipo);
    }

    private static FatoCandidatoView Fato(string codigo, string origem, bool aprovada) => new(
        Guid.CreateVersion7(), codigo, codigo, "Descrição do fato.", "CATEGORICO", origem, "ESCALAR", null, "INSCRICAO",
        origem == "DERIVADO" ? $"REGRA_DERIVACAO:{codigo}" : $"CAMPO_FORMULARIO:{codigo}", null, "GLOBAL", Ativo: true,
        ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade,
        ProtecaoAprovada: aprovada);
}
