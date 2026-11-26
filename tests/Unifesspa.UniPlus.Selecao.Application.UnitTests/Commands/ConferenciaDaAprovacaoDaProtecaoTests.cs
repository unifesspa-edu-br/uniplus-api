namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
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
            processo, Vivos(tipo with { ProtecaoAprovada = false }), [Fato("MODALIDADE", OrigemFato.Calculado, aprovada: false), Fato("SEXO", OrigemFato.Declarado, aprovada: false)]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("documentosExigidos.LAUDO_MEDICO", "ProcessoSeletivo.TipoDocumentoComProtecaoPendente"),
            ("fatos.MODALIDADE", "ProcessoSeletivo.FatoComProtecaoPendente"));
    }

    [Fact(DisplayName = "As recusas da publicação nomeiam o tipo de documento e o dado do candidato pelo nome, sem o código, e dizem quem aprova")]
    public async Task TipoEDadoPendentes_NomeiamOItemSemOCodigo()
    {
        (ProcessoSeletivo processo, TipoDocumentoView tipo) = await ProcessoComExigenciaAsync(fatosComprovados: ["MODALIDADE"]);
        FatoCandidatoView dado = Fato("MODALIDADE", OrigemFato.Calculado, aprovada: false) with { Nome = "Modalidade de concorrência" };

        Result resultado = ConferenciaDaAprovacaoDaProtecao.Conferir(processo, Vivos(tipo with { ProtecaoAprovada = false }), [dado]);

        resultado.Errors.Select(static e => e.Error.Message).Should().Equal(
            "O tipo de documento 'Laudo médico', exigido pelo processo, aguarda a aprovação do Representante do controlador, "
            + "e o processo só pode ser publicado ou retificado depois dela. Peça ao administrador do catálogo que envie a solicitação de aprovação ou, "
            + "se houver ajuste recomendado, que aceite a recomendação; ou troque o tipo de documento da exigência.",
            "O dado do candidato 'Modalidade de concorrência', usado pelo processo, aguarda a aprovação do Representante do controlador, "
            + "e o processo só pode ser publicado ou retificado depois dela. Peça ao administrador do catálogo que envie a solicitação de aprovação ou, "
            + "se houver ajuste recomendado, que aceite a recomendação; ou retire o dado do formulário, da regra, da condição ou da exigência de documento que o usa.");
    }

    [Fact(DisplayName = "O fato derivado pendente que o processo não usa não bloqueia; o que o processo usa bloqueia")]
    public async Task DerivadoPendente_SoBloqueiaQuandoOProcessoOUsa()
    {
        (ProcessoSeletivo semUso, TipoDocumentoView tipoSemUso) = await ProcessoComExigenciaAsync(fatosComprovados: []);
        (ProcessoSeletivo comUso, TipoDocumentoView tipoComUso) = await ProcessoComExigenciaAsync(fatosComprovados: ["MODALIDADE"]);
        FatoCandidatoView[] catalogo = [Fato("MODALIDADE", OrigemFato.Calculado, aprovada: false), Fato("SEXO", OrigemFato.Declarado, aprovada: false)];

        ConferenciaDaAprovacaoDaProtecao.Conferir(semUso, Vivos(tipoSemUso), catalogo).IsSuccess.Should().BeTrue();
        ConferenciaDaAprovacaoDaProtecao.Conferir(comUso, Vivos(tipoComUso), catalogo).Errors
            .Should().ContainSingle().Which.Error.Code.Should().Be("ProcessoSeletivo.FatoComProtecaoPendente");
    }

    [Fact(DisplayName = "Tipo e fatos aprovados deixam a publicação seguir")]
    public async Task TudoAprovado_Segue()
    {
        (ProcessoSeletivo processo, TipoDocumentoView tipo) = await ProcessoComExigenciaAsync(fatosComprovados: ["MODALIDADE"]);

        ConferenciaDaAprovacaoDaProtecao.Conferir(processo, Vivos(tipo), [Fato("MODALIDADE", OrigemFato.Calculado, aprovada: true)]).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "O tipo exigido que o cadastro não devolve mais (excluído) é recusado em vez de passar como aprovado")]
    public async Task TipoExcluido_E_Recusado()
    {
        (ProcessoSeletivo processo, TipoDocumentoView tipo) = await ProcessoComExigenciaAsync(fatosComprovados: []);

        Result resultado = ConferenciaDaAprovacaoDaProtecao.Conferir(processo, new Dictionary<Guid, TipoDocumentoView>(), []);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("DocumentoExigido.TipoDocumentoNaoEncontrado");
        resultado.Errors[0].Field.Should().Be($"documentosExigidos.{tipo.Id}");
    }

    [Fact(DisplayName = "A proteção vigente marca a exigência que a aprovação recusa: tipo pendente, tipo excluído e fato comprovado pendente; a de tipo e fatos aprovados não")]
    public async Task ProtecaoVigente_MarcaAsExigenciasQueAAprovacaoRecusa()
    {
        (ProcessoSeletivo pendente, TipoDocumentoView tipoPendente) = await ProcessoComExigenciaAsync(fatosComprovados: []);
        (ProcessoSeletivo excluido, _) = await ProcessoComExigenciaAsync(fatosComprovados: []);
        (ProcessoSeletivo comFatoPendente, TipoDocumentoView tipoComFato) = await ProcessoComExigenciaAsync(fatosComprovados: ["MODALIDADE"]);
        (ProcessoSeletivo aprovado, TipoDocumentoView tipoAprovado) = await ProcessoComExigenciaAsync(fatosComprovados: ["MODALIDADE"]);

        MarcadasDe(pendente, Vivos(tipoPendente with { ProtecaoAprovada = false }), []).Should().Equal(pendente.DocumentosExigidos.Single().Id);
        MarcadasDe(excluido, new Dictionary<Guid, TipoDocumentoView>(), []).Should().Equal(excluido.DocumentosExigidos.Single().Id);
        MarcadasDe(comFatoPendente, Vivos(tipoComFato), [Fato("MODALIDADE", OrigemFato.Calculado, aprovada: false)]).Should().Equal(comFatoPendente.DocumentosExigidos.Single().Id);
        MarcadasDe(aprovado, Vivos(tipoAprovado), [Fato("MODALIDADE", OrigemFato.Calculado, aprovada: true)]).Should().BeEmpty();
    }

    private static IReadOnlySet<Guid> MarcadasDe(
        ProcessoSeletivo processo, IReadOnlyDictionary<Guid, TipoDocumentoView> tiposVivos, IReadOnlyList<FatoCandidatoView> catalogo) =>
        ProtecaoVigenteDasExigenciasDoProcesso.Montar(processo, tiposVivos, catalogo, daVersaoVigente: null).Exigencias.ExigenciasRecusadasPelaAprovacao;

    private static Dictionary<Guid, TipoDocumentoView> Vivos(params TipoDocumentoView[] tipos) => tipos.ToDictionary(static t => t.Id);

    private static FaseCronograma FaseDeInscricao() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(1, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    [Fact(DisplayName = "Os tipos de documento pendentes são recusados na ordem ordinal dos códigos, não na ordem das exigências")]
    public async Task TiposPendentes_SaoRecusadosNaOrdemDosCodigos()
    {
        (ProcessoSeletivo processo, FaseCronograma fase) = ProcessoComUmaFase();
        TipoDocumentoView laudo = TipoAprovado("Z_LAUDO", "Laudo médico");
        TipoDocumentoView atestado = TipoAprovado("A_ATESTADO", "Atestado de matrícula");
        await DefinirExigenciasDosTiposAsync(processo, fase, [laudo, atestado]);

        Result resultado = ConferenciaDaAprovacaoDaProtecao.Conferir(processo, Vivos(laudo with { ProtecaoAprovada = false }, atestado with { ProtecaoAprovada = false }), []);

        resultado.Errors.Select(static e => e.Field).Should().Equal("documentosExigidos.A_ATESTADO", "documentosExigidos.Z_LAUDO");
    }

    private static TipoDocumentoView TipoAprovado(string codigo, string nome) =>
        new(Guid.CreateVersion7(), codigo, nome, "SAUDE", ClassificacaoDeProtecao.Sensivel, "Comprovação do atendimento especializado.", "TUTELA_DA_SAUDE", ProtecaoAprovada: true);

    private static async Task DefinirExigenciasDosTiposAsync(ProcessoSeletivo processo, FaseCronograma fase, TipoDocumentoView[] tipos)
    {
        ITipoDocumentoReader leitor = Substitute.For<ITipoDocumentoReader>();
        foreach (TipoDocumentoView tipo in tipos)
        {
            leitor.ObterPorIdAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);
        }
        RegraDeComprovacaoInformada[] regras = [.. tipos.Select(tipo => RegraDeComprovacaoDeTeste.DeDocumento(ApoioDosDocumentosExigidos.GeralNoFormularioDaExigencia(fase.Id, tipo.Id)).Construir())];
        Result definido = await DefinicaoDosDocumentosExigidos.DefinirAsync(
            processo, regras, [],
            new CadastrosDosDocumentosExigidos(leitor, Substitute.For<IFatoCandidatoReader>(), Substitute.For<Unifesspa.UniPlus.Selecao.Domain.Interfaces.IModeloDeDocumentoRepository>()),
            CancellationToken.None);
        definido.IsSuccess.Should().BeTrue(definido.Error?.Message);
    }

    private static (ProcessoSeletivo Processo, FaseCronograma Fase) ProcessoComUmaFase()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Medicina", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseDeInscricao();
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();
        ApoioDosDocumentosExigidos.DeclararOFormularioDaExigencia(processo, fase.Id);
        return (processo, fase);
    }

    private static async Task<(ProcessoSeletivo Processo, TipoDocumentoView Tipo)> ProcessoComExigenciaAsync(string[] fatosComprovados)
    {
        (ProcessoSeletivo processo, FaseCronograma fase) = ProcessoComUmaFase();
        TipoDocumentoView tipo = new(Guid.CreateVersion7(), "LAUDO_MEDICO", "Laudo médico", "SAUDE", ClassificacaoDeProtecao.Sensivel, "Comprovação do atendimento especializado.", "TUTELA_DA_SAUDE", ProtecaoAprovada: true);
        await DefinirExigenciaDoTipoAsync(processo, fase, tipo, fatosComprovados);

        return (processo, tipo);
    }

    private static async Task DefinirExigenciaDoTipoAsync(
        ProcessoSeletivo processo, FaseCronograma fase, TipoDocumentoView tipo, string[] fatosComprovados)
    {
        ITipoDocumentoReader leitor = Substitute.For<ITipoDocumentoReader>();
        leitor.ObterPorIdAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);
        ItemDocumentoExigidoDeTeste item = ApoioDosDocumentosExigidos.GeralNoFormularioDaExigencia(fase.Id, tipo.Id).ComFatosComprovados(fatosComprovados);
        Result definido = await DefinicaoDosDocumentosExigidos.DefinirAsync(
            processo, [RegraDeComprovacaoDeTeste.DeDocumento(item).Construir()], [],
            new CadastrosDosDocumentosExigidos(leitor, Substitute.For<IFatoCandidatoReader>(), Substitute.For<Unifesspa.UniPlus.Selecao.Domain.Interfaces.IModeloDeDocumentoRepository>()),
            CancellationToken.None);
        definido.IsSuccess.Should().BeTrue(definido.Error?.Message);
    }

    private static FatoCandidatoView Fato(string codigo, OrigemFato origem, bool aprovada) => new(
        Guid.CreateVersion7(), codigo, codigo, "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao), CodigoComRotulo<OrigemFato>.De(origem), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico), null, "INSCRICAO",
        origem == OrigemFato.Calculado ? $"REGRA_DE_CALCULO:{codigo}" : $"CAMPO_FORMULARIO:{codigo}", null, CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Catalogo), Ativo: true,
        ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade,
        ProtecaoAprovada: aprovada);
}
