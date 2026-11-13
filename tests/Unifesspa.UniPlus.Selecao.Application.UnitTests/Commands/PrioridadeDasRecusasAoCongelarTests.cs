namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDeRecusaNoCongelamento;

/// <summary>
/// Quando dois defeitos coincidem, vence a recusa do portão de maior prioridade, em cada uma das três operações
/// que congelam, e a recusa não deixa nada canonicalizado, transitado nem gravado. Cada defeito sozinho recusa
/// com o código que lhe cabe, para que o par não passe por recusa nenhuma.
/// </summary>
public sealed class PrioridadeDasRecusasAoCongelarTests
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

    [Theory(DisplayName = "Com dois defeitos, a recusa do portão de maior prioridade vence e nada é gravado")]
    [MemberData(nameof(Pares))]
    public async Task DoisDefeitos_VenceOMaisPrioritario(OperacaoQueCongela operacao, DefeitoDoCongelamento maior, DefeitoDoCongelamento menor)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao).Com(menor).Com(maior);

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        resposta.Error!.Code.Should().Be(CodigoDa(maior));
        eventos.Should().BeEmpty();
        await cenario.Servicos.NadaFoiGravadoAsync();
        cenario.AfirmarQueNaoTransitou();
    }

    [Theory(DisplayName = "Cada defeito sozinho recusa com o seu código e nada é gravado")]
    [MemberData(nameof(Defeitos))]
    public async Task UmDefeito_RecusaComOSeuCodigo(OperacaoQueCongela operacao, DefeitoDoCongelamento defeito)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao).Com(defeito);

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        resposta.Error!.Code.Should().Be(CodigoDa(defeito));
        eventos.Should().BeEmpty();
        await cenario.Servicos.NadaFoiGravadoAsync();
        cenario.AfirmarQueNaoTransitou();
    }
}
