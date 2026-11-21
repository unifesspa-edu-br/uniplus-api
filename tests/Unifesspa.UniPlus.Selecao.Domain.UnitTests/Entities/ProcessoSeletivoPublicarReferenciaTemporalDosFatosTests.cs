namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaPublicacaoDoProcessoSeletivo;

public sealed class ProcessoSeletivoPublicarReferenciaTemporalDosFatosTests
{
    [Fact(DisplayName = "No rascunho em montagem, a faixa etária citada sem referência declarada não tem data, sem recusar")]
    public void DataReferenciaFatosEmMontagem_SemReferencia_NulaSemLancar()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaCondicionalComGatilhoPorFaixaEtaria(faseId), 0).Value!])
            .IsSuccess.Should().BeTrue();
        TimeZoneInfo fuso = TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId);

        ReferenciaTemporalDosFatos.DataReferenciaFatosEmMontagem(processo.Declaracao(), fuso).Should().BeNull("o rascunho ainda não declarou a referência, e a simulação não pode cair por isso");

        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.DataEspecifica, new DateOnly(2026, 1, 31), null).Value!)
            .IsSuccess.Should().BeTrue();
        ReferenciaTemporalDosFatos.DataReferenciaFatosEmMontagem(processo.Declaracao(), fuso).Should().Be(new DateOnly(2026, 1, 31));
    }

    [Fact(DisplayName = "DefinirReferenciaTemporalFatos: fase de outro processo é recusada")]
    public void DefinirReferenciaTemporalFatos_FaseDeOutroProcesso_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        ReferenciaTemporalFatos referencia = ReferenciaTemporalFatos.Criar(
            ReferenciaTipo.FimFase, null, Guid.CreateVersion7()).Value!;

        Result resultado = processo.DefinirReferenciaTemporalFatos(referencia);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ReferenciaTemporalFatos.FaseNaoPertenceAoProcesso");
    }

    [Fact(DisplayName = "DefinirReferenciaTemporalFatos: fase do próprio cronograma é aceita (contraprova)")]
    public void DefinirReferenciaTemporalFatos_FaseDoProprioProcesso_Aceita()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        ReferenciaTemporalFatos referencia = ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, faseId).Value!;

        Result resultado = processo.DefinirReferenciaTemporalFatos(referencia);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.ReferenciaTemporalFatos.Should().Be(referencia);
    }

    [Fact(DisplayName = "DefinirReferenciaTemporalFatos: redefinir para null é aceito — presença é 0..1 (contraprova)")]
    public void DefinirReferenciaTemporalFatos_Nulo_Aceita()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, faseId).Value!)
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirReferenciaTemporalFatos(null);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.ReferenciaTemporalFatos.Should().BeNull();
    }

    [Fact(DisplayName = "B-03: gatilho por FAIXA_ETARIA sem referência configurada é bloqueado — a guarda B-01 (issue #547) que antes mascarava esta checagem foi removida na PR #903 (issue #548)")]
    public void Publicar_GatilhoPorFaixaEtariaSemReferencia_BloqueadoPorB03()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaCondicionalComGatilhoPorFaixaEtaria(faseId), 0).Value!])
            .IsSuccess.Should().BeTrue();
        // Nenhuma ReferenciaTemporalFatos configurada — com a guarda B-01 removida, a
        // checagem B-03 (issue #892) agora é alcançada e recusa nomeadamente.

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ReferenciaTemporalFatosAusente");
    }

    [Fact(DisplayName = "Publicar sem nenhum gatilho por FAIXA_ETARIA não é afetado pela pendência B-03 (contraprova)")]
    public void Publicar_SemGatilhoPorFaixaEtaria_NaoBloqueiaPorReferenciaTemporalFatos()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, faseId).Value!)
            .IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "B-03: FIM_FASE apontando para fase sem Fim definido bloqueia publicação")]
    public void Publicar_FimFaseSemExtremoDefinido_BloqueadoPorB03()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseSemExtremoId = AcrescentarFaseSemExtremo(processo);
        Guid faseComEtapaId = processo.CronogramaFases.Single(f => f.Ordem == 1).Id;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaCondicionalComGatilhoPorFaixaEtaria(faseComEtapaId), 0).Value!])
            .IsSuccess.Should().BeTrue();
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, faseSemExtremoId).Value!)
            .IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ReferenciaTemporalFatosExtremoAusente");
    }

    /// <summary>
    /// O único caminho que resta até o B-03 depois da issue #1350, e o motivo de ele continuar
    /// existindo.
    /// </summary>
    /// <remarks>
    /// Os outros dois caminhos foram fechados por gates que rodam antes: com inscrição própria,
    /// a ausência de fase de coleta é recusada por <c>InscricaoPropriaSemFaseDeColeta</c>; com
    /// fase de coleta presente, a falta de janela é recusada por
    /// <c>FaseQueColetaInscricaoSemJanela</c>. Sobra a importação externa, que legitimamente não
    /// tem fase de coleta e ainda assim aceita declarar FIM_INSCRICAO —
    /// <c>DefinirReferenciaTemporalFatos</c> não guarda contra isso.
    /// </remarks>
    [Fact(DisplayName = "B-03: FIM_INSCRICAO sem nenhuma fase que colete inscrição bloqueia publicação")]
    public void Publicar_FimInscricaoSemFaseComFimDefinido_BloqueadoPorB03()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(OrigemCandidatos.ImportacaoExterna);
        Guid faseColetaId = DefinirCronogramaSemFaseDeColeta(processo);
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaCondicionalComGatilhoPorFaixaEtaria(faseColetaId, finalidade: null), 0).Value!])
            .IsSuccess.Should().BeTrue();
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimInscricao, null, null).Value!)
            .IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ReferenciaTemporalFatosFimInscricaoIndisponivel");
    }

    [Fact(DisplayName = "B-03: FIM_INSCRICAO com mais de uma fase de coleta resolve pela menor Ordem, não pela ordem de inserção — achado de revisão da PR #903")]
    public void ResolverDataReferenciaFatos_FimInscricaoComDuasFasesDeColeta_ResolvePelaMenorOrdem()
    {
        ProcessoSeletivo processo = ProcessoComDuasFasesDeColeta();

        ReferenciaTemporalDosFatos.ResolverDataReferenciaFatos(processo.Declaracao(), TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId)).Should().Be(new DateOnly(2026, 1, 15),
            "a fase de Ordem 1 (Fim 2026-01-15) precisa vencer a de Ordem 2 (Fim 2026-01-25) por Ordem — se a " +
            "escolha dependesse da ordem de inserção em _cronogramaFases, a reidratação (sempre ordenada por " +
            "Ordem) poderia resolver outra data e quebrar o round-trip");
    }

    [Fact(DisplayName = "B-03: ResolverDataReferenciaFatos converte para o fuso institucional — um Fim de madrugada em UTC cai no dia anterior local")]
    public void ResolverDataReferenciaFatos_FimDeMadrugadaEmUtc_ResolveODiaAnteriorNoFusoInstitucional()
    {
        ProcessoSeletivo processo = ProcessoComReferenciaAoFimDeMadrugada();

        ReferenciaTemporalDosFatos.ResolverDataReferenciaFatos(processo.Declaracao(), TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId)).Should().Be(new DateOnly(2026, 3, 1),
            "o Fim (2026-03-02T01:30:00Z) convertido para o fuso institucional (America/Belem, UTC-3, sem DST) é 2026-03-01T22:30 — o dia LOCAL, não o dia UTC");
    }

    private static DocumentoExigido ExigenciaCondicionalComGatilhoPorFaixaEtaria(
        Guid exigidoNaFaseId, FinalidadeFormulario? finalidade = FinalidadeFormulario.Inscricao)
    {
        CondicaoGatilho condicao = CondicaoGatilho.Criar(
            0, "FAIXA_ETARIA", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!;
        return DocumentoExigido.Criar(
            exigidoNaFaseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "DECLARACAO_MAIORIDADE",
            tipoDocumentoNome: "Declaração de maioridade",
            tipoDocumentoCategoria: "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            aplicabilidade: Aplicabilidade.Condicional,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            condicoes: [condicao], basesLegais: [BaseLegalResolvidaQualquer()], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null, finalidade: finalidade).Value!;
    }

    /// <summary>Acrescenta ao cronograma conforme uma segunda fase NA_EXECUCAO (sem janela) — a única forma de ter uma fase sem Fim, já que NO_EDITAL exige janela (CA-07).</summary>
    private static Guid AcrescentarFaseSemExtremo(ProcessoSeletivo processo)
    {
        FaseCronograma semExtremo = FaseCronograma.Criar(
            new OrigemCanonicaDaFase(OrigemId: Guid.CreateVersion7(), Codigo: "HOMOLOGACAO_INSCRICOES", OrigemData: OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(Ordem: 2, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;
        FormularioDeTeste.TrocarCronograma(processo, [FaseConforme(), semExtremo]).IsSuccess.Should().BeTrue();
        return semExtremo.Id;
    }

    /// <summary>
    /// Cronograma SEM nenhuma fase que colete inscrição, com o resultado numa fase NO_EDITAL.
    /// </summary>
    /// <remarks>
    /// Era, antes da issue #1350, um cronograma com a coleta numa fase NA_EXECUCAO sem janela — "a
    /// única forma de ter uma fase ColetaInscricao=true sem Fim". Aquele estado deixou de ser
    /// publicável: a fase que coleta inscrição passou a exigir janela, e a recusa dela roda no
    /// gate do cronograma, ANTES do B-03. O teste continuaria verde recusando pelo motivo errado.
    /// <para>
    /// O caminho que sobrou para alcançar o B-03 é este: nenhuma fase de coleta, com FIM_INSCRICAO
    /// declarado assim mesmo — <c>DefinirReferenciaTemporalFatos</c> não guarda contra isso, e é
    /// por isso que o gate segue sendo a única barreira. Sem ele,
    /// <c>ResolverDataReferenciaFatos</c> lança e o 422 vira 500.
    /// </para>
    /// </remarks>
    private static Guid DefinirCronogramaSemFaseDeColeta(ProcessoSeletivo processo)
    {
        FaseCronograma resultado = FaseCronograma.Criar(
            new OrigemCanonicaDaFase(OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(Ordem: 2, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologaComRecurso(),
            bancasRequeridas: HomologacaoDasInscricoesDeTeste.BancasDaFaseQueHomologaComRecurso()).Value!;
        FormularioDeTeste.TrocarCronograma(processo, [resultado]).IsSuccess.Should().BeTrue();
        ContagemDePrazosDeTeste.DeclararConvencao(processo);
        return resultado.Id;
    }

    private static FaseCronograma ColetaNaPrimeiraQuinzena()
    {
        FaseCronograma coletaOrdem1 = FaseCronograma.Criar(
            new OrigemCanonicaDaFase(OrigemId: Guid.CreateVersion7(), Codigo: "INSCRICAO_AC", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: true, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;
        return coletaOrdem1;
    }


    private static FaseCronograma ColetaNaSegundaQuinzena()
    {
        FaseCronograma coletaOrdem2 = FaseCronograma.Criar(
            new OrigemCanonicaDaFase(OrigemId: Guid.CreateVersion7(), Codigo: "INSCRICAO_REMANEJAMENTO", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(Ordem: 2, Inicio: new DateTimeOffset(2026, 1, 20, 12, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 25, 12, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(),
            bancasRequeridas: [BancasDeTeste.DaHomologacaoDasInscricoesDaFase()]).Value!;
        return coletaOrdem2;
    }


    private static FaseCronograma FaseComFimDeMadrugadaEmUtc()
    {
        FaseCronograma faseComVirada = FaseCronograma.Criar(
            new OrigemCanonicaDaFase(OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_PRELIMINAR", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(Ordem: 2, Inicio: new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 3, 2, 1, 30, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;
        return faseComVirada;
    }


    private static ProcessoSeletivo ProcessoComDuasFasesDeColeta()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        // Insere a fase de Ordem 2 ANTES da de Ordem 1 — _cronogramaFases preserva a ordem
        // de ENTRADA de DefinirCronogramaFases, diferente do envelope (sempre ordenado por
        // Ordem). Se a resolução dependesse dessa ordem de inserção, pegaria a fase errada.
        FormularioDeTeste.TrocarCronograma(processo, [ColetaNaSegundaQuinzena(), ColetaNaPrimeiraQuinzena()]).IsSuccess.Should().BeTrue();
        Guid faseOrdem1Id = processo.CronogramaFases.Single(f => f.Ordem == 1).Id;

        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaCondicionalComGatilhoPorFaixaEtaria(faseOrdem1Id), 0).Value!])
            .IsSuccess.Should().BeTrue();
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimInscricao, null, null).Value!)
            .IsSuccess.Should().BeTrue();
        return processo;
    }


    private static ProcessoSeletivo ProcessoComReferenciaAoFimDeMadrugada()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        FaseCronograma faseComVirada = FaseComFimDeMadrugadaEmUtc();
        // O cronograma precede a exigência que referencia suas fases.
        FormularioDeTeste.TrocarCronograma(processo, [FaseConforme(), faseComVirada]).IsSuccess.Should().BeTrue();
        Guid faseComEtapaId = processo.CronogramaFases.Single(f => f.Ordem == 1).Id;
        Guid faseComViradaId = processo.CronogramaFases.Single(f => f.Ordem == 2).Id;

        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaCondicionalComGatilhoPorFaixaEtaria(faseComEtapaId), 0).Value!])
            .IsSuccess.Should().BeTrue();
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, faseComViradaId).Value!)
            .IsSuccess.Should().BeTrue();
        return processo;
    }

}
