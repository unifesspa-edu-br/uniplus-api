namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioProcessoSeletivoCronograma;

/// <summary>
/// A gravação do cronograma acumula as causas de todas as famílias (ADR-0125), na ordem: a estrutura, a mudança, a precedência,
/// o ciclo recursal e a janela das etapas. Só a mudança, que casa as fases por origem, deixa de ser avaliada com origem repetida, e
/// isso nunca vira exceção: a precedência casa por código e é avaliada sempre.
/// </summary>
public sealed class RecusasAcumuladasNaGravacaoDoCronogramaTests
{
    private const string OrdemRepetida = "ProcessoSeletivo.OrdemFaseDuplicada";
    private const string OrigemRepetidaCodigo = "ProcessoSeletivo.FaseCanonicaDuplicada";
    private const string PrecedenciaViolada = "ProcessoSeletivo.PrecedenciaFaseViolada";
    private const string ConclusaoNaoDeclarada = "ProcessoSeletivo.ConclusaoNaoDeclarada";
    private const string FaseDoFormulario = FormularioProcessoErrorCodes.FaseReferenciadaPorFormulario;

    private static string[] Codigos(Result resultado) => [.. resultado.Errors.Select(static causa => causa.Error.Code)];

    private static readonly ArestaPrecedencia[] ArestasDasDuasViolacoes = [new("C", "D", false), new("E", "F", false)];

    // A e B repetem a ordem; as demais vêm de DuasArestasViolaveis.
    private static FaseCronograma[] FasesDaOrdemRepetida() => [new ModeloDeFase("A", 1).Criar(), new ModeloDeFase("B", 1).Criar()];

    // C vem depois de D na ordem, e E depois de F: cada par viola a aresta que o cadastro declara entre eles.
    private static FaseCronograma[] DuasArestasViolaveis() =>
        [new ModeloDeFase("C", 4).Criar(), new ModeloDeFase("D", 3).Criar(), new ModeloDeFase("E", 6).Criar(), new ModeloDeFase("F", 5).Criar()];

    private static ModeloDeFase AbreOCiclo(string codigo, int ordem) => new ModeloDeFase(codigo, ordem).Publicando(ModeloDeFase.Preliminar("RESULTADO"));

    [Fact(DisplayName = "Estrutura e ciclo recursal inválidos no mesmo cronograma: as duas famílias saem, a estrutura primeiro")]
    public void Definir_EstruturaECicloInvalidos_AcumulaAsDuasFamilias()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma[] fases = [AbreOCiclo("A", 1).Criar(), new ModeloDeFase("B", 1).Criar()];

        Result resultado = processo.DefinirCronogramaFases(fases, [], PrecondicaoIfMatch.Ausente);

        resultado.Errors.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("fases[1].ordem", OrdemRepetida),
            ("fases[0].faseConcluinteCodigo", ConclusaoNaoDeclarada));
        processo.CronogramaFases.Should().BeEmpty();
    }

    [Fact(DisplayName = "Origem repetida no cronograma novo, sobre um cronograma já gravado: a estrutura recusa e a mudança não lança")]
    public void Definir_OrigemRepetidaSobreCronogramaGravado_RecusaSemLancar()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCronogramaFases([new ModeloDeFase("A", 1).Criar()], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        Guid origem = Guid.CreateVersion7();
        FaseCronograma[] fases = [new ModeloDeFase("B", 1) { OrigemId = origem }.Criar(), new ModeloDeFase("C", 2) { OrigemId = origem }.Criar()];

        Result? resultado = null;
        Action definir = () => resultado = processo.DefinirCronogramaFases(fases, [], PrecondicaoIfMatch.Curinga);

        definir.Should().NotThrow();
        Codigos(resultado!).Should().Equal(OrigemRepetidaCodigo);
    }

    [Fact(DisplayName = "A ordem repetida é recusa do conjunto: a precedência das outras fases é avaliada junto")]
    public void Definir_OrdemRepetida_AcumulaComAsPrecedencias()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma[] fases = [.. FasesDaOrdemRepetida(), .. DuasArestasViolaveis()];

        Result resultado = processo.DefinirCronogramaFases(fases, ArestasDasDuasViolacoes, PrecondicaoIfMatch.Ausente);

        Codigos(resultado).Should().Equal(OrdemRepetida, PrecedenciaViolada, PrecedenciaViolada);
    }

    [Fact(DisplayName = "Origem repetida suspende só a mudança, mesmo com uma remoção que seria recusada: a precedência é avaliada")]
    public void Definir_OrigemRepetida_SuspendeAMudancaEMantemAPrecedencia()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Guid origem = Guid.CreateVersion7();
        FaseCronograma[] fases =
        [
            new ModeloDeFase("A", 1) { OrigemId = origem }.Criar(), new ModeloDeFase("B", 2) { OrigemId = origem }.Criar(), .. DuasArestasViolaveis(),
        ];

        Result? resultado = null;
        Action definir = () => resultado = processo.DefinirCronogramaFases(fases, ArestasDasDuasViolacoes, PrecondicaoIfMatch.Curinga);

        definir.Should().NotThrow();
        Codigos(resultado!).Should().Equal(OrigemRepetidaCodigo, PrecedenciaViolada, PrecedenciaViolada);
    }

    [Fact(DisplayName = "Código repetido com origens distintas avalia a mudança, suspende a aresta ambígua e recusa a outra")]
    public void Definir_CodigoRepetidoComOrigensDistintas_AvaliaAMudancaESuspendeSoAArestaAmbigua()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        FaseCronograma[] fases = [new ModeloDeFase("A", 1).Criar(), new ModeloDeFase("A", 2).Criar(), .. DuasArestasViolaveis()];
        ArestaPrecedencia[] arestas = [new("C", "D", false), new("D", "A", false)];

        Result resultado = processo.DefinirCronogramaFases(fases, arestas, PrecondicaoIfMatch.Curinga);

        Codigos(resultado).Should().Equal(OrigemRepetidaCodigo, FaseDoFormulario, PrecedenciaViolada);
        resultado.Errors[^1].Field.Should().Be("fases[3].ordem");
    }

    [Fact(DisplayName = "Com a estrutura válida, a precedência e o ciclo recursal acumulam, a precedência primeiro")]
    public void Definir_PrecedenciaECicloInvalidos_AcumulaAsDuasFamilias()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma[] fases = [AbreOCiclo("HOMOLOGACAO", 1).Criar(), new ModeloDeFase("INSCRICAO", 2).Criar()];

        Result resultado = processo.DefinirCronogramaFases(fases, [new ArestaPrecedencia("INSCRICAO", "HOMOLOGACAO", false)], PrecondicaoIfMatch.Ausente);

        Codigos(resultado).Should().Equal(PrecedenciaViolada, ConclusaoNaoDeclarada);
    }

    [Fact(DisplayName = "O cronograma sem causa é gravado")]
    public void Definir_SemCausas_Grava()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirCronogramaFases([new ModeloDeFase("A", 1).Criar(), new ModeloDeFase("B", 2).Criar()], [], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue();
        processo.CronogramaFases.Should().HaveCount(2);
    }
}
