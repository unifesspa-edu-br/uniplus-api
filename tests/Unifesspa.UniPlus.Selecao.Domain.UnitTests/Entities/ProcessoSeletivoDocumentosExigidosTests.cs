namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;

/// <summary>
/// Cobertura de <see cref="ProcessoSeletivo.DefinirDocumentosExigidos"/> (Story #554,
/// PR #895): substituição integral e pertencimento da fase ao cronograma do MESMO
/// processo (§2 da issue #547).
/// </summary>
public sealed class ProcessoSeletivoDocumentosExigidosTests
{
    private static FaseCronograma FaseQueResponde(bool inscricao, bool isencao) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: inscricao, ColetaSolicitacaoIsencao: isencao),
        new DecisoesDeCronogramaDaFase(1, Inicio: null, Fim: null, FaseConcluinteCodigo: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static Result DefinirNaFase(FaseCronograma fase, FinalidadeFormulario? finalidade)
    {
        ProcessoSeletivo processo = ProcessoComFases(fase);
        return DefinirFolhas(processo, Exigencia(fase.Id, finalidade: finalidade));
    }

    [Fact(DisplayName = "O documento de um formulário que a fase não responde é recusado")]
    public void DefinirDocumentosExigidos_FinalidadeQueAFaseNaoResponde_Recusa()
    {
        Result resultado = DefinirNaFase(FaseQueResponde(inscricao: true, isencao: false), FinalidadeFormulario.IsencaoTaxa);

        resultado.Error!.Code.Should().Be(DocumentoExigidoErrorCodes.FaseIncoerenteComFinalidade);
    }

    [Theory(DisplayName = "Na fase que responde a inscrição e a isenção, o documento pertence a qualquer dos dois formulários")]
    [InlineData(FinalidadeFormulario.Inscricao)]
    [InlineData(FinalidadeFormulario.IsencaoTaxa)]
    public void DefinirDocumentosExigidos_FaseCompartilhada_AceitaAsDuasFinalidades(FinalidadeFormulario finalidade)
    {
        Result resultado = DefinirNaFase(FaseQueResponde(inscricao: true, isencao: true), finalidade);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Theory(DisplayName = "O formulário do documento é obrigatório só na fase que responde algum formulário")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void DefinirDocumentosExigidos_SemFinalidade_SoNaFaseSemFormulario(bool faseRespondeFormulario, bool aceita)
    {
        Result resultado = DefinirNaFase(FaseQueResponde(inscricao: faseRespondeFormulario, isencao: false), finalidade: null);

        resultado.IsSuccess.Should().Be(aceita);
        if (!aceita)
        {
            resultado.Error!.Code.Should().Be(DocumentoExigidoErrorCodes.FinalidadeObrigatoria);
        }
    }

    [Fact(DisplayName = "Fase que não pertence ao cronograma do processo é recusada")]
    public void DefinirDocumentosExigidos_FaseDeOutroProcesso_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = DefinirFolhas(processo, Exigencia(Guid.CreateVersion7()));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.FaseNaoPertenceAoProcesso");
    }

    [Fact(DisplayName = "Fase viva do cronograma do próprio processo é aceita")]
    public void DefinirDocumentosExigidos_FaseDoProprioProcesso_Aceita()
    {
        FaseCronograma fase = Fase(1, "INSCRICAO");
        ProcessoSeletivo processo = ProcessoComFases(fase);

        DocumentoExigido exigencia = Exigencia(fase.Id);
        Result resultado = DefinirFolhas(processo, exigencia);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().ContainSingle(d => d.Id == exigencia.Id);
        exigencia.ProcessoSeletivoId.Should().Be(processo.Id);
    }

    [Fact(DisplayName = "Coleção vazia é um estado válido — nenhuma exigência configurada")]
    public void DefinirDocumentosExigidos_ListaVazia_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirDocumentosExigidos([]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().BeEmpty();
    }

    [Fact(DisplayName = "PUT idempotente: reenviar o mesmo payload substitui a coleção por inteiro, sem duplicar")]
    public void DefinirDocumentosExigidos_ReenviarMesmoPayload_NaoDuplica()
    {
        FaseCronograma fase = Fase(1, "INSCRICAO");
        ProcessoSeletivo processo = ProcessoComFases(fase);
        ExigirFolhas(processo, Exigencia(fase.Id));

        DocumentoExigido segundaChamada = Exigencia(fase.Id);
        Result resultado = DefinirFolhas(processo, segundaChamada);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().HaveCount(1);
        processo.DocumentosExigidos.Should().ContainSingle(d => d.Id == segundaChamada.Id);
    }
}
