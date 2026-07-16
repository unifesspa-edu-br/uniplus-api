namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura das 7 variantes de <see cref="PredicadoObrigatoriedade"/> avaliadas por
/// <see cref="AvaliadorConformidadeLegal"/> (Story #853, §3.1/CA-01 a CA-09) contra o
/// agregado <see cref="ProcessoSeletivo"/> real. <c>BonusObrigatorio</c> (CA-05, oitava
/// variante original) foi descartada — ADR-0114, executado por esta story:
/// <c>ConfiguracaoBonusRegional</c> é global ao processo, sem lista de modalidades.
/// </summary>
public sealed class AvaliadorConformidadeLegalTests
{
    private const string TipoProcessoAvaliado = "SiSU";

    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PS Avaliador 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

    private static ObrigatoriedadeLegal NovaRegra(string regraCodigo, PredicadoObrigatoriedade predicado) =>
        ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: ObrigatoriedadeLegal.TipoProcessoUniversal,
            categoria: CategoriaObrigatoriedade.Outros,
            regraCodigo: regraCodigo,
            predicado: predicado,
            descricaoHumana: "Regra de teste",
            baseLegal: "Lei de teste",
            vigenciaInicio: new DateOnly(2026, 1, 1)).Value!;

    [Fact(DisplayName = "CA-01: sem nenhuma regra vigente, a avaliação aprova (lista vazia)")]
    public void SemRegras_Aprova()
    {
        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            NovoProcesso(), TipoProcessoAvaliado, []);

        resultado.Regras.Should().BeEmpty();
        resultado.Avisos.Should().BeEmpty();
    }

    [Fact(DisplayName = "CA-01: devolve exatamente uma RegraAvaliada por ObrigatoriedadeLegal de entrada")]
    public void UmaRegraAvaliadaPorRegraDeEntrada()
    {
        ObrigatoriedadeLegal regra1 = NovaRegra("R1", new ConcorrenciaDuplaObrigatoria());
        ObrigatoriedadeLegal regra2 = NovaRegra("R2", new ConcorrenciaDuplaObrigatoria());

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            NovoProcesso(), TipoProcessoAvaliado, [regra1, regra2]);

        resultado.Regras.Should().HaveCount(2);
        resultado.Regras.Select(r => r.RegraCodigo).Should().BeEquivalentTo(["R1", "R2"]);
        resultado.Regras.Should().OnlyContain(r => r.TipoProcessoCodigoAvaliado == TipoProcessoAvaliado);
    }

    [Fact(DisplayName = "CA-02 (EtapaObrigatoria): aprova quando existe etapa cujo Nome bate, ordinal case-insensitive")]
    public void EtapaObrigatoria_ComEtapaCorrespondente_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas(
            [EtapaProcesso.Criar("prova objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("ETAPA", new EtapaObrigatoria("Prova Objetiva"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

        resultado.Regras.Single().Aprovada.Should().BeTrue();
        resultado.Regras.Single().Motivo.Should().BeNull("regra aprovada não carrega motivo de reprovação");
    }

    [Fact(DisplayName = "CA-02 (EtapaObrigatoria): reprova nomeando o código da etapa ausente")]
    public void EtapaObrigatoria_SemEtapaCorrespondente_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas(
            [EtapaProcesso.Criar("Redação", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("ETAPA", new EtapaObrigatoria("Prova Objetiva"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

        RegraAvaliada avaliada = resultado.Regras.Single();
        avaliada.Aprovada.Should().BeFalse();
        avaliada.Motivo.Should().Contain("Prova Objetiva",
            "CA-02 exige que a reprovação nomeie o código da etapa ausente, não só um booleano");
    }

    private static ModalidadeSelecionada NovaModalidade(string codigo, NaturezaLegalModalidade natureza) =>
        ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: codigo,
            descricao: null,
            naturezaLegal: natureza,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: natureza == NaturezaLegalModalidade.CotaReservada
                ? RegraRemanejamentoModalidade.SegueCascata
                : RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: [],
            acaoQuandoIndeferido: null,
            baseLegal: "Lei 12.711/2012").Value!;

    private static ConfiguracaoDistribuicaoVagas NovaOferta(params ModalidadeSelecionada[] modalidades) =>
        ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 0.6m,
            regraDistribuicao: ReferenciaRegra.Criar(
                "DISTRIBUICAO-PADRAO", "v1", new string('a', 64)).Value!,
            referenciaDemografica: null,
            modalidades: modalidades).Value!;

    [Fact(DisplayName = "CA-03 (ModalidadesMinimas): aprova sse TODA oferta contém todas as modalidades exigidas")]
    public void ModalidadesMinimas_TodasAsOfertasTemAModalidade_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirDistribuicaoVagas(
            [
                NovaOferta(NovaModalidade("AC", NaturezaLegalModalidade.Ampla), NovaModalidade("LB_PPI", NaturezaLegalModalidade.CotaReservada)),
                NovaOferta(NovaModalidade("AC", NaturezaLegalModalidade.Ampla), NovaModalidade("LB_PPI", NaturezaLegalModalidade.CotaReservada)),
            ],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("MODALIDADES", new ModalidadesMinimas(["LB_PPI"]));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

        resultado.Regras.Single().Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-03 (ModalidadesMinimas — contraprova obrigatória): reprova nomeando a oferta que falhou")]
    public void ModalidadesMinimas_OfertaSemModalidade_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ConfiguracaoDistribuicaoVagas ofertaCompleta = NovaOferta(
            NovaModalidade("AC", NaturezaLegalModalidade.Ampla), NovaModalidade("LB_PPI", NaturezaLegalModalidade.CotaReservada));
        ConfiguracaoDistribuicaoVagas ofertaQueFalha = NovaOferta(NovaModalidade("AC", NaturezaLegalModalidade.Ampla));
        processo.DefinirDistribuicaoVagas(
            [ofertaCompleta, ofertaQueFalha], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("MODALIDADES", new ModalidadesMinimas(["LB_PPI"]));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

        RegraAvaliada avaliada = resultado.Regras.Single();
        avaliada.Aprovada.Should().BeFalse(
            "a Lei 12.711 reserva vagas por curso e turno — uma oferta sem a modalidade é ilegal ainda que outra a tenha");
        avaliada.Motivo.Should().Contain("LB_PPI").And.Contain(ofertaQueFalha.Id.ToString(),
            "CA-03 exige que o erro nomeie A OFERTA que falhou (não só o código da modalidade ausente) — " +
            "e não a oferta completa, que não tem nada a ver com a reprovação");
    }

    [Fact(DisplayName = "CA-04 (DesempateDeveIncluir): aprova por código de catálogo, não pelo rótulo")]
    public void DesempateDeveIncluir_PorCodigoDeCatalogo_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ReferenciaRegra regraDesempate = ReferenciaRegra.Criar("DESEMPATE-IDOSO", "v1", new string('b', 64)).Value!;
        processo.DefinirCriteriosDesempate(
            [CriterioDesempate.Criar(1, regraDesempate, new ArgsDesempateIdoso(IdadeMinima: 60)).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("DESEMPATE", new DesempateDeveIncluir("DESEMPATE-IDOSO"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

        resultado.Regras.Single().Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-06 (AtendimentoDisponivel): aprova quando todas as necessidades aparecem entre os nomes ofertados (NFC, case-insensitive)")]
    public void AtendimentoDisponivel_ComNecessidadeOfertada_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar(
                [OfertaCondicao.Criar(Guid.CreateVersion7(), OfertaAtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência")],
                [],
                [OfertaTipoDeficiencia.Criar(Guid.CreateVersion7(), "auditiva")]).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("ATENDIMENTO", new AtendimentoDisponivel(["Auditiva"]));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

        resultado.Regras.Single().Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-06 (AtendimentoDisponivel): reprova SEM lançar quando OfertaAtendimento é nula")]
    public void AtendimentoDisponivel_OfertaNula_ReprovaSemLancar()
    {
        ObrigatoriedadeLegal regra = NovaRegra("ATENDIMENTO", new AtendimentoDisponivel(["Auditiva"]));

        Action act = () =>
        {
            ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(NovoProcesso(), TipoProcessoAvaliado, [regra]);
            RegraAvaliada avaliada = resultado.Regras.Single();
            avaliada.Aprovada.Should().BeFalse();
            avaliada.Motivo.Should().NotBeNullOrWhiteSpace();
        };

        act.Should().NotThrow();
    }

    [Fact(DisplayName = "CA-07 (ConcorrenciaDuplaObrigatoria): aprova quando há modalidade CotaReservada")]
    public void ConcorrenciaDupla_ComCotaReservada_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirDistribuicaoVagas(
            [NovaOferta(NovaModalidade("AC", NaturezaLegalModalidade.Ampla), NovaModalidade("LB_PPI", NaturezaLegalModalidade.CotaReservada))],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("CONCORRENCIA", new ConcorrenciaDuplaObrigatoria());

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

        resultado.Regras.Single().Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-07 (ConcorrenciaDuplaObrigatoria — contraprova obrigatória, NÃO é tautológica): REPROVA sem cota reservada")]
    public void ConcorrenciaDupla_SemCotaReservada_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirDistribuicaoVagas(
            [NovaOferta(NovaModalidade("AC", NaturezaLegalModalidade.Ampla))],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("CONCORRENCIA", new ConcorrenciaDuplaObrigatoria());

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

        RegraAvaliada avaliada = resultado.Regras.Single();
        avaliada.Aprovada.Should().BeFalse(
            "um processo só de ampla concorrência é legítimo (uma transferência pode não ter cota nenhuma), " +
            "e a regra só é cadastrada para os tipos em que a lei obriga a concorrência dupla — um teste que " +
            "desse este predicado como sempre-aprovado estaria errado");
        avaliada.Motivo.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "CA-08 (Customizado): sempre aprova, sempre emite aviso, nunca lança — inclusive com Parametros malformado")]
    public void Customizado_SempreAprovaEEmiteAviso_MesmoComParametrosMalformado()
    {
        using JsonDocument documento = JsonDocument.Parse("""{"qualquer": "coisa", "ate": [1,2,3]}""");
        ObrigatoriedadeLegal regra = NovaRegra("CUSTOM", new Customizado(documento.RootElement.Clone()));

        Action act = () =>
        {
            ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(NovoProcesso(), TipoProcessoAvaliado, [regra]);
            resultado.Regras.Single().Aprovada.Should().BeTrue();
            resultado.Avisos.Should().ContainSingle();
        };

        act.Should().NotThrow();
    }

    [Fact(DisplayName = "CA-09 (DocumentoObrigatorioParaModalidade): reprova sempre, nomeando modalidade e tipo — bloqueado pela #554, nunca aprova cegamente")]
    public void DocumentoObrigatorioParaModalidade_SempreReprova()
    {
        ObrigatoriedadeLegal regra = NovaRegra(
            "DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", "COMPROVANTE_RESIDENCIA"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(NovoProcesso(), TipoProcessoAvaliado, [regra]);

        RegraAvaliada avaliada = resultado.Regras.Single();
        avaliada.Aprovada.Should().BeFalse(
            "DocumentoExigido (#554) não existe ainda — o padrão conservador é nunca aprovar cegamente o que não pode ser verificado");
        avaliada.Motivo.Should().Contain("LB_PPI").And.Contain("COMPROVANTE_RESIDENCIA",
            "CA-09 exige que a reprovação nomeie a modalidade e o tipo de documento, não só um booleano");
    }
}
