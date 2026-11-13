namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Remover uma fase do cronograma é recusado quando algo de fora dela depende dela: a âncora de
/// idade máxima de um documento exigido em outra fase, a etapa que o desempate ou a eliminação cita
/// e o formulário respondido nela. Quando mais de um vínculo existe, a recusa é a da primeira
/// dependência nessa ordem.
/// </summary>
public sealed class RemocaoDeFaseDoCronogramaPrecedenciaTests
{
    private const string FaseRemovida = "INSCRICAO";
    private const string FaseQueFica = "HABILITACAO";

    private static readonly DateTimeOffset FimDaFaseRemovida = new(2027, 3, 10, 23, 59, 0, TimeSpan.Zero);

    public enum Vinculo
    {
        AncoraDeIdadeDeOutraFase,
        EtapaDoDesempate,
        FormularioRespondido,
    }

    /// <summary>O que o agregado guarda e que uma remoção recusada não pode mexer.</summary>
    private sealed record Foto(
        IReadOnlyList<string> Fases, IReadOnlyList<Guid> Etapas, int Documentos, int Criterios, IReadOnlyList<FinalidadeFormulario> Formularios)
    {
        internal static Foto De(ProcessoSeletivo processo) => new(
            [.. processo.CronogramaFases.Select(static f => f.Codigo).Order()],
            [.. processo.Etapas.Select(static e => e.Id).Order()],
            processo.DocumentosExigidos.Count,
            processo.CriteriosDesempate.Count,
            [.. processo.Formularios.Select(static f => f.Finalidade).Order()]);
    }

