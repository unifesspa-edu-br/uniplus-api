namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Monta o quadro de vagas de uma oferta a partir da distribuição declarada, que já passou pela
/// conferência: pelo cálculo do art. 10 da Lei 12.711/2012 no ramo da Lei de Cotas, ou pela soma
/// declarada, reduzida pelo ajuste, no quadro fixado no edital.
/// </summary>
public static class MontagemDoQuadroDeVagas
{
    /// <summary>Sem excesso a reduzir, o reconciliador devolve o quadro intacto.</summary>
    private static readonly ParametrosReduzirDe SemAjuste = new(string.Empty);

    /// <exception cref="ArgumentException">
    /// A regra de distribuição não é de nenhum dos dois ramos: a partição dos códigos é fechada, e a
    /// regra que entra nela entra também no ramo que a monta.
    /// </exception>
    /// <exception cref="InvalidOperationException">A declaração traz dado recusado: só o que foi aceito se monta.</exception>
    public static Result<QuadroDeVagas> Montar(DistribuicaoDeVagasDeclarada declarada)
    {
        ArgumentNullException.ThrowIfNull(declarada);
        declarada.ExigirSemRecusados();

        return MontarDoRamo(declarada with { Modalidades = [.. declarada.Modalidades.OrderBy(static m => m.Codigo, StringComparer.Ordinal)] });
    }

    // O desempate do maior resto é pela posição no quadro: ordenar as modalidades pelo código faz o quadro
    // não depender da ordem da declaração, que o envelope também não preserva.
    private static Result<QuadroDeVagas> MontarDoRamo(DistribuicaoDeVagasDeclarada declarada)
    {
        if (declarada.NoRamoFederal)
        {
            return MontarDaLeiDeCotas(declarada);
        }

        return declarada.NoQuadroFixo
            ? MontarFixadoNoEdital(declarada)
            : throw new ArgumentException(
                $"A regra de distribuição {declarada.CodigoDaRegra} não é do ramo da Lei de Cotas nem de quadro fixado no edital.",
                nameof(declarada));
    }

    private static Result<QuadroDeVagas> MontarDaLeiDeCotas(DistribuicaoDeVagasDeclarada declarada)
    {
        Result<QuadroDeVagasDaLeiDeCotas> calculo = CalculadoraQuadroVagasLei12711.Calcular(
            declarada.VagasOfertadasBase,
            declarada.PercentualDaReservaLegal!.Value,
            declarada.ReferenciaDemografica!,
            QuantidadesDeclaradas(declarada.Modalidades, ComposicaoVagasModalidade.RetiradaDeOutraModalidade),
            QuantidadesDeclaradas(declarada.Modalidades, ComposicaoVagasModalidade.SuplementarAoTotal));

        return calculo.IsFailure
            ? Result<QuadroDeVagas>.Failure(calculo.Error!)
            : VagasDoQuadroCalculado(declarada.Modalidades, calculo.Value!);
    }

    private static Dictionary<string, int> QuantidadesDeclaradas(
        IReadOnlyList<ModalidadeSelecionada> modalidades, ComposicaoVagasModalidade composicao) =>
        modalidades
            .Where(m => m.ComposicaoVagas == composicao)
            .ToDictionary(static m => m.Codigo, static m => m.QuantidadeDeclarada!.Value, StringComparer.Ordinal);

    private static Result<QuadroDeVagas> VagasDoQuadroCalculado(
        IReadOnlyList<ModalidadeSelecionada> modalidades, QuadroDeVagasDaLeiDeCotas calculado) =>
        modalidades.Select(static m => m.Codigo).FirstOrDefault(c => calculado.Vagas.All(v => v.ModalidadeCodigo != c)) is { } ausente
            ? Result<QuadroDeVagas>.Failure(new DomainError(
                DistribuicaoDeVagasErrorCodes.QuadroModalidadeAusente,
                $"A modalidade {ausente} está selecionada mas não aparece no quadro calculado."))
            : Result<QuadroDeVagas>.Success(calculado with
            {
                Vagas = [.. modalidades.Select(m => new VagasDaModalidade(m.Codigo, calculado.QuantidadeDe(m.Codigo)))],
            });

    // As quantidades dividem o total da oferta; não o ampliam nem o deixam incompleto. A regra de ajuste só reduz:
    // acrescer seria o sistema criar vaga que ninguém autorizou, e a quantidade de vagas do
    // certame é ato administrativo.
    private static Result<QuadroDeVagas> MontarFixadoNoEdital(DistribuicaoDeVagasDeclarada declarada)
    {
        int totalDeclarado = declarada.Modalidades.Sum(static m => m.QuantidadeDeclarada!.Value);
        int excesso = totalDeclarado - declarada.VagasOfertadasBase;

        return excesso < 0 ? Result<QuadroDeVagas>.Failure(QuadroQueNaoCompleta(declarada, totalDeclarado))
            : excesso > 0 && declarada.AjusteDoQuadro is null ? Result<QuadroDeVagas>.Failure(QuadroQueExcede(declarada, totalDeclarado))
            : ReduzirOExcesso(declarada, excesso);
    }

    private static DomainError QuadroQueNaoCompleta(DistribuicaoDeVagasDeclarada declarada, int totalDeclarado) => new(
        DistribuicaoDeVagasErrorCodes.QuadroNaoCompletaAsVagasOfertadasBase,
        $"As quantidades do quadro somam {totalDeclarado}, e a oferta tem {declarada.VagasOfertadasBase} vagas a distribuir.");

    private static DomainError QuadroQueExcede(DistribuicaoDeVagasDeclarada declarada, int totalDeclarado) => new(
        DistribuicaoDeVagasErrorCodes.QuadroExcedeAsVagasOfertadasBase,
        $"As quantidades do quadro somam {totalDeclarado}, acima das {declarada.VagasOfertadasBase} vagas da oferta.");

    private static Result<QuadroDeVagas> ReduzirOExcesso(DistribuicaoDeVagasDeclarada declarada, int excesso)
    {
        IReadOnlyList<ModalidadeSelecionada> modalidades = declarada.Modalidades;
        int[] declaradas = [.. modalidades.Select(static m => m.QuantidadeDeclarada!.Value)];

        Result<ReconciliadorQuadroInstitucional.QuadroReconciliado> reconciliado = ReconciliadorQuadroInstitucional.Reduzir(
            [.. modalidades.Select(static m => m.Codigo)], declaradas, excesso, declarada.AjusteDoQuadro ?? SemAjuste);

        return reconciliado.IsFailure
            ? Result<QuadroDeVagas>.Failure(reconciliado.Error!)
            : Result<QuadroDeVagas>.Success(QuadroReduzido(declarada, declaradas, reconciliado.Value!.Quantidades));
    }

    private static QuadroDeVagasFixadoNoEdital QuadroReduzido(
        DistribuicaoDeVagasDeclarada declarada, int[] declaradas, IReadOnlyList<int> reduzidas)
    {
        List<VagasDaModalidade> vagas = [];
        List<VagasDaModalidade> retiradas = [];

        for (int indice = 0; indice < declarada.Modalidades.Count; indice++)
        {
            string codigo = declarada.Modalidades[indice].Codigo;
            vagas.Add(new VagasDaModalidade(codigo, reduzidas[indice]));
            if (declaradas[indice] > reduzidas[indice])
            {
                retiradas.Add(new VagasDaModalidade(codigo, declaradas[indice] - reduzidas[indice]));
            }
        }

        return new QuadroDeVagasFixadoNoEdital(vagas, declarada.VagasOfertadasBase, retiradas);
    }
}
