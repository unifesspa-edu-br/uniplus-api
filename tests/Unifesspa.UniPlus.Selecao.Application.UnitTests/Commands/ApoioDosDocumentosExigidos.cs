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

/// <summary>
/// Mocks, processo e fatos de teste partilhados pelos testes do handler que define os documentos exigidos.
/// </summary>
internal static class ApoioDosDocumentosExigidos
{
    /// <summary>FormatosPermitidos agora obrigatório (Story #918) — QUALQUER é o valor neutro dos testes que não são sobre formato.</summary>
    internal static readonly JsonElement Qualquer = JsonSerializer.SerializeToElement("QUALQUER");

    internal sealed record Mocks(
        IProcessoSeletivoRepository Repository,
        ITipoDocumentoReader TipoDocumentoReader,
        IFatoCandidatoReader FatoCandidatoReader,
        IModeloDeDocumentoRepository Modelos,
        IUnitOfWork UnitOfWork);

    internal static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);

        return new Mocks(
            repository,
            Substitute.For<ITipoDocumentoReader>(),
            Substitute.For<IFatoCandidatoReader>(),
            Substitute.For<IModeloDeDocumentoRepository>(),
            Substitute.For<IUnitOfWork>());
    }

    internal static Task<Result> HandleAsync(Mocks mocks, DefinirDocumentosExigidosCommand command) =>
        DefinirDocumentosExigidosCommandHandler.Handle(
            command,
            mocks.Repository,
            mocks.TipoDocumentoReader,
            mocks.FatoCandidatoReader,
            mocks.Modelos,
            mocks.UnitOfWork,
            CancellationToken.None);

    internal static TipoDocumentoView TipoDocumentoResultado(Guid id) =>
        new(id, "IDENTIDADE", "Documento de identidade", "PESSOAL", ClassificacaoDeProtecao.Pessoal, "Comprovação documental do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL", ProtecaoAprovada: true);

    internal static FaseCronograma FaseQualquer() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(1, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    // Os valores são os tipos do cadastro de tipos de deficiência, que o catálogo entrega como valores do fato.
    internal static FatoCandidatoView FatoTipoDeficiencia() => new(
        Guid.CreateVersion7(), "TIPO_DEFICIENCIA", "Tipo de deficiência", "Descrição do fato.", "CATEGORICO", "DECLARADO", "ESCALAR",
        ["DEFICIENCIA_VISUAL", "TEA"], "INSCRICAO", "CAMPO_FORMULARIO:TIPO_DEFICIENCIA",
        [new FatoValorDominioViewItem("DEFICIENCIA_VISUAL", "Deficiência visual", 0, Ativo: true),
         new FatoValorDominioViewItem("TEA", "Transtorno do espectro autista", 1, Ativo: true)],
        "TIPOS_DEFICIENCIA", Ativo: true, ClassificacaoProtecao: "SENSIVEL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

    /// <summary>
    /// Faz o processo coletar o fato, que é o que torna um gatilho sobre ele resolvível.
    ///
    /// Sem isto, a exigência condicionada nunca se aplicaria a candidato nenhum — o fato
    /// ficaria indeterminado para sempre, e a exigência pendente. O comando recusa por isso.
    /// </summary>
    internal static void ColetarFato(ProcessoSeletivo processo, string codigo, TipoRenderizacao renderizacao = TipoRenderizacao.SelecaoUnica)
    {
        FatoColetado fato = FatoColetadoDeTeste.Coletando(codigo, 0).DoTipo(renderizacao).ComObrigatoriedade(Obrigatoriedade.Sempre).Construir();
        processo.DefinirItens([fato]).IsSuccess.Should().BeTrue();
    }

    /// <summary>Processo com o grupo da composição familiar no formulário, e o catálogo com o campo de membro.</summary>
    internal static (ProcessoSeletivo Processo, FaseCronograma Fase, Mocks Mocks, Guid TipoDocumentoId) ProcessoComComposicaoFamiliar()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase([FatoMaiorIdadeDoMembro()]);
        cenario.Processo.DefinirItens([], grupos: [GrupoDaComposicaoFamiliar()]).IsSuccess.Should().BeTrue();

        return (cenario.Processo, cenario.Fase, cenario.Mocks, cenario.TipoDocumentoId);
    }

    private static GrupoColetado GrupoDaComposicaoFamiliar() =>
        GrupoColetadoDeTeste.Agrupando("COMPOSICAO_FAMILIAR", 0)
            .ComOcorrencias(1, 10)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .ComCampos(FatoColetadoDeTeste.Coletando("MAIOR_IDADE", 0).ComRotulo("Maior de idade").ComObrigatoriedade(Obrigatoriedade.Sempre).Construir())
            .Construir();

    private static FatoCandidatoView FatoMaiorIdadeDoMembro() =>
        new(Guid.CreateVersion7(), "MAIOR_IDADE", "Maior de idade", "Descrição do fato.", "BOOLEANO", "DECLARADO", "ESCALAR",
            null, "INSCRICAO", "CAMPO_FORMULARIO:MAIOR_IDADE", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, Escopo: "MEMBRO_GRUPO", ProtecaoAprovada: true);

    internal static (ProcessoSeletivo Processo, Mocks Mocks, Guid TipoDocumentoId) ProcessoComAmplaConcorrencia(params FaseCronograma[] fases)
    {
        ProcessoSeletivo processo = NovoProcesso("PS Convocação");
        processo.DefinirDistribuicaoVagas([ProcessoSeletivoConformeBuilder.DistribuicaoAmplaConcorrencia()], FatosDeModalidadeDeTeste.DoCatalogo)
            .IsSuccess.Should().BeTrue();
        processo.DefinirCronogramaFases(fases, []).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        ConfigurarTipoDeDocumento(mocks, tipoDocumentoId);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com(CadastrosVivos.FatosDeModalidade()));
        return (processo, mocks, tipoDocumentoId);
    }

    /// <summary>O processo, uma fase, os mocks do handler e o id do tipo de documento que o leitor devolve.</summary>
    internal sealed record CenarioDaExigencia(ProcessoSeletivo Processo, FaseCronograma Fase, Mocks Mocks, Guid TipoDocumentoId)
    {
        /// <summary>Exigência geral e obrigatória, sem condições nem bases legais, com qualquer formato.</summary>
        internal ItemDocumentoExigidoDeTeste ItemGeral(JsonElement? formatosPermitidos = null, int? tamanhoMaximoBytes = null)
        {
            ItemDocumentoExigidoDeTeste item = ItemDocumentoExigidoDeTeste.Geral(Fase.Id, TipoDocumentoId).ComTamanhoMaximo(tamanhoMaximoBytes);
            return formatosPermitidos is { } formatos ? item.ComFormatos(formatos) : item;
        }

        internal ItemDocumentoExigidoDeTeste ItemCondicional(CondicaoGatilhoInput condicao) => ItemGeral().Condicional(condicao);

        /// <summary>O leitor de tipos passa a devolver o tipo dado para o id do cenário (ou nenhum, quando é <see langword="null"/>).</summary>
        internal CenarioDaExigencia ComTipoDeDocumento(Func<Guid, TipoDocumentoView?> tipo)
        {
            Mocks.TipoDocumentoReader.ObterPorIdAsync(TipoDocumentoId, Arg.Any<CancellationToken>()).Returns(tipo(TipoDocumentoId));
            return this;
        }

        internal DefinirDocumentosExigidosCommand Comando(ItemDocumentoExigidoInput item) =>
            new(Processo.Id, [RegraDeComprovacaoDeTeste.DeDocumento(item).Construir()]);

        internal DefinirDocumentosExigidosCommand Comando(ItemDocumentoExigidoDeTeste item) => Comando(item.Construir());

        internal Task<Result> DefinirAsync(ItemDocumentoExigidoDeTeste item) => HandleAsync(Mocks, Comando(item));
    }

    internal static ProcessoSeletivo NovoProcesso(string nome) =>
        ProcessoSeletivo.Criar(
            nome, TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    /// <summary>
    /// Os leitores ficam sem configuração: serve aos testes que provam a recusa antes de qualquer leitura.
    /// </summary>
    internal static CenarioDaExigencia ProcessoComUmaFaseSemLeitoresConfigurados()
    {
        ProcessoSeletivo processo = NovoProcesso("PS Handler");
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();

        return new CenarioDaExigencia(processo, fase, NovosMocks(processo, processo.Id), Guid.CreateVersion7());
    }

    /// <summary>O leitor de tipos devolve o tipo de documento; o de fatos, o catálogo, quando ele é informado.</summary>
    internal static CenarioDaExigencia ProcessoComUmaFase(IReadOnlyList<FatoCandidatoView>? catalogoDeFatos = null)
    {
        CenarioDaExigencia cenario = ProcessoComUmaFaseSemLeitoresConfigurados();
        ConfigurarTipoDeDocumento(cenario.Mocks, cenario.TipoDocumentoId);
        if (catalogoDeFatos is not null)
        {
            cenario.Mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com(catalogoDeFatos));
        }

        return cenario;
    }

    private static void ConfigurarTipoDeDocumento(Mocks mocks, Guid tipoDocumentoId) =>
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
}
