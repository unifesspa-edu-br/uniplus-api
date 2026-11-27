namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>A retificação cita a fase pelo nome que a versão publicada guardou, e não pelo nome do rascunho que a propõe.</summary>
public sealed class NomePublicadoNaRetificacaoTests
{
    private static readonly Guid OrigemDaFase = Guid.CreateVersion7();

    private static readonly CitacaoDaFase NomePublicado = new("Prova de redação");

    [Fact(DisplayName = "A troca de unidade cita o nome da versão publicada, com a ordem, e não o nome do rascunho")]
    public void Conferir_TrocaDeUnidade_CitaONomeDaVersaoPublicada()
    {
        FaseCronograma rascunho = FaseDoRascunho("Redação revisada", UnidadesDeTeste.Cga);
        FasesIniciadas iniciadas = new(
            [KeyValuePair.Create<Guid, Guid?>(OrigemDaFase, UnidadesDeTeste.Ceps.OrigemId)], [KeyValuePair.Create(OrigemDaFase, NomePublicado)]);

        FieldError recusa = ConferenciaDasFasesIniciadas.Conferir(iniciadas, [rascunho], []).Should().ContainSingle().Subject;

        recusa.Error.Message.Should().Be("A fase 'Prova de redação' (ordem 2) já começou, e a unidade responsável por ela não muda.");
    }

    [Fact(DisplayName = "A remoção da fase iniciada cita o nome da versão publicada, e não o código da fase")]
    public void Conferir_RemocaoDaFase_CitaONomeDaVersaoPublicada()
    {
        FasesIniciadas iniciadas = new(
            [KeyValuePair.Create<Guid, Guid?>(OrigemDaFase, UnidadesDeTeste.Ceps.OrigemId)], [KeyValuePair.Create(OrigemDaFase, NomePublicado)]);

        FieldError recusa = ConferenciaDasFasesIniciadas.Conferir(iniciadas, [], []).Should().ContainSingle().Subject;

        recusa.Error.Message.Should().Be("A fase 'Prova de redação' já começou, e ela não sai do cronograma.");
    }

    [Fact(DisplayName = "A citação publicada é a da versão vigente quando ela traz a fase")]
    public void CitacaoPublicada_FaseNaVersaoVigente_DevolveANomeDaVersao()
    {
        FasesIniciadas iniciadas = new([], [KeyValuePair.Create(OrigemDaFase, NomePublicado)]);

        iniciadas.CitacaoPublicada(FaseDoRascunho("Redação revisada", UnidadesDeTeste.Ceps)).Should().Be(NomePublicado);
    }

    [Fact(DisplayName = "A citação publicada, sem a fase na versão vigente, é a da própria fase")]
    public void CitacaoPublicada_FaseForaDaVersaoVigente_DevolveACitacaoDaPropriaFase()
    {
        FaseCronograma rascunho = FaseDoRascunho("Redação revisada", UnidadesDeTeste.Ceps);

        FasesIniciadas.Nenhuma.CitacaoPublicada(rascunho).Should().Be(new CitacaoDaFase("Redação revisada"));
    }

    private static FaseCronograma FaseDoRascunho(string nome, UnidadeResponsavelSnapshot unidade) =>
        FaseCronograma.Criar(ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.Da(OrigemDaFase, "REDACAO", OrigemDataFase.NoEdital, false, false, false, false).ComNome(nome),
            new DecisoesDeCronogramaDaFase(
                2, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: unidade),
            new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;
}
