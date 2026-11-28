namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class CausasDoItemNoLeitorEnvelopeTests
{
    private static readonly DomainError PrimeiraCausa = new("primeira", "primeira mensagem");
    private static readonly DomainError SegundaCausa = new("segunda", "segunda mensagem");
    private static readonly DomainError CausaAnterior = new("anterior", "mensagem anterior");

    [Fact]
    public void PropagarCausasDoItem_SemRecusaAnterior_SituaTodasAsCausasNoItem()
    {
        LeitorEnvelope leitor = new();

        leitor.PropagarCausasDoItem<object>([new FieldError("valor", PrimeiraCausa), new FieldError(null, SegundaCausa)], "itens[0]");

        leitor.Erro.Should().Be(PrimeiraCausa);
        leitor.Falha<object>().Errors.Should().Equal(
            new FieldError("itens[0].valor", PrimeiraCausa), new FieldError("itens[0]", SegundaCausa));
    }

    [Fact]
    public void PropagarCausasDoItem_DepoisDeRecusaDeItemContido_PoeAsCausasDoItemAntes()
    {
        LeitorEnvelope leitor = new();
        leitor.Propagar<object>([new FieldError("interno", CausaAnterior)], "itens[0].contido");

        leitor.PropagarCausasDoItem<object>([new FieldError("valor", PrimeiraCausa), new FieldError(null, SegundaCausa)], "itens[0]");

        leitor.Erro.Should().Be(PrimeiraCausa);
        leitor.Falha<object>().Errors.Should().Equal(new FieldError("itens[0].valor", PrimeiraCausa),
            new FieldError("itens[0]", SegundaCausa), new FieldError("itens[0].contido.interno", CausaAnterior));
    }

    [Fact]
    public void PropagarCausasDoItem_DepoisDeRecusaSoDeFormato_PoeAsCausasDoItemAntes()
    {
        LeitorEnvelope leitor = new();
        leitor.Recusar<object>("recusa de formato");
        DomainError recusaDeFormato = leitor.Erro;

        leitor.PropagarCausasDoItem<object>([new FieldError("valor", PrimeiraCausa)], "itens[0]");

        leitor.Erro.Should().Be(PrimeiraCausa);
        leitor.Falha<object>().Errors.Should().Equal(
            new FieldError("itens[0].valor", PrimeiraCausa), new FieldError(null, recusaDeFormato));
    }
}
