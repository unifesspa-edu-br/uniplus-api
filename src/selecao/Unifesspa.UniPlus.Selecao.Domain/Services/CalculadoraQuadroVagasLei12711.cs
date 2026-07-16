namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
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
    private static readonly string[] OrdemGarantiaMinima =
    [
        ModalidadesFederaisLei12711.LbPpi,
        ModalidadesFederaisLei12711.LbQ,
        ModalidadesFederaisLei12711.LbPcd,
        ModalidadesFederaisLei12711.LbEp,
        ModalidadesFederaisLei12711.LiPpi,
        ModalidadesFederaisLei12711.LiPcd,
        ModalidadesFederaisLei12711.LiEp,
    ];

    public static Result<QuadroVagasCalculado> Calcular(
        int voBase,
        decimal pr,
        ReferenciaReservaDemograficaSnapshot demografica,
        IReadOnlyDictionary<string, int> retiradas,
        IReadOnlyDictionary<string, int> suplementares)
    {
        ArgumentNullException.ThrowIfNull(demografica);
        ArgumentNullException.ThrowIfNull(retiradas);
        ArgumentNullException.ThrowIfNull(suplementares);

        Result quantidadesValidas = ValidarQuantidadesNaoNegativas(retiradas, suplementares);
        if (quantidadesValidas.IsFailure)
        {
            return Result<QuadroVagasCalculado>.Failure(quantidadesValidas.Error!);
        }

        Result semColisao = ValidarColisaoDeChave(retiradas, suplementares);
        if (semColisao.IsFailure)
        {
            return Result<QuadroVagasCalculado>.Failure(semColisao.Error!);
        }

        int vrNominal = (int)Math.Ceiling(voBase * pr);
        int vrri = (int)Math.Ceiling(vrNominal * 0.5m);
        int vrsi = vrNominal - vrri;

        int lbPpi = CeilPercentual(vrri, demografica.PpiPercentual);
        int lbQ = CeilPercentual(vrri, demografica.QuilombolaPercentual);
        int lbPcd = CeilPercentual(vrri, demografica.PcdPercentual);
        int lbEp = Math.Max(0, vrri - lbPpi - lbQ - lbPcd);

        int liPpi = CeilPercentual(vrsi, demografica.PpiPercentual);
        int liQ = FloorPercentual(vrsi, demografica.QuilombolaPercentual);
        int liPcd = CeilPercentual(vrsi, demografica.PcdPercentual);
        int liEp = Math.Max(0, vrsi - liPpi - liQ - liPcd);

        int[] bruto = [lbPpi, lbQ, lbPcd, lbEp, liPpi, liPcd, liEp];
        int[] demanda = [.. bruto.Select(v => Math.Max(1, v))];

        int totalDemanda = demanda.Sum() + liQ;
        bool capadoEmVo = totalDemanda > voBase;

        int liQFinal;
        int[] final;
        if (capadoEmVo)
        {
            final = AlocarComEscassez(demanda, voBase, liQ, out liQFinal);
        }
        else
        {
            final = demanda;
            liQFinal = liQ;
        }

        Dictionary<string, int> quadro = new(StringComparer.Ordinal);
        for (int i = 0; i < OrdemGarantiaMinima.Length; i++)
        {
            quadro[OrdemGarantiaMinima[i]] = final[i];
        }

        quadro[ModalidadesFederaisLei12711.LiQ] = liQFinal;

        int vrFinal = quadro.Values.Sum();
        int estouro = Math.Max(0, vrFinal - vrNominal);
        int retiradasTotal = retiradas.Values.Sum();
        int suplementaresTotal = suplementares.Values.Sum();
        int ac = voBase - vrFinal - retiradasTotal;

        if (ac < 0)
        {
            return Result<QuadroVagasCalculado>.Failure(new DomainError(
                "ConfiguracaoDistribuicaoVagas.QuadroAmplaConcorrenciaNegativa",
                $"A ampla concorrência resultaria negativa ({ac}) para VO={voBase}: ajuste o VO, o PR ou as retiradas."));
        }

        quadro[ModalidadesFederaisLei12711.Ac] = ac;

        foreach (KeyValuePair<string, int> retirada in retiradas)
        {
            quadro[retirada.Key] = retirada.Value;
        }

        foreach (KeyValuePair<string, int> suplementar in suplementares)
        {
            quadro[suplementar.Key] = suplementar.Value;
        }

        int totalPublicado = voBase + suplementaresTotal;

        return Result<QuadroVagasCalculado>.Success(new QuadroVagasCalculado(
            quadro, vrNominal, vrFinal, estouro, capadoEmVo, ac, retiradasTotal, suplementaresTotal, totalPublicado));
    }

    /// <summary>
    /// Duas passadas sobre os 7 grupos garantidos, na ordem legal: primeiro o
    /// piso de 1 vaga a cada grupo enquanto houver espaço; depois o excedente
    /// de cada grupo, na mesma ordem. <c>LI_Q</c> (fora da garantia) é servido
    /// por último, com o que sobrar do espaço — nunca mais que o seu bruto por
    /// piso, e nunca com garantia própria de 1 vaga.
    /// </summary>
    private static int[] AlocarComEscassez(int[] demanda, int voBase, int liQBruto, out int liQFinal)
    {
        int[] final = new int[demanda.Length];
        int espaco = voBase;

        for (int i = 0; i < demanda.Length && espaco > 0; i++)
        {
            final[i] = 1;
            espaco--;
        }

        for (int i = 0; i < demanda.Length && espaco > 0; i++)
        {
            int faltante = demanda[i] - final[i];
            int concedido = Math.Min(faltante, espaco);
            final[i] += concedido;
            espaco -= concedido;
        }

        liQFinal = Math.Min(liQBruto, espaco);
        return final;
    }

    private static int CeilPercentual(int baseValor, decimal percentual) =>
        (int)Math.Ceiling(baseValor * percentual / 100m);

    private static int FloorPercentual(int baseValor, decimal percentual) =>
        (int)Math.Floor(baseValor * percentual / 100m);

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