    [Fact(DisplayName = "Remover a fase de que nada de fora depende é aceito, e a fase que fica continua no cronograma")]
    public void Remover_FaseSemDependentes_Aceita()
    {
        ProcessoSeletivo processo = ProcessoComFaseARemover();

        Result resultado = Remover(processo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Should().ContainSingle().Which.Codigo.Should().Be(FaseQueFica);
    }

    [Fact(DisplayName = "Remover a fase cuja janela é âncora de idade máxima de um documento de outra fase é recusado")]
    public void Remover_FaseAncoraDeIdadeDeOutraFase_Recusa()
    {
        Result resultado = RemoverSemMudarOAgregado(Vinculo.AncoraDeIdadeDeOutraFase);

        resultado.Error!.Code.Should().Be("FaseCronograma.ReferenciadaPorExigenciaViva");
        resultado.Error.Message.Should().Be(
            "A fase 'INSCRICAO' (ordem 1) está sendo removida do cronograma, mas o documento 'Comprovante de residência', exigido em outra fase, usa a janela dela como âncora de idade máxima de emissão.");
    }

    [Fact(DisplayName = "Remover a fase cuja etapa desempata a classificação é recusado")]
    public void Remover_FaseComEtapaDoDesempate_Recusa()
    {
        Result resultado = RemoverSemMudarOAgregado(Vinculo.EtapaDoDesempate);

        resultado.Error!.Code.Should().Be("FaseCronograma.EtapaDaFaseReferenciada");
        resultado.Error.Message.Should().Be(
            "A fase 'INSCRICAO' (ordem 1) está sendo removida, mas a etapa 'Prova objetiva' que acontece nela é referenciada por um critério de desempate ou por uma regra de eliminação — reconfigure a classificação antes.");
    }

    [Fact(DisplayName = "Remover a fase em que um formulário é respondido é recusado")]
    public void Remover_FaseDoFormulario_Recusa()
    {
        Result resultado = RemoverSemMudarOAgregado(Vinculo.FormularioRespondido);

        resultado.Error!.Code.Should().Be(FormularioProcessoErrorCodes.FaseReferenciadaPorFormulario);
        resultado.Error.Message.Should().Be(
            "A fase 'INSCRICAO' (ordem 1) está sendo removida, mas o formulário de INSCRICAO é respondido nela — troque a fase do formulário antes.");
    }

    [Fact(DisplayName = "A âncora de idade de outra fase vence a etapa do desempate na mesma fase removida")]
    public void Remover_AncoraEEtapa_RecusaAAncora()
    {
        Result resultado = RemoverSemMudarOAgregado(Vinculo.AncoraDeIdadeDeOutraFase, Vinculo.EtapaDoDesempate);

        resultado.Error!.Code.Should().Be("FaseCronograma.ReferenciadaPorExigenciaViva");
    }

    [Fact(DisplayName = "A etapa do desempate vence o formulário respondido na mesma fase removida")]
    public void Remover_EtapaEFormulario_RecusaAEtapa()
    {
        Result resultado = RemoverSemMudarOAgregado(Vinculo.EtapaDoDesempate, Vinculo.FormularioRespondido);

        resultado.Error!.Code.Should().Be("FaseCronograma.EtapaDaFaseReferenciada");
    }

    [Fact(DisplayName = "A âncora de idade de outra fase vence o formulário respondido na mesma fase removida")]
    public void Remover_AncoraEFormulario_RecusaAAncora()
    {
        Result resultado = RemoverSemMudarOAgregado(Vinculo.AncoraDeIdadeDeOutraFase, Vinculo.FormularioRespondido);

        resultado.Error!.Code.Should().Be("FaseCronograma.ReferenciadaPorExigenciaViva");
    }

    [Fact(DisplayName = "Com os três vínculos na fase removida, a recusa é a da âncora de idade")]
    public void Remover_ComOsTresVinculos_RecusaAAncora()
    {
        Result resultado = RemoverSemMudarOAgregado(
            Vinculo.FormularioRespondido, Vinculo.EtapaDoDesempate, Vinculo.AncoraDeIdadeDeOutraFase);

        resultado.Error!.Code.Should().Be("FaseCronograma.ReferenciadaPorExigenciaViva");
    }

    private static Result RemoverSemMudarOAgregado(params Vinculo[] vinculos)
    {
        ProcessoSeletivo processo = ProcessoComFaseARemover(vinculos);
        Foto antes = Foto.De(processo);

        Result resultado = Remover(processo);

        resultado.IsFailure.Should().BeTrue();
        Foto.De(processo).Should().BeEquivalentTo(antes, "a remoção recusada não poda nem altera nada do que a fase carrega");
        return resultado;
    }

    private static Result Remover(ProcessoSeletivo processo) =>
        processo.DefinirCronogramaFases([.. processo.CronogramaFases.Where(static f => f.Codigo == FaseQueFica)], [], PrecondicaoIfMatch.Curinga);

    private static ProcessoSeletivo ProcessoComFaseARemover(params Vinculo[] vinculos)
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma removida = Fase(1, FaseRemovida, coletaInscricao: true);
        FaseCronograma queFica = Fase(2, FaseQueFica, coletaInscricao: false);
        processo.DefinirCronogramaFases([removida, queFica], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        EtapaProcesso prova = EtapaDaFase(FaseRemovida);
        processo.DefinirEtapas([prova], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        foreach (Vinculo vinculo in vinculos)
        {
            Criar(vinculo, processo, removida, queFica, prova);
        }

        return processo;
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "PS Remoção de fase", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static void Criar(Vinculo vinculo, ProcessoSeletivo processo, FaseCronograma removida, FaseCronograma queFica, EtapaProcesso prova)
    {
        switch (vinculo)
        {
            case Vinculo.AncoraDeIdadeDeOutraFase:
                processo.DefinirDocumentosExigidos(
                    [NoExigencia.CriarFolha(DocumentoComIdadeAncoradaNaFase(queFica.Id, removida.Id), 0).Value!], PrecondicaoIfMatch.Ausente)
                    .IsSuccess.Should().BeTrue();
                break;
            case Vinculo.EtapaDoDesempate:
                processo.DefinirCriteriosDesempate([DesempatePelaNotaDaEtapa(prova.Id)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
                break;
            case Vinculo.FormularioRespondido:
                FormularioDeTeste.GarantirFormulario(processo, FinalidadeFormulario.Inscricao, PrecondicaoIfMatch.Ausente);
                break;
        }
    }

    private static FaseCronograma Fase(int ordem, string codigo, bool coletaInscricao) => FaseCronograma.Criar(
        ordem, Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital,
        agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: coletaInscricao, coletaSolicitacaoIsencao: false,
        inicio: FimDaFaseRemovida.AddDays(-10), fim: FimDaFaseRemovida,
        produtos: [], faseConcluinteCodigo: null, emiteParecerIndividual: false,
        bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    private static EtapaProcesso EtapaDaFase(string faseCodigo) => EtapaProcesso.Criar(
        "Prova objetiva", CaraterEtapa.Classificatoria,
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
        peso: 1m, ordem: 1, faseCodigo: faseCodigo).Value!;

    private static CriterioDesempate DesempatePelaNotaDaEtapa(Guid etapaId) => CriterioDesempate.Criar(
        1, ReferenciaRegra.Criar(CriterioDesempateCodigo.MaiorNotaEtapa, "v1", new string('a', 64)).Value!,
        new ArgsDesempateMaiorNotaEtapa(etapaId)).Value!;

    private static DocumentoExigido DocumentoComIdadeAncoradaNaFase(Guid exigidoNaFaseId, Guid faseAncoraId) => DocumentoExigido.Criar(
        exigidoNaFaseId,
        tipoDocumentoOrigemId: Guid.CreateVersion7(),
        tipoDocumentoCodigo: "COMPROVANTE_RESIDENCIA",
        tipoDocumentoNome: "Comprovante de residência",
        tipoDocumentoCategoria: "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
        Aplicabilidade.Geral,
        obrigatorio: true,
        consequenciaIndeferimento: null,
        condicoes: [], basesLegais: [],
        idadeMaximaEmissao: IdadeMaximaEmissao.Criar(90, UnidadeIdade.Dias, ReferenciaTipoIdadeEmissao.FimFase, null, faseAncoraId).Value!,
        formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!,
        tamanhoMaximoBytes: null,
        finalidade: FinalidadeFormulario.Habilitacao).Value!;
}
