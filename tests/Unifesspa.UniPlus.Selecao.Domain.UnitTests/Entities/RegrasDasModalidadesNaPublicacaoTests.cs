namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As regras de aplicabilidade congeladas na publicação de um processo com inscrição própria: cada
/// uma cita só o que a inscrição pergunta ou o processo deriva, lida como vale na oferta, e tem a
/// forma que a natureza da modalidade pede no cadastro.
/// </summary>
public sealed class RegrasDasModalidadesNaPublicacaoTests
{
    private const string ItemFatoNaoColetado = "modalidade_regra_cita_fato_nao_coletado";
    private const string ItemNatureza = "modalidade_regra_incoerente_com_a_natureza";
    private const string ItemRemanejamento = "modalidade_remanejamento_incoerente_com_a_natureza";

    [Fact(DisplayName = "Sem cota da lei ofertada, a reserva PcD que recusa a opção pelas cotas publica com as perguntas de PcD na inscrição")]
    public void SemCota_ReservaPcd_Publica()
    {
        ProcessoSeletivo processo = ProcessoComAmplaEReservaPcd();
        processo.DefinirItens([Booleano("PCD", 0), Booleano("CONCORRER_PCD", 1)]).IsSuccess.Should().BeTrue();

        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo).Should().BeNull(
            "sem cota da lei ofertada, ninguém optou pelas cotas, e a regra cita só o que a inscrição pergunta");
    }

    [Fact(DisplayName = "A regra que cita fato coletado só na isenção é recusada: a modalidade é decidida pela inscrição")]
    public void FatoSoNaIsencao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoComAmplaEReservaPcd();
        processo.DefinirItens([Booleano("PCD", 0)]).IsSuccess.Should().BeTrue();
        processo.DefinirItens([Booleano("CONCORRER_PCD", 0)], finalidade: FinalidadeFormulario.IsencaoTaxa).IsSuccess.Should().BeTrue();

        Item(processo, ItemFatoNaoColetado).Ok.Should().BeFalse("CONCORRER_PCD é perguntado só na isenção");
    }

    [Fact(DisplayName = "A regra que cita fato coletado na inscrição é aceita")]
    public void FatoNaInscricao_Aceita()
    {
        ProcessoSeletivo processo = ProcessoComAmplaEReservaPcd();
        processo.DefinirItens([Booleano("PCD", 0), Booleano("CONCORRER_PCD", 1)]).IsSuccess.Should().BeTrue();

        Item(processo, ItemFatoNaoColetado).Ok.Should().BeTrue();
    }

    [Theory(DisplayName = "A regra que cita a opção pelas cotas depende da opção pelas vagas de escola pública perguntada na inscrição")]
    [InlineData(FinalidadeFormulario.Inscricao, true)]
    [InlineData(FinalidadeFormulario.Habilitacao, false)]
    public void OpcaoPelasCotas_DependeDaInscricao(FinalidadeFormulario finalidade, bool esperadoOk)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Ofertar(processo,
            Modalidade("AC", NaturezaLegalModalidade.Ampla, PredicadoDnf.SemClausula),
            Modalidade("LI_EP", NaturezaLegalModalidade.CotaReservada, Exige((OpcaoPelasCotasDaLei.Fato, true))));
        processo.DefinirRegrasDerivacao(
            [ConfiguracaoDerivacaoFato.Criar(OrigemEscolar.FatoEgresso, [RegraDerivacaoConfigurada.Criar(0, null, null).Value!]).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirItens([Booleano(OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, 0)], finalidade: finalidade).IsSuccess.Should().BeTrue();

        Item(processo, ItemFatoNaoColetado).Ok.Should().Be(esperadoOk,
            "a opção pelas cotas é derivada da opção pelas vagas de escola pública, que a inscrição tem de perguntar");
    }

    [Theory(DisplayName = "Sem o egresso configurado, a cota da lei depende das perguntas da origem escolar na inscrição, pela regra do sistema")]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void SemEgressoConfigurado_DependeDaOrigemEscolar(bool perguntaAOrigemEscolar, bool esperadoOk)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Ofertar(processo,
            Modalidade("AC", NaturezaLegalModalidade.Ampla, PredicadoDnf.SemClausula),
            Modalidade("LI_EP", NaturezaLegalModalidade.CotaReservada, Exige((OpcaoPelasCotasDaLei.Fato, true))));
        List<FatoColetado> itens = [Booleano(OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, 0)];
        if (perguntaAOrigemEscolar)
        {
            itens.Add(Booleano(OrigemEscolar.FatoOndeCursou, 1));
            itens.Add(Booleano(OrigemEscolar.FatoFormaDeConclusao, 2));
        }

        processo.DefinirItens(itens, finalidade: FinalidadeFormulario.Inscricao).IsSuccess.Should().BeTrue();

        processo.RegrasDerivacao.Should().BeEmpty("o processo não aplicou modelo nem configurou derivação");
        Item(processo, ItemFatoNaoColetado).Ok.Should().Be(esperadoOk,
            "o egresso, de que a opção pelas cotas depende, vem pela regra do sistema, que cita a origem escolar");
    }

    [Theory(DisplayName = "A regra que cita agregado sobre grupo é aceita quando um grupo da inscrição pergunta o campo de membro, e a recusa nomeia esse campo")]
    [InlineData(FinalidadeFormulario.Inscricao, true)]
    [InlineData(FinalidadeFormulario.Habilitacao, false)]
    public void AgregadoDeGrupo_DependeDoCampoDeMembroNaInscricao(FinalidadeFormulario finalidadeDoGrupo, bool esperadoAceito)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Ofertar(processo,
            Modalidade("AC", NaturezaLegalModalidade.Ampla, PredicadoDnf.SemClausula),
            Modalidade("X", NaturezaLegalModalidade.AcaoAfirmativa, Exige((OpcaoPelasCotasDaLei.Fato, false), (AgregadoDoGrupo, true))));
        processo.DefinirItens([], finalidade: FinalidadeFormulario.Inscricao).IsSuccess.Should().BeTrue();
        processo.DefinirItens([], finalidade: finalidadeDoGrupo, grupos: [GrupoComOCampoDeMembro(finalidadeDoGrupo)]).IsSuccess.Should().BeTrue();

        DomainError? recusa = processo.PendenciaPreCanonicalizacao(ComOAgregado);

        if (esperadoAceito)
        {
            recusa.Should().BeNull("o grupo da inscrição pergunta o campo de que o agregado é calculado");
        }
        else
        {
            recusa!.Code.Should().Be(ProcessoSeletivoErrorCodes.RegraDeModalidadeCitaFatoNaoColetado);
            recusa.Message.Should().Contain("modalidade X", "a recusa nomeia a modalidade");
            recusa.Message.Should().Contain($"'{CampoDeMembro}'", "a recusa nomeia o campo que a inscrição não pergunta");
            recusa.Message.Should().NotContain(AgregadoDoGrupo, "o agregado não é campo que o operador ponha no formulário");
        }

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario, ComOAgregado, TiposDeAtoUnicosDeTeste.Nenhum)
            .Single(i => i.Codigo == ItemFatoNaoColetado).Ok.Should().Be(esperadoAceito);
    }

    [Fact(DisplayName = "No processo que importa os candidatos, o egresso suprido pela oferta não exige as perguntas da origem escolar")]
    public void ImportacaoExterna_EgressoSuprido_NaoExigeAOrigemEscolar()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS SiSU", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Ofertar(processo,
            Modalidade("AC", NaturezaLegalModalidade.Ampla, PredicadoDnf.SemClausula),
            Modalidade("LI_EP", NaturezaLegalModalidade.CotaReservada, Exige((OpcaoPelasCotasDaLei.Fato, true))));

        processo.DerivacoesEfetivas().Should().Contain(d => d.CodigoFato == OrigemEscolar.FatoEgresso);
        Item(processo, "derivacao_fatos_citados_inexistentes").Ok.Should().BeTrue(
            "a regra do sistema que a oferta supre não é configuração do processo, e o candidato chega com a modalidade");
    }

    [Fact(DisplayName = "A cota da lei copiada sem a opção pelas cotas — a linha anterior à cópia da regra — recusa a publicação")]
    public void CotaSemOpcao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Ofertar(processo, Modalidade("AC", NaturezaLegalModalidade.Ampla, PredicadoDnf.SemClausula), Modalidade("LI_EP", NaturezaLegalModalidade.CotaReservada, PredicadoDnf.SemClausula));

        Item(processo, ItemNatureza).Ok.Should().BeFalse();
        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo)!.Code
            .Should().Be(ProcessoSeletivoErrorCodes.RegraIncoerenteComANatureza);
    }

    [Theory(DisplayName = "A regra congelada tem a forma que a natureza pede: a cota exige a opção, e a ação afirmativa a recusa")]
    [InlineData(NaturezaLegalModalidade.CotaReservada, true, true)]
    [InlineData(NaturezaLegalModalidade.CotaReservada, false, false)]
    [InlineData(NaturezaLegalModalidade.AcaoAfirmativa, false, true)]
    [InlineData(NaturezaLegalModalidade.AcaoAfirmativa, true, false)]
    public void Natureza_DecideAForma(NaturezaLegalModalidade natureza, bool optou, bool esperadoOk)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Ofertar(processo, Modalidade("AC", NaturezaLegalModalidade.Ampla, PredicadoDnf.SemClausula), Modalidade("X", natureza, Exige((OpcaoPelasCotasDaLei.Fato, optou))));

        Item(processo, ItemNatureza).Ok.Should().Be(esperadoOk);
    }

    [Theory(DisplayName = "O remanejamento copiado é o que a natureza pede: a ampla não remaneja, e a ação afirmativa remaneja para um destino ou para o par")]
    [InlineData(NaturezaLegalModalidade.AcaoAfirmativa, RegraRemanejamentoModalidade.DestinoUnico, true)]
    [InlineData(NaturezaLegalModalidade.AcaoAfirmativa, RegraRemanejamentoModalidade.Nenhuma, false)]
    [InlineData(NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.DestinoUnico, false)]
    public void Remanejamento_DecidePelaNatureza(NaturezaLegalModalidade natureza, RegraRemanejamentoModalidade remanejamento, bool esperadoOk)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        ModalidadeSelecionada copia = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "X", null, natureza, ComposicaoVagasModalidade.SuplementarAoTotal, null, remanejamento,
            remanejamento == RegraRemanejamentoModalidade.DestinoUnico ? "AC" : null, null, null,
            RegrasDeAplicabilidadeDeTeste.DaNatureza(natureza), null, "base legal", quantidadeDeclarada: 10).Value!;
        Ofertar(processo, Modalidade("AC", NaturezaLegalModalidade.Ampla, PredicadoDnf.SemClausula), copia);

        Item(processo, ItemRemanejamento).Ok.Should().Be(esperadoOk);
    }

    [Fact(DisplayName = "A ação afirmativa copiada sem remanejamento recusa a publicação com erro nomeado")]
    public void AcaoAfirmativaSemRemanejamento_RecusaAPublicacao()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        ModalidadeSelecionada semRemanejamento = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), ModalidadesFederaisLei12711.AcPcd, null, NaturezaLegalModalidade.AcaoAfirmativa,
            ComposicaoVagasModalidade.SuplementarAoTotal, null, RegraRemanejamentoModalidade.Nenhuma, null, null, null,
            RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.AcaoAfirmativa), null, "base legal", quantidadeDeclarada: 10).Value!;
        Ofertar(processo, Modalidade("AC", NaturezaLegalModalidade.Ampla, PredicadoDnf.SemClausula), semRemanejamento);

        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo)!.Code
            .Should().Be(ProcessoSeletivoErrorCodes.RemanejamentoIncoerenteComANatureza);
    }

    /// <summary>A ampla e a reserva PcD de ampla concorrência, sem cota da lei, no processo conforme com inscrição própria.</summary>
    private static ProcessoSeletivo ProcessoComAmplaEReservaPcd()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Ofertar(
            processo,
            Modalidade("AC", NaturezaLegalModalidade.Ampla, PredicadoDnf.SemClausula),
            Modalidade(ModalidadesFederaisLei12711.AcPcd, NaturezaLegalModalidade.AcaoAfirmativa,
                Exige((OpcaoPelasCotasDaLei.Fato, false), ("PCD", true), ("CONCORRER_PCD", true))));
        return processo;
    }

    private const string AgregadoDoGrupo = "ALGUM_INTEGRANTE_SOLICITA";
    private const string CampoDeMembro = "MEMBRO_PEDE";

    /// <summary>O catálogo com o agregado sobre grupo cujo fato de membro é <see cref="CampoDeMembro"/>.</summary>
    private static readonly FatosDeModalidade ComOAgregado = new(
        FatosDeModalidadeDeTeste.DoCatalogo.Codigos,
        new Dictionary<string, string>(StringComparer.Ordinal) { [AgregadoDoGrupo] = CampoDeMembro });

    private static GrupoColetado GrupoComOCampoDeMembro(FinalidadeFormulario finalidade) =>
        GrupoColetado.Criar("COMPOSICAO_FAMILIAR", 2, FormularioDeTeste.Secao, "Composição familiar", 0, 5, null, Obrigatoriedade.Sempre,
            [FatoColetado.Criar(CampoDeMembro, 0, "Membro pede", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL").Value!],
            finalidade).Value!;

    private static ItemConformidade Item(ProcessoSeletivo processo, string codigo) =>
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Single(i => i.Codigo == codigo);

    private static void Ofertar(ProcessoSeletivo processo, params ModalidadeSelecionada[] modalidades) =>
        processo.DefinirDistribuicaoVagas(
            [ConfiguracaoDistribuicaoVagas.Criar(
                Guid.CreateVersion7(), 10 * modalidades.Length, 1m,
                ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)).Value!,
                null, null, modalidades, ofertaCurso: OfertaCursoDeTeste.Medicina).Value!],
            FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

    private static ModalidadeSelecionada Modalidade(string codigo, NaturezaLegalModalidade natureza, PredicadoDnf regra) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null, natureza,
            natureza == NaturezaLegalModalidade.Ampla ? ComposicaoVagasModalidade.ResidualDoVo : ComposicaoVagasModalidade.SuplementarAoTotal, null,
            natureza switch
            {
                NaturezaLegalModalidade.CotaReservada => RegraRemanejamentoModalidade.SegueCascata,
                NaturezaLegalModalidade.AcaoAfirmativa => RegraRemanejamentoModalidade.DestinoUnico,
                _ => RegraRemanejamentoModalidade.Nenhuma,
            },
            natureza == NaturezaLegalModalidade.AcaoAfirmativa ? "AC" : null, null, null, regra, null, "base legal", quantidadeDeclarada: 10).Value!;

    private static FatoColetado Booleano(string fato, int ordem) =>
        FatoColetado.Criar(fato, ordem, fato, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "SENSIVEL").Value!;

    private static PredicadoDnf Exige(params (string Fato, bool Valor)[] atomos) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([.. atomos.Select(static a => (0, CondicaoDnf.Criar(
            a.Fato, Operador.Igual, JsonSerializer.SerializeToElement(a.Valor)).Value!))]).Value!;
}
