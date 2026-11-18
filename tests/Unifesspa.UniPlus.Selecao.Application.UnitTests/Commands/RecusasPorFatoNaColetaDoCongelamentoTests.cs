namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A recusa dos fatos coletados é por fato: cada fato que o catálogo deixou de aceitar e cada campo sem valor para
/// oferecer é uma causa própria, na ordem do formulário, e os valores se resolvem sobre os fatos que ainda se coletam.
/// </summary>
public sealed class RecusasPorFatoNaColetaDoCongelamentoTests
{
    private const string NaoMaisDeclarado = "ProcessoSeletivo.FatoColetadoNaoMaisDeclarado";
    private const string SemValoresOfertados = "ProcessoSeletivo.FatoColetadoSemValoresOfertados";

    [Fact(DisplayName = "Um fato reclassificado e um campo de seleção sem valores recusam juntos, nessa ordem")]
    public async Task FatoReclassificadoECampoSemValores_RecusamJuntos()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Recusas por fato");
        processo.DefinirItens([ItemDeSelecao("MODALIDADE", 0), ItemDeSelecao("TIPO_DEFICIENCIA", 1)])
            .IsSuccess.Should().BeTrue();
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo)
            .ComCatalogoDeFatos(Catalogo(ModalidadeDerivada(), TipoDeDeficienciaSemTipoVivo()));

        (Result resposta, IEnumerable<object> eventos) = await servicos.PublicarAsync(processo.Id);

        resposta.Errors.Select(static e => e.Error.Code).Should().Equal(NaoMaisDeclarado, SemValoresOfertados);
        resposta.Errors[0].Error.Message.Should().Contain("'MODALIDADE'");
        resposta.Errors[1].Error.Message.Should().Contain("'TIPO_DEFICIENCIA'");
        eventos.Should().BeEmpty();
        await servicos.NadaFoiGravadoAsync();
    }

    [Fact(DisplayName = "Dois fatos reclassificados recusam duas vezes, um por fato, na ordem do formulário")]
    public void DoisFatosReclassificados_RecusamUmaVezCada()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens([ItemDeSelecao("MODALIDADE", 0), ItemDeSelecao("TIPO_DEFICIENCIA", 1)], finalidade: FinalidadeFormulario.Habilitacao)
            .IsSuccess.Should().BeTrue();
        Dictionary<string, FatoCandidatoView> catalogo = PorCodigo(ModalidadeDerivada(), TipoDeDeficienciaSemTipoVivo() with { Origem = "DERIVADO" });

        Result resultado = ConferenciaDeColetabilidadeDeFatos.Conferir(processo, catalogo);

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal(NaoMaisDeclarado, NaoMaisDeclarado);
        resultado.Errors.Select(static e => e.Error.Message).Should().SatisfyRespectively(
            primeira => primeira.Should().Contain("'MODALIDADE'"),
            segunda => segunda.Should().Contain("'TIPO_DEFICIENCIA'"));
    }

    [Fact(DisplayName = "Dois campos de seleção sem valores recusam duas vezes, um por campo, e nenhum deles aparece nos valores")]
    public void DoisCamposSemValores_RecusamUmaVezCada()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens([ItemDeSelecao("TIPO_DEFICIENCIA", 0), ItemDeSelecao("OUTRO_TIPO", 1)], finalidade: FinalidadeFormulario.Habilitacao)
            .IsSuccess.Should().BeTrue();
        Dictionary<string, FatoCandidatoView> catalogo = PorCodigo(
            TipoDeDeficienciaSemTipoVivo(), TipoDeDeficienciaSemTipoVivo() with { Codigo = "OUTRO_TIPO", Binding = "CAMPO_FORMULARIO:OUTRO_TIPO" });

        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> resultado =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, catalogo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(static e => e.Error.Code).Should().Equal(SemValoresOfertados, SemValoresOfertados);
        resultado.Errors.Select(static e => e.Error.Message).Should().SatisfyRespectively(
            primeira => primeira.Should().Contain("'TIPO_DEFICIENCIA'"),
            segunda => segunda.Should().Contain("'OUTRO_TIPO'"));
    }

    [Fact(DisplayName = "O campo do fato excluído da resolução não é resolvido, nem lança por faltar no catálogo")]
    public void FatoExcluido_NaoEResolvido()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens([ItemDeSelecao("MODALIDADE", 0)], finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();

        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> resultado =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, new Dictionary<string, FatoCandidatoView>(), new HashSet<string> { "MODALIDADE" });

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeEmpty();
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "PS Recusas por fato", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static Dictionary<string, FatoCandidatoView> PorCodigo(params FatoCandidatoView[] fatos) =>
        fatos.ToDictionary(static f => f.Codigo, StringComparer.Ordinal);

    private static IFatoCandidatoReader Catalogo(params FatoCandidatoView[] fatos)
    {
        IFatoCandidatoReader catalogo = Substitute.For<IFatoCandidatoReader>();
        catalogo.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)fatos));

        return catalogo;
    }

    private static FatoCandidatoView ModalidadeDerivada() => new(
        Id: Guid.CreateVersion7(), Codigo: "MODALIDADE", Nome: "Modalidade de concorrência", Descricao: "Descrição do fato.",
        Dominio: "CATEGORICO", Origem: "DERIVADO", Cardinalidade: "MULTIVALORADO", ValoresDominio: null, PontoResolucao: "INSCRICAO",
        Binding: "REGRA_DERIVACAO:MODALIDADE", ValoresDominioDeclarados: null, FonteValores: "MODALIDADE", Ativo: true,
        ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal,
        FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

    private static FatoCandidatoView TipoDeDeficienciaSemTipoVivo() => new(
        Id: Guid.CreateVersion7(), Codigo: "TIPO_DEFICIENCIA", Nome: "Tipo de deficiência", Descricao: "Descrição do fato.",
        Dominio: "CATEGORICO", Origem: "DECLARADO", Cardinalidade: "MULTIVALORADO", ValoresDominio: null, PontoResolucao: "INSCRICAO",
        Binding: "CAMPO_FORMULARIO:TIPO_DEFICIENCIA",
        ValoresDominioDeclarados: [new FatoValorDominioViewItem("TEA", "Transtorno do espectro autista", 0, Ativo: false)],
        FonteValores: "TIPOS_DEFICIENCIA", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal,
        FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

    private static FatoColetado ItemDeSelecao(string codigo, int ordem) =>
        FatoColetado.Criar(
            codigo, ordem, codigo, TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL",
            hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade,
            descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
}
