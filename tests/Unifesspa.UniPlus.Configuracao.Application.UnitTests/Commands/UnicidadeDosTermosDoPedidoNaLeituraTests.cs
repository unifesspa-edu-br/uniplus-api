namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.CenarioDoHandlerDoModelo;
using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.RecusasParaTeste;

/// <summary>
/// O código e a ordem repetidos dos termos são conferidos sobre o pedido inteiro quando algo foi recusado, e só pelo
/// domínio quando tudo foi lido: a repetição sai uma vez, na posição que veio.
/// </summary>
public sealed class UnicidadeDosTermosDoPedidoNaLeituraTests
{
    private readonly CenarioDoHandlerDoModelo _cenario = new();

    [Fact(DisplayName = "O termo recusado na leitura e repetido de outro lido recusa as duas coisas, a repetição na posição do segundo")]
    public async Task Criar_TermoNaoLidoComCodigoDeOutro_RecusaARepeticaoNaPosicaoDoSegundo()
    {
        TermoExigidoInput primeiro = Termo("ACEITE_FICTICIO", 0).Construir();
        TermoExigidoInput naoLido = Termo("ACEITE_FICTICIO", 1).ComObrigatoriedade("INEXISTENTE").Construir();

        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(Item("CERTIFICADO", 0)) with { Termos = [primeiro, naoLido] });

        Campos(resultado).Should().Contain(("conteudo.termos[1].regras.obrigatoriedade", ModeloFormularioErrorCodes.ObrigatoriedadeInvalida));
        Campos(resultado).Should().Contain(("conteudo.termos[1].codigo", TermoFormularioErrorCodes.CodigoDuplicado));
    }

    [Fact(DisplayName = "A ordem repetida entre um termo lido e um recusado na leitura também é recusada")]
    public void Ler_TermoNaoLidoComOrdemDeOutro_RecusaAOrdemRepetida()
    {
        TermoExigidoInput primeiro = Termo("PRIMEIRO_TERMO", 3).Construir();
        TermoExigidoInput naoLido = Termo("SEGUNDO_TERMO", 3).ComObrigatoriedade("INEXISTENTE").Construir();

        DaLeitura(new ConteudoDoModeloInput(null, null, null, [primeiro, naoLido], null)).Should().Equal(
            ("termos[1].regras.obrigatoriedade", ModeloFormularioErrorCodes.ObrigatoriedadeInvalida),
            ("termos[1].ordem", TermoFormularioErrorCodes.OrdemDuplicada));
    }

    [Fact(DisplayName = "Com todos os termos lidos e repetidos, a repetição sai uma vez só, pelo domínio")]
    public async Task Criar_TodosOsTermosLidosERepetidos_RecusaUmaVez()
    {
        TermoExigidoInput primeiro = Termo("ACEITE_FICTICIO", 0).Construir();
        TermoExigidoInput repetido = Termo("ACEITE_FICTICIO", 1).Construir();

        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(Item("CERTIFICADO", 0)) with { Termos = [primeiro, repetido] });

        Campos(resultado).Where(static c => c.Codigo == TermoFormularioErrorCodes.CodigoDuplicado).Should().Equal(
            ("conteudo.termos[1].codigo", TermoFormularioErrorCodes.CodigoDuplicado));
    }

    [Fact(DisplayName = "O termo nulo é recusado como entrada nula e não conta na repetição dos demais")]
    public void Ler_TermoNuloNoMeio_NaoEntraNaConferenciaDeRepeticao()
    {
        TermoExigidoInput primeiro = Termo("PRIMEIRO_TERMO", 0).Construir();
        TermoExigidoInput segundo = Termo("SEGUNDO_TERMO", 1).Construir();

        DaLeitura(new ConteudoDoModeloInput(null, null, null, [primeiro, null!, segundo], null)).Should().Equal(
            ("termos[1]", ModeloFormularioErrorCodes.EntradaMalformada));
    }

    private static EntradaDoTermoExigidoDeTeste Termo(string codigo, int ordem) =>
        EntradaDoTermoExigidoDeTeste.Exigindo(codigo, ordem).DaVersao(Guid.NewGuid(), Guid.NewGuid());
}
