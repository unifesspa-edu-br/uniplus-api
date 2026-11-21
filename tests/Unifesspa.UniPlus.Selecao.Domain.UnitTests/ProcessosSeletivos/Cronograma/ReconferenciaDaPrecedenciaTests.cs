namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>
/// A precedência é conferida de novo na primeira publicação e sempre que o que a conferência lê do cronograma mudou desde a versão
/// publicada: as fases presentes, a ordem, a marca e o período de cada uma. O resto da fase não conta.
/// </summary>
public sealed class ReconferenciaDaPrecedenciaTests
{
    private static ModeloDeFase Modelo(string codigo, int ordem, Guid origem) =>
        new ModeloDeFase(codigo, ordem) { OrigemId = origem }.Janela(0, 5);

    private static (ModeloDeFase A, ModeloDeFase B) Fases() => (Modelo("A", 1, Guid.CreateVersion7()), Modelo("B", 2, Guid.CreateVersion7()));

    private static FaseCronograma[] Criar(params ModeloDeFase[] modelos) => [.. modelos.Select(static modelo => modelo.Criar())];

    [Fact(DisplayName = "A primeira publicação, sem versão publicada, confere sempre")]
    public void SemVersaoPublicada_Exige()
    {
        (ModeloDeFase a, ModeloDeFase b) = Fases();

        ReconferenciaDaPrecedencia.Exigida(null, Criar(a, b)).Should().BeTrue();
    }

    [Fact(DisplayName = "O cronograma igual ao publicado não exige conferir de novo")]
    public void CronogramaIgual_NaoExige()
    {
        (ModeloDeFase a, ModeloDeFase b) = Fases();

        ReconferenciaDaPrecedencia.Exigida(Criar(a, b), Criar(b, a)).Should().BeFalse();
    }

    [Fact(DisplayName = "Mudar a ordem, a marca, o início ou o fim de uma fase exige conferir de novo")]
    public void MudancaQueAConferenciaLe_Exige()
    {
        (ModeloDeFase a, ModeloDeFase b) = Fases();
        FaseCronograma[] publicadas = Criar(a, b);

        ReconferenciaDaPrecedencia.Exigida(publicadas, Criar(a, b with { Ordem = 3 })).Should().BeTrue();
        ReconferenciaDaPrecedencia.Exigida(publicadas, Criar(a, b.NoMesmoMomento())).Should().BeTrue();
        ReconferenciaDaPrecedencia.Exigida(publicadas, Criar(a, b with { Inicio = b.Inicio!.Value.AddDays(1) })).Should().BeTrue();
        ReconferenciaDaPrecedencia.Exigida(publicadas, Criar(a, b with { Fim = b.Fim!.Value.AddDays(1) })).Should().BeTrue();
    }

    [Fact(DisplayName = "Entrar ou sair uma fase do cronograma exige conferir de novo")]
    public void FaseNovaOuRetirada_Exige()
    {
        (ModeloDeFase a, ModeloDeFase b) = Fases();
        ModeloDeFase c = Modelo("C", 3, Guid.CreateVersion7());

        ReconferenciaDaPrecedencia.Exigida(Criar(a, b), Criar(a, b, c)).Should().BeTrue();
        ReconferenciaDaPrecedencia.Exigida(Criar(a, b), Criar(a)).Should().BeTrue();
    }

    [Fact(DisplayName = "Mudar só o que a conferência não lê, como o que a fase coleta, não exige conferir de novo")]
    public void MudancaQueAConferenciaNaoLe_NaoExige()
    {
        (ModeloDeFase a, ModeloDeFase b) = Fases();

        ReconferenciaDaPrecedencia.Exigida(Criar(a, b), Criar(a, b with { ColetaInscricao = true })).Should().BeFalse();
    }

    [Fact(DisplayName = "Sem o cronograma atual, a pergunta não se faz")]
    public void SemCronogramaAtual_Lanca()
    {
        Func<bool> perguntar = () => ReconferenciaDaPrecedencia.Exigida([], null!);

        perguntar.Should().Throw<ArgumentNullException>();
    }
}
