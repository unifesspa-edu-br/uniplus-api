namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Calcula o quadro de vagas do ramo federal (art. 10/11 da Lei 12.711/2012,
/// red. Lei 14.723/2023) a partir dos insumos já congelados de uma oferta —
/// domain service puro (sem I/O, sem repositório, sem <c>TimeProvider</c> —
/// ADR-0042), consumido exclusivamente por
/// <see cref="Entities.ConfiguracaoDistribuicaoVagas.Criar"/> (ADR-0115).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Garantia mínima é floor-first, não proporcional add-only.</strong>
/// Quando a soma das demandas dos 7 grupos garantidos (art. 10 §2º — todos
/// menos <c>LI_Q</c>, que o art. 11 exclui da garantia) excede <c>VO</c>, o
/// quadro capa em duas passadas: primeiro 1 vaga a cada grupo, na ordem legal,
/// enquanto houver espaço; só depois o excedente de cada grupo, na mesma
/// ordem. Uma implementação "proporcional com mínimo, tudo de uma vez" já foi
/// tentada num protótipo e falhou: em <c>VO=3, PR=1, ppi=100%</c> ela dava
/// <c>LB_PPI=2, LB_Q=0, LB_PCD=0</c> — o 2º de PPI vencendo o piso de 1 vaga
/// de Q e PcD, o que a lei não autoriza. O correto é
/// <c>LB_PPI=1, LB_Q=1, LB_PCD=1</c>.
/// </para>
/// <para>
/// <c>LI_Q</c> nunca recebe a garantia de 1 vaga (arredonda por piso, não por
/// teto — exceção do art. 11) e, na escassez, é servido por último, com o que
/// sobrar depois das duas passadas sobre os 7 grupos garantidos.
/// </para>
/// </remarks>
public static class CalculadoraQuadroVagasLei12711
{
    public static Result<QuadroDeVagasDaLeiDeCotas> Calcular(
        int vagasOfertadasBase,
        decimal percentualDaReservaLegal,
        ReferenciaReservaDemograficaSnapshot demografica,
        IReadOnlyDictionary<string, int> retiradas,
        IReadOnlyDictionary<string, int> suplementares)
    {
        ArgumentNullException.ThrowIfNull(demografica);
        ArgumentNullException.ThrowIfNull(retiradas);
        ArgumentNullException.ThrowIfNull(suplementares);

        Result entradas = ValidarEntradas(retiradas, suplementares);
        return entradas.IsFailure
            ? Result<QuadroDeVagasDaLeiDeCotas>.Failure(entradas.Error!)
            : Montar(vagasOfertadasBase, percentualDaReservaLegal, demografica, retiradas, suplementares);
    }

    private static Result<QuadroDeVagasDaLeiDeCotas> Montar(
        int vagasOfertadasBase,
        decimal percentualDaReservaLegal,
        ReferenciaReservaDemograficaSnapshot demografica,
        IReadOnlyDictionary<string, int> retiradas,
        IReadOnlyDictionary<string, int> suplementares)
    {
        int reservaLegalNominal = (int)Math.Ceiling(vagasOfertadasBase * percentualDaReservaLegal);
        AlocacaoDasSubReservasFederais alocacao = DemandaDasSubReservasFederais
            .Calcular(reservaLegalNominal, demografica)
            .Alocar(vagasOfertadasBase);
        int ampla = vagasOfertadasBase - alocacao.Total - retiradas.Values.Sum();

        return ampla < 0
            ? Result<QuadroDeVagasDaLeiDeCotas>.Failure(AmplaConcorrenciaNegativa(ampla, vagasOfertadasBase))
            : Result<QuadroDeVagasDaLeiDeCotas>.Success(
                Compor(vagasOfertadasBase, reservaLegalNominal, alocacao, ampla, retiradas, suplementares));
    }

    private static QuadroDeVagasDaLeiDeCotas Compor(
        int vagasOfertadasBase,
        int reservaLegalNominal,
        AlocacaoDasSubReservasFederais alocacao,
        int ampla,
        IReadOnlyDictionary<string, int> retiradas,
        IReadOnlyDictionary<string, int> suplementares)
    {
        Dictionary<string, int> quadro = alocacao.PorModalidade();
        quadro[ModalidadesFederaisLei12711.Ac] = ampla;
        Acrescentar(quadro, retiradas);
        Acrescentar(quadro, suplementares);

        return new(
            [.. quadro.Select(static vaga => new VagasDaModalidade(vaga.Key, vaga.Value))],
            vagasOfertadasBase + suplementares.Values.Sum(),
            new ConferenciaDaReservaLegal(reservaLegalNominal, alocacao.Total, alocacao.VagasCortadasPorEscassez));
    }

    private static void Acrescentar(Dictionary<string, int> quadro, IReadOnlyDictionary<string, int> vagas)
    {
        foreach (KeyValuePair<string, int> vaga in vagas)
        {
            quadro[vaga.Key] = vaga.Value;
        }
    }

    private static DomainError AmplaConcorrenciaNegativa(int ampla, int vagasOfertadasBase) => new(
        "ConfiguracaoDistribuicaoVagas.QuadroAmplaConcorrenciaNegativa",
        $"A ampla concorrência resultaria negativa ({ampla}) para {vagasOfertadasBase} vagas ofertadas base: ajuste as vagas ofertadas base, o percentual da reserva legal ou as retiradas.");

    private static Result ValidarEntradas(
        IReadOnlyDictionary<string, int> retiradas, IReadOnlyDictionary<string, int> suplementares)
    {
        Result quantidadesValidas = ValidarQuantidadesNaoNegativas(retiradas, suplementares);
        return quantidadesValidas.IsFailure ? quantidadesValidas : ValidarColisaoDeChave(retiradas, suplementares);
    }

    private static Result ValidarQuantidadesNaoNegativas(
        IReadOnlyDictionary<string, int> retiradas, IReadOnlyDictionary<string, int> suplementares)
    {
        KeyValuePair<string, int>? negativa = retiradas.Concat(suplementares).FirstOrDefault(kv => kv.Value < 0);
        return negativa is { Key: not null } n
            ? Result.Failure(new DomainError(
                "ConfiguracaoDistribuicaoVagas.QuantidadeVagaNegativa",
                $"A quantidade de vagas de \"{n.Key}\" não pode ser negativa ({n.Value})."))
            : Result.Success();
    }

    /// <summary>
    /// Uma retirada ou suplemento cujo código coincide com uma sub-reserva
    /// federal ou com a ampla concorrência sobrescreveria o valor calculado
    /// sem erro, quebrando a conservação em silêncio (achado do protótipo
    /// SQL, vetor V7).
    /// </summary>
    private static Result ValidarColisaoDeChave(
        IReadOnlyDictionary<string, int> retiradas, IReadOnlyDictionary<string, int> suplementares)
    {
        string? chaveColidida = retiradas.Keys.Concat(suplementares.Keys)
            .FirstOrDefault(codigo => ModalidadesFederaisLei12711.CodigosComAc.Contains(codigo, StringComparer.Ordinal));

        return chaveColidida is not null
            ? Result.Failure(new DomainError(
                "ConfiguracaoDistribuicaoVagas.QuadroChaveColide",
                $"\"{chaveColidida}\" é uma sub-reserva federal ou a ampla concorrência — não pode ser retirada nem suplemento."))
            : Result.Success();
    }
}
