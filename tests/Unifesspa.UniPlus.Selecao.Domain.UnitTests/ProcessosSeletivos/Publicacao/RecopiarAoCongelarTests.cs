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
        UnidadeResponsavelSnapshot renomeada = new(UnidadesDeTeste.Ceps.OrigemId, "NOVA", "Unidade renomeada");

        Result recopiado = processo.RecopiarAoCongelar(new CopiasDoCongelamento(
            new Dictionary<string, CopiaDoItemNoCatalogo>
            {
                [item.FatoCodigo] = new(OrigemValoresColeta.Catalogo, null, item.ClassificacaoProtecao, item.HipoteseLegal, item.FinalidadeTratamento, item.Descricao, null),
            },
            new HashSet<string>(StringComparer.Ordinal),
            new Dictionary<Guid, UnidadeResponsavelSnapshot> { [renomeada.OrigemId] = renomeada },
            new ProtecaoVigenteDasExigencias(
                new Dictionary<string, ClassificacaoDeProtecao>(),
                new Dictionary<Guid, ProtecaoDoDocumento> { [exigencia.Id] = ProtecaoDoDocumentoDeTeste.De(ClassificacaoDeProtecao.Pessoal) },
                new HashSet<string>())));

        recopiado.IsFailure.Should().BeTrue();
        processo.CronogramaFases.Single().UnidadeResponsavelSigla.Should().Be(UnidadesDeTeste.Ceps.Sigla);
        exigencia.ClassificacaoProtecao.Should().Be(ClassificacaoDeProtecao.Sensivel);
    }

    private static FaseCronograma FaseDeInscricao() => FaseCronograma.Criar(
        1, Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NoEdital,
        agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: true, coletaSolicitacaoIsencao: false,
        inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
        produtos: [ProdutoDaFase.Criar("INSCRICAO", PapelProdutoFase.Definitivo)],
        faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    private static DocumentoExigido ExigenciaSensivel(ProcessoSeletivo processo, FaseCronograma fase)
    {
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "LAUDO_MEDICO", "LAUDO_MEDICO", "SAUDE", ProtecaoDoDocumentoDeTeste.De(ClassificacaoDeProtecao.Sensivel),
            Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: null, [], [], null,
            FormatosPermitidos.Criar(true, null).Value!, null, finalidade: FinalidadeFormulario.Inscricao).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        return processo.DocumentosExigidos.Single();
    }
}
