namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Text.RegularExpressions;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent.Syntax.Elements.Types;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using BarramentoDeComandos = Unifesspa.UniPlus.Application.Abstractions.Messaging.ICommandBus;
using BarramentoDoWolverine = Wolverine.ICommandBus;

/// <summary>
/// O handler roda na transação que o Wolverine abre para ele, e o que ele grava e o que emite ao outbox confirmam juntos
/// (ADR-0004, ADR-0170). Despachar outro comando pelo <c>ICommandBus.Send</c>, ou invocar outra mensagem pelo
/// <c>IMessageBus.InvokeAsync</c>, abriria outra transação: a primeira gravação confirmaria sem a segunda, ou a segunda
/// sem a primeira. A consequência de negócio em outra operação é evento com consumidor próprio, e a gravação em outro
/// módulo é a porta de escrita dele, chamada na mesma transação. O <c>InvokeAsync</c> é declarado no <c>ICommandBus</c> do
/// Wolverine, que o <c>IMessageBus</c> herda; a regra proíbe depender dos dois <c>ICommandBus</c>, porque o ArchUnitNET não
/// casa a chamada de um método genérico (<c>Send&lt;T&gt;</c>) com o membro declarado, e um handler só os usa para despachar.
/// Os tipos aninhados (máquinas de estado e lambdas) contam como o tipo que os contém.
/// </summary>
public sealed class HandlerNaoAbreOutraTransacaoTests
{
    [Fact(DisplayName = "Nenhum handler dos módulos despacha comando pelo ICommandBus nem invoca mensagem pelo IMessageBus")]
    public void Produto_HandlerNaoAbreOutraTransacao()
    {
        Handlers(ModulosDoMonolito.Raizes).GetObjects(ArquiteturaDasTravas.Produto).Should()
            .NotBeEmpty("sem handler descoberto, a conferência passaria vazia");
        Types().That().DependOnAny(Barramentos()).GetObjects(ArquiteturaDasTravas.Produto).Should()
            .NotBeEmpty("sem dependente do barramento descoberto, a regra não enxergaria nenhum");

        Violacoes(ArquiteturaDasTravas.Produto, ModulosDoMonolito.Raizes).Should().BeEmpty(
            "o handler não abre outra transação: o efeito em outra operação é evento, e a gravação em outro módulo é a porta de escrita");
    }

    [Fact(DisplayName = "Fitness_HandlerQueAbreOutraTransacao_Falha")]
    public void Fitness_HandlerQueAbreOutraTransacao_Falha()
    {
        List<string> reprovados = Violacoes(ArquiteturaDasTravas.Fixtures, ModulosDoMonolito.RaizesDosFixtures);

        reprovados.Should().Contain(static v => v.Contains("DespachaOutroComandoHandler", StringComparison.Ordinal));
        reprovados.Should().Contain(static v => v.Contains("InvocaOutraMensagemHandler", StringComparison.Ordinal));
        reprovados.Should().NotContain(static v => v.Contains("PublicaEventoNoOutboxHandler", StringComparison.Ordinal));
    }

    private static GivenTypesConjunction Handlers(string[] raizes) =>
        Types().That().HaveFullNameMatching($@"^({string.Join('|', raizes.Select(Regex.Escape))})\..*Handler(\+|$)");

    private static GivenTypesConjunction Barramentos() =>
        Types().That().HaveFullName(typeof(BarramentoDeComandos).FullName!).Or().HaveFullName(typeof(BarramentoDoWolverine).FullName!);

    private static List<string> Violacoes(Architecture arquitetura, string[] raizes) =>
        ArquiteturaDasTravas.Reprovacoes(Handlers(raizes).Should().NotDependOnAnyTypesThat().Are(Barramentos()), arquitetura);
}
