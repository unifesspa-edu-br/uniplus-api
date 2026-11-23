namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>O tipo que exige correção independente só é congelado com a regra congelada que põe ao menos dois corretores em campo.</summary>
public sealed class PendenciasDaCorrecaoIndependenteTests
{
    private static readonly ReferenciaRegra Regra = ReferenciaRegra.De(RegraConsolidacaoCodigo.CorrecaoIndependente, "v1", new string('a', 64));

    private static EtapaProcesso Etapa(bool tipoExige, string nome = "Redação", ReferenciaRegra? regra = null, int ordem = 1)
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(
                Nome: nome, Carater: CaraterEtapa.Classificatoria,
                TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "REDACAO", "Redação", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false, exigeCorrecaoIndependente: tipoExige).Value!,
                Ordem: ordem, FaseCodigo: "RESULTADO_FINAL"),
            new DeclaracaoDaAvaliacaoDaEtapa(
                Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: 0.5m, FormaDeAvaliacao: FormaDeAvaliacao.Humana,
                RegraConsolidacao: regra ?? Regra, EscalaDeAtendimento: null)).Value!;

        return etapa;
    }

    private static IReadOnlyList<FieldError> Causas(CorretoresDasEtapas corretores, params EtapaProcesso[] etapas)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirEtapas(etapas).IsSuccess.Should().BeTrue();

        return PendenciasDaCorrecaoIndependente.CausasDaCorrecaoIndependente(processo.Declaracao(), corretores);
    }

    private static DomainError? Pendencia(EtapaProcesso etapa, CorretoresDasEtapas corretores) => Causas(corretores, etapa) is { Count: > 0 } causas ? causas[0].Error : null;

    private static CorretoresDasEtapas Com(CorretoresDaRegra corretores) => new(new Dictionary<ReferenciaRegra, CorretoresDaRegra> { [Regra] = corretores });

    [Fact(DisplayName = "Tipo que exige correção independente recusa a regra congelada de um só corretor")]
    public void UmCorretor_Recusa()
    {
        DomainError? pendencia = Pendencia(Etapa(tipoExige: true), Com(CorretoresDaRegra.SemNovoCorretor(1)));

        pendencia!.Code.Should().Be(EtapaProcessoErrorCodes.CorrecaoIndependenteComMenosDeDoisCorretores);
        pendencia.Message.Should().Contain("Redação");
    }

    [Fact(DisplayName = "Duas etapas recusadas dão duas causas, na ordem das etapas, e a regra não lida não esconde a de um só corretor")]
    public void DuasEtapasRecusadas_DuasCausas()
    {
        ReferenciaRegra outraRegra = ReferenciaRegra.De(RegraConsolidacaoCodigo.CorrecaoIndependente, "v2", new string('b', 64));

        IReadOnlyList<FieldError> causas = Causas(
            Com(CorretoresDaRegra.SemNovoCorretor(1)),
            Etapa(tipoExige: true, nome: "Redação A", ordem: 1),
            Etapa(tipoExige: true, nome: "Redação B", regra: outraRegra, ordem: 2));

        causas.Select(static causa => causa.Error.Code).Should().Equal(
            EtapaProcessoErrorCodes.CorrecaoIndependenteComMenosDeDoisCorretores, EtapaProcessoErrorCodes.RegraDeConsolidacaoNaoResolvida);
        causas[0].Error.Message.Should().Contain("Redação A");
        causas[1].Error.Message.Should().Contain("Redação B");
    }

    [Fact(DisplayName = "O novo corretor não conta como corretor distinto: um distinto com novo corretor é recusado")]
    public void UmDistintoComNovoCorretor_Recusa() =>
        Pendencia(Etapa(tipoExige: true), Com(new CorretoresDaRegra(Distintos: 1, EmCampo: 2)))!
            .Code.Should().Be(EtapaProcessoErrorCodes.CorrecaoIndependenteComMenosDeDoisCorretores);

    [Theory(DisplayName = "Tipo que exige correção independente aceita a regra congelada de dois ou mais corretores")]
    [InlineData(2)]
    [InlineData(3)]
    public void DoisOuMaisCorretores_Aceita(int corretores) =>
        Pendencia(Etapa(tipoExige: true), Com(CorretoresDaRegra.SemNovoCorretor(corretores))).Should().BeNull();

    [Fact(DisplayName = "Regra cujo número de corretores não foi lido é recusada, sem presumir")]
    public void RegraNaoLida_Recusa() =>
        Pendencia(Etapa(tipoExige: true), new CorretoresDasEtapas(new Dictionary<ReferenciaRegra, CorretoresDaRegra>()))!
            .Code.Should().Be(EtapaProcessoErrorCodes.RegraDeConsolidacaoNaoResolvida);

    [Fact(DisplayName = "Tipo sem o sinalizador não exige o número de corretores")]
    public void TipoSemSinalizador_Aceita() =>
        Pendencia(Etapa(tipoExige: false), Com(CorretoresDaRegra.SemNovoCorretor(1))).Should().BeNull();

    [Theory(DisplayName = "A conferência recusa o tipo que exige correção independente com um só corretor e aceita com dois")]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void Conferencia_RecusaSoComMenosDeDoisCorretores(int corretores, bool recusa)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirEtapas([Etapa(tipoExige: true)]).IsSuccess.Should().BeTrue();

        Result<ConfiguracaoAptaACongelar> conferida = ConfiguracaoAptaACongelar.Conferir(
            processo.Id, processo.Declaracao(), CadastrosDeTeste.Com(corretores: Com(CorretoresDaRegra.SemNovoCorretor(corretores)), motivosEscolhidos: MotivosDeDecisaoDoProcessoDeTeste.Cobrindo(processo)));

        conferida.Errors.Any(static causa => causa.Error.Code == EtapaProcessoErrorCodes.CorrecaoIndependenteComMenosDeDoisCorretores).Should().Be(recusa);
    }
}
