namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDeRecusaNoCongelamento;

/// <summary>
/// A ordem das recusas ao congelar, em cada uma das três operações: o defeito que impede as conferências seguintes recusa sozinho, os que se
/// acumulam saem todos na ordem em que a conferência os julga, e a recusa não deixa nada canonicalizado, transitado
/// nem gravado. Cada defeito sozinho recusa com o código que lhe cabe, para que o par não passe por recusa nenhuma.
/// </summary>
public sealed class OrdemDasRecusasDoCongelamentoTests
{
    public static TheoryData<OperacaoQueCongela, DefeitoDoCongelamento, DefeitoDoCongelamento> Pares()
    {
        TheoryData<OperacaoQueCongela, DefeitoDoCongelamento, DefeitoDoCongelamento> pares = [];
        foreach (OperacaoQueCongela operacao in Enum.GetValues<OperacaoQueCongela>())
        {
            IReadOnlyList<DefeitoDoCongelamento> ordem = PorPrioridade(operacao);
            for (int i = 0; i < ordem.Count - 1; i++)
            {
                pares.Add(operacao, ordem[i], ordem[i + 1]);
            }

            pares.Add(operacao, ordem[0], ordem[^1]);
        }

        return pares;
    }

    public static TheoryData<OperacaoQueCongela, DefeitoDoCongelamento> Defeitos()
    {
        TheoryData<OperacaoQueCongela, DefeitoDoCongelamento> defeitos = [];
        foreach (OperacaoQueCongela operacao in Enum.GetValues<OperacaoQueCongela>())
        {
            foreach (DefeitoDoCongelamento defeito in PorPrioridade(operacao))
            {
                defeitos.Add(operacao, defeito);
            }
        }

        return defeitos;
    }

    [Theory(DisplayName = "Com dois defeitos, o que recusa sozinho impede os demais, e os outros se acumulam na ordem da conferência, sem nada gravado")]
    [MemberData(nameof(Pares))]
    public async Task DoisDefeitos_RecusaSozinhoOuAcumulaNaOrdem(OperacaoQueCongela operacao, DefeitoDoCongelamento primeiro, DefeitoDoCongelamento segundo)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao).Com(segundo).Com(primeiro);

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        AfirmarOsCodigos(resposta, primeiro, segundo);
        eventos.Should().BeEmpty();
        await cenario.Servicos.NadaFoiGravadoAsync();
        cenario.AfirmarQueNaoTransitou();
    }

    [Theory(DisplayName = "Cada defeito sozinho recusa com todas as suas causas, na ordem, e nada é gravado")]
    [MemberData(nameof(Defeitos))]
    public async Task UmDefeito_RecusaComOSeuCodigo(OperacaoQueCongela operacao, DefeitoDoCongelamento defeito)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao).Com(defeito);

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        resposta.Errors.Select(static e => e.Error.Code).Should().Equal(CausasDo(defeito));
        eventos.Should().BeEmpty();
        await cenario.Servicos.NadaFoiGravadoAsync();
        cenario.AfirmarQueNaoTransitou();
    }

    private static void AfirmarOsCodigos(Result resposta, DefeitoDoCongelamento primeiro, DefeitoDoCongelamento segundo)
    {
        string[] codigos = [.. resposta.Errors.Select(static e => e.Error.Code)];
        codigos.First().Should().Be(CodigoDa(primeiro));
        if (RecusaSozinho(primeiro) || ConformidadeLegalSemEdital(primeiro, segundo))
        {
            codigos.Should().NotContain(CodigoDa(segundo));
        }
        else
        {
            codigos.Should().ContainInOrder(CodigoDa(primeiro), CodigoDa(segundo));
        }
    }

    // A conformidade legal lê o dia de referência do edital: com os anexos recusados, o edital não se resolve e ela não roda.
    private static bool ConformidadeLegalSemEdital(DefeitoDoCongelamento primeiro, DefeitoDoCongelamento segundo) =>
        primeiro == DefeitoDoCongelamento.Anexos && segundo == DefeitoDoCongelamento.ConformidadeLegal;
}
