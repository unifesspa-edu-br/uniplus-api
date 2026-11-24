namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Publicacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class RecopiarAoCongelarTests
{
    [Fact(DisplayName = "A recusa dos itens deixa intactas as unidades das fases e a proteção das exigências")]
    public void CatalogoIncoerente_RecusaSemRefazerUnidadesNemProtecao()
    {
        FaseCronograma inscricao = FaseDeInscricao();
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: inscricao);
        DocumentoExigido exigencia = ExigenciaSensivel(processo, inscricao);
        FatoColetado item = processo.FatosColetados.First(static f => f.Formato is not null);
        UnidadeResponsavelSnapshot renomeada = new(UnidadesDeTeste.Ceps.OrigemId, "NOVO_CODIGO", "NOVA", "Unidade renomeada");

        Result recopiado = processo.RecopiarAoCongelar(CopiasComCatalogoIncoerente(item, renomeada, exigencia));

        recopiado.IsFailure.Should().BeTrue();
        processo.CronogramaFases.Single().UnidadeResponsavel!.Sigla.Should().Be(UnidadesDeTeste.Ceps.Sigla);
        exigencia.ClassificacaoProtecao.Should().Be(ClassificacaoDeProtecao.Sensivel);
    }

    [Fact]
    public void RecopiarUnidades_CodigoAlteradoNaMesmaOrigem_CopiaNovoCodigo()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: FaseDeInscricao());
        UnidadeResponsavelSnapshot unidade = new(UnidadesDeTeste.Ceps.OrigemId, "CODIGO_NOVO", "CEPS", "Centro de Processos Seletivos");

        processo.RecopiarUnidadesDasFases(new Dictionary<Guid, UnidadeResponsavelSnapshot> { [unidade.OrigemId] = unidade });

        processo.CronogramaFases.Single().UnidadeResponsavel!.Codigo.Should().Be("CODIGO_NOVO");
    }

    [Fact]
    public void RecopiarAoCongelar_ProtecaoAlterada_PreservaChaveDaExigencia()
    {
        FaseCronograma fase = FaseDeInscricao();
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: fase);
        DocumentoExigido exigencia = ExigenciaSensivel(processo, fase);
        ChaveLocal chave = exigencia.Chave;
        ProtecaoVigenteDasExigencias protecao = new(new Dictionary<string, ClassificacaoDeProtecao>(),
            new Dictionary<Guid, ProtecaoDoDocumento> { [exigencia.Id] = ProtecaoDoDocumentoDeTeste.Pessoal }, new HashSet<string>(), new HashSet<Guid>());

        Result resultado = processo.RecopiarAoCongelar(new(new Dictionary<string, CopiaDoItemNoCatalogo>(),
            new HashSet<string>(), new Dictionary<Guid, UnidadeResponsavelSnapshot>(), protecao));

        resultado.IsSuccess.Should().BeTrue();
        exigencia.ClassificacaoProtecao.Should().Be(ClassificacaoDeProtecao.Pessoal);
        exigencia.Chave.Should().Be(chave);
    }

    private static CopiasDoCongelamento CopiasComCatalogoIncoerente(FatoColetado item, UnidadeResponsavelSnapshot renomeada, DocumentoExigido exigencia) => new(
        new Dictionary<string, CopiaDoItemNoCatalogo>
        {
            [item.FatoCodigo] = new(OrigemValoresColeta.Catalogo, null, item.ClassificacaoProtecao, item.HipoteseLegal, item.FinalidadeTratamento, item.Descricao, null),
        },
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<Guid, UnidadeResponsavelSnapshot> { [renomeada.OrigemId] = renomeada },
        new ProtecaoVigenteDasExigencias(
            new Dictionary<string, ClassificacaoDeProtecao>(),
            new Dictionary<Guid, ProtecaoDoDocumento> { [exigencia.Id] = ProtecaoDoDocumentoDeTeste.De(ClassificacaoDeProtecao.Pessoal) },
            new HashSet<string>(),
            new HashSet<Guid>()));

    private static FaseCronograma FaseDeInscricao() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: true, ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(
            1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("INSCRICAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static DocumentoExigido ExigenciaSensivel(ProcessoSeletivo processo, FaseCronograma fase)
    {
        DocumentoExigido exigencia = new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "LAUDO_MEDICO", "LAUDO_MEDICO", "SAUDE", ProtecaoDoDocumentoDeTeste.De(ClassificacaoDeProtecao.Sensivel)))
            .ComMomento(new MomentoDaExigencia(fase.Id, null, FinalidadeFormulario.Inscricao))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []))
            .ComIndeferimento(new IndeferimentoDoDocumento(true, null, []))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, null, null))
            .ComFatos(null)
            .Criar().Value!;
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(exigencia, 0).Value!])
            .IsSuccess.Should().BeTrue();
        return processo.DocumentosExigidos.Single();
    }
}
