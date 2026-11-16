namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O que se confere na distribuição de vagas declarada para uma oferta antes de montar o quadro: a
/// forma, o teto das vagas autorizadas no e-MEC, o rol fechado da regra, a coerência do ramo e a
/// fronteira entre a quantidade calculada e a declarada. As recusas se acumulam (ADR-0125), na ordem
/// em que as conferências estão acima.
/// </summary>
public static class ConferenciaDaDistribuicaoDeVagas
{
    private const decimal PercentualMinimoDaReservaLegal = 0.5m;
    private const decimal PercentualMaximoDaReservaLegal = 1m;

    /// <param name="declarada">A distribuição declarada para a oferta.</param>
    /// <param name="vagasAnuaisAutorizadas">
    /// O teto de vagas anuais autorizadas no e-MEC; nulo quando a oferta não o declara — ausência não é
    /// permissão nem proibição, e nenhum teto é imposto.
    /// </param>
    /// <param name="modalidadesAdmitidas">
    /// Os códigos que a regra de distribuição reconhece; nulo ou vazio quando a regra não restringe o rol.
    /// </param>
    public static IReadOnlyList<FieldError> Conferir(
        DistribuicaoDeVagasDeclarada declarada,
        int? vagasAnuaisAutorizadas,
        IReadOnlyCollection<string>? modalidadesAdmitidas)
    {
        ArgumentNullException.ThrowIfNull(declarada);

        return
        [
            .. RecusasDeForma(declarada),
            .. RecusasDeTeto(declarada, vagasAnuaisAutorizadas),
            .. RecusasDoRolFechado(declarada, modalidadesAdmitidas),
            .. RecusasDeCoerenciaDoRamo(declarada),
            .. RecusasDeFronteiraDaQuantidade(declarada),
        ];
    }

    /// <summary>
    /// A forma que não depende de modalidade já resolvida nem da regra: as vagas ofertadas base, o percentual
    /// quando informado e a contagem das modalidades. O handler a confere de todas as ofertas antes de resolver
    /// qualquer cadastro, para que a recusa de forma não perca para o I/O.
    /// </summary>
    public static IReadOnlyList<FieldError> ConferirForma(
        int vagasOfertadasBase, decimal? percentualDaReservaLegal, int quantidadeDeModalidades) =>
        [.. Presentes(
            VagasOfertadasBaseInvalidas(vagasOfertadasBase),
            PercentualForaDoLimite(percentualDaReservaLegal),
            ModalidadesAusentes(quantidadeDeModalidades))];

    private static IEnumerable<FieldError> RecusasDeForma(DistribuicaoDeVagasDeclarada declarada) =>
        Presentes(
            VagasOfertadasBaseInvalidas(declarada.VagasOfertadasBase),
            PercentualInvalido(declarada),
            ModalidadesAusentes(declarada.Modalidades.Count),
            ModalidadesRepetidas(declarada.Modalidades));

    private static FieldError? VagasOfertadasBaseInvalidas(int vagasOfertadasBase) =>
        vagasOfertadasBase <= 0
            ? Recusa("vagasOfertadasBase", DistribuicaoDeVagasErrorCodes.VagasOfertadasBaseInvalido,
                "As vagas ofertadas base devem ser maior que zero.")
            : null;

    // O percentual é da Lei de Cotas: lá é obrigatório; no quadro fixado no edital não se usa.
    private static FieldError? PercentualInvalido(DistribuicaoDeVagasDeclarada declarada) =>
        declarada.PercentualDaReservaLegal is null && RegraDistribuicaoVagasCodigo.EhRamoFederal(declarada.RegraDeDistribuicao.Codigo)
            ? RecusaDoPercentual()
            : PercentualForaDoLimite(declarada.PercentualDaReservaLegal);

    private static FieldError? PercentualForaDoLimite(decimal? percentual) =>
        percentual is < PercentualMinimoDaReservaLegal or > PercentualMaximoDaReservaLegal
            ? RecusaDoPercentual()
            : null;

    private static FieldError RecusaDoPercentual() =>
        Recusa("percentualDaReservaLegal", DistribuicaoDeVagasErrorCodes.PercentualDaReservaLegalForaDoLimite,
            $"O percentual da reserva legal deve estar entre {PercentualMinimoDaReservaLegal:0.0} e {PercentualMaximoDaReservaLegal:0.0} (art. 10, II).");

    private static FieldError? ModalidadesAusentes(int quantidadeDeModalidades) =>
        quantidadeDeModalidades == 0
            ? Recusa("modalidades", DistribuicaoDeVagasErrorCodes.ModalidadesVazias,
                "A oferta deve ter ao menos uma modalidade selecionada.")
            : null;

    private static FieldError? ModalidadesRepetidas(IReadOnlyList<ModalidadeSelecionada> modalidades) =>
        modalidades.Select(static m => m.Codigo).Distinct(StringComparer.Ordinal).Count() != modalidades.Count
            ? Recusa("modalidades", DistribuicaoDeVagasErrorCodes.ModalidadeDuplicada,
                "Cada modalidade só pode ser selecionada uma vez por oferta.")
            : null;

    // O teto é anual e as vagas ofertadas base são de um certame: uma oferta de 40 vagas comporta dois
    // processos de 20. Comparar com o de um certame é o limite superior seguro, e nunca recusa caso legítimo.
    private static IEnumerable<FieldError> RecusasDeTeto(DistribuicaoDeVagasDeclarada declarada, int? vagasAnuaisAutorizadas) =>
        vagasAnuaisAutorizadas is { } teto && declarada.VagasOfertadasBase > teto
            ? [Recusa("vagasOfertadasBase", DistribuicaoDeVagasErrorCodes.VagasOfertadasBaseAcimaDasVagasAutorizadas,
                $"As vagas ofertadas base ({declarada.VagasOfertadasBase}) excedem as {teto} vagas anuais autorizadas para a oferta.")]
            : [];

    // A regra declara o conjunto exato que reconhece, e não um teto: os dois lados do rol são a mesma
    // invariante, só o sentido da comparação muda.
    private static IEnumerable<FieldError> RecusasDoRolFechado(
        DistribuicaoDeVagasDeclarada declarada, IReadOnlyCollection<string>? modalidadesAdmitidas)
    {
        if (modalidadesAdmitidas is not { Count: > 0 })
        {
            return [];
        }

        string[] codigos = [.. declarada.Modalidades.Select(static m => m.Codigo)];
        string regra = declarada.RegraDeDistribuicao.Codigo;
        return Presentes(ModalidadesForaDoRol(regra, codigos, modalidadesAdmitidas), ModalidadesDoRolAusentes(regra, codigos, modalidadesAdmitidas));
    }

    private static FieldError? ModalidadesForaDoRol(string regra, string[] codigos, IReadOnlyCollection<string> admitidas)
    {
        string[] naoAdmitidas = [.. codigos.Where(c => !admitidas.Contains(c, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal)];
        return naoAdmitidas.Length > 0
            ? Recusa("modalidades", DistribuicaoDeVagasErrorCodes.ModalidadeNaoAdmitidaPelaRegra,
                $"A regra {regra} não admite a(s) modalidade(s) {string.Join(", ", naoAdmitidas)}.")
            : null;
    }

    private static FieldError? ModalidadesDoRolAusentes(string regra, string[] codigos, IReadOnlyCollection<string> admitidas)
    {
        string[] ausentes = [.. admitidas.Where(c => !codigos.Contains(c, StringComparer.Ordinal))];
        return ausentes.Length > 0
            ? Recusa("modalidades", DistribuicaoDeVagasErrorCodes.ModalidadeDoRolAusente,
                $"A regra {regra} exige a(s) modalidade(s) {string.Join(", ", ausentes)}.")
            : null;
    }

    private static IEnumerable<FieldError> RecusasDeCoerenciaDoRamo(DistribuicaoDeVagasDeclarada declarada) =>
        RegraDistribuicaoVagasCodigo.EhRamoFederal(declarada.RegraDeDistribuicao.Codigo)
            ? RecusasDeCoerenciaDaLeiDeCotas(declarada)
            : Presentes(ReferenciaDemograficaIndevida(declarada));

    private static FieldError? ReferenciaDemograficaIndevida(DistribuicaoDeVagasDeclarada declarada) =>
        declarada.ReferenciaDemografica is not null
            ? Recusa("referenciaDemografica", DistribuicaoDeVagasErrorCodes.ReferenciaDemograficaIndevida,
                "A referência de reserva demográfica só se aplica à distribuição pelo ramo federal (Lei 12.711).")
            : null;

    private static IEnumerable<FieldError> RecusasDeCoerenciaDaLeiDeCotas(DistribuicaoDeVagasDeclarada declarada) =>
    [
        .. Presentes(
            ReferenciaDemograficaAusente(declarada),
            RegraDeAjusteAusente(declarada),
            MotorDeAjusteVedado(declarada),
            ModalidadesFederaisFaltantes(declarada.Modalidades)),
        .. RetiradasForaDaAmplaConcorrencia(declarada.Modalidades),
    ];

    private static FieldError? ReferenciaDemograficaAusente(DistribuicaoDeVagasDeclarada declarada) =>
        declarada.ReferenciaDemografica is null
            ? Recusa("referenciaDemografica", DistribuicaoDeVagasErrorCodes.ReferenciaDemograficaObrigatoria,
                "A distribuição pelo ramo federal (Lei 12.711) exige a referência de reserva demográfica.")
            : null;

    private static FieldError? RegraDeAjusteAusente(DistribuicaoDeVagasDeclarada declarada) =>
        declarada.RegraDeAjuste is null
            ? Recusa("regraDeAjuste", DistribuicaoDeVagasErrorCodes.RegraAjusteObrigatoria,
                "A distribuição pelo ramo federal (Lei 12.711) exige a regra de ajuste (RECONCILIACAO-VAGAS-ARTIGO-11-PARAGRAFO-UNICO).")
            : null;

    // Os motores são da distribuição institucional. Na Lei de Cotas a reconciliação é a do art. 11,
    // parágrafo único, e aceitar o motor deixaria o ajuste declarado sem rodar.
    private static FieldError? MotorDeAjusteVedado(DistribuicaoDeVagasDeclarada declarada) =>
        declarada.AjusteDoQuadro is not null
            ? Recusa("ajusteDoQuadro", DistribuicaoDeVagasErrorCodes.MotorDeAjusteVedadoNaLeiDeCotas,
                "Os motores de redução não se aplicam à distribuição pelo ramo federal (Lei 12.711) — a reconciliação dele é a do art. 11, parágrafo único.")
            : null;

    private static FieldError? ModalidadesFederaisFaltantes(IReadOnlyList<ModalidadeSelecionada> modalidades)
    {
        string[] informados = [.. modalidades.Select(static m => m.Codigo)];
        string[] faltantes = [.. ModalidadesFederaisLei12711.CodigosComAc.Where(c => !informados.Contains(c, StringComparer.Ordinal))];
        return faltantes.Length > 0
            ? Recusa("modalidades", DistribuicaoDeVagasErrorCodes.ModalidadesFederaisIncompletas,
                $"A distribuição pela Lei 12.711 exige as 8 modalidades federais e AC; faltam: {string.Join(", ", faltantes)}.")
            : null;
    }

    // A fórmula retira sempre da ampla concorrência; uma retirada com outra origem descontaria de AC
    // sem nunca reduzir a modalidade que alega retirar.
    private static IEnumerable<FieldError> RetiradasForaDaAmplaConcorrencia(IReadOnlyList<ModalidadeSelecionada> modalidades) =>
        modalidades
            .Where(static m => m.ComposicaoVagas == ComposicaoVagasModalidade.RetiradaDeOutraModalidade
                && !string.Equals(m.ComposicaoOrigemCodigo, ModalidadesFederaisLei12711.Ac, StringComparison.Ordinal))
            .Select(static retirada => Recusa("modalidades", DistribuicaoDeVagasErrorCodes.RetiradaFederalDeveSerDeAmplaConcorrencia,
                $"No ramo federal, a modalidade {retirada.Codigo} (RETIRADA_DE_OUTRA_MODALIDADE) só pode retirar da ampla concorrência (AC) — a fórmula do art. 10 desconta toda retirada de AC."));

    private static IEnumerable<FieldError> Presentes(params FieldError?[] recusas) =>
        recusas.OfType<FieldError>();

    // A fronteira entre o calculado e o declarado: na Lei de Cotas, a sub-reserva e a ampla concorrência
    // nunca aceitam quantidade fixada, e a retirada e o suplemento sempre exigem; no quadro fixado, toda
    // modalidade exige a quantidade, porque não há cálculo algum.
    private static IEnumerable<FieldError> RecusasDeFronteiraDaQuantidade(DistribuicaoDeVagasDeclarada declarada)
    {
        string regra = declarada.RegraDeDistribuicao.Codigo;
        if (!RegraDistribuicaoVagasCodigo.EhRamoFederal(regra) && !RegraDistribuicaoVagasCodigo.EhQuadroFixo(regra))
        {
            return [];
        }

        bool federal = RegraDistribuicaoVagasCodigo.EhRamoFederal(regra);
        return [.. declarada.Modalidades.Select(m => federal ? FronteiraDaLeiDeCotas(m) : FronteiraDoQuadroFixado(m)).OfType<FieldError>()];
    }

    private static FieldError? FronteiraDoQuadroFixado(ModalidadeSelecionada modalidade) =>
        modalidade.QuantidadeDeclarada is null
            ? Recusa("modalidades", DistribuicaoDeVagasErrorCodes.QuadroModalidadeAusente,
                $"A modalidade {modalidade.Codigo} está selecionada mas não tem quantidade fixada pelo edital.")
            : null;

    private static FieldError? FronteiraDaLeiDeCotas(ModalidadeSelecionada modalidade)
    {
        bool calculada = modalidade.ComposicaoVagas is ComposicaoVagasModalidade.DentroDaReservaLegal or ComposicaoVagasModalidade.RestanteDasVagasOfertadas;
        bool exigeDeclaracao = modalidade.ComposicaoVagas is ComposicaoVagasModalidade.RetiradaDeOutraModalidade or ComposicaoVagasModalidade.SuplementarAoTotal;

        if (calculada && modalidade.QuantidadeDeclarada is not null)
        {
            return Recusa("modalidades", DistribuicaoDeVagasErrorCodes.QuantidadeCalculadaNaoInformavel,
                $"A quantidade de {modalidade.Codigo} é calculada pela Lei 12.711 — não pode ser fixada pelo edital.");
        }

        return exigeDeclaracao && modalidade.QuantidadeDeclarada is null
            ? Recusa("modalidades", DistribuicaoDeVagasErrorCodes.QuantidadeDeclaradaObrigatoria,
                $"A modalidade {modalidade.Codigo} (retirada ou suplemento) exige a quantidade fixada pelo edital.")
            : null;
    }

    private static FieldError Recusa(string campo, string codigo, string mensagem) =>
        new(campo, new DomainError(codigo, mensagem));
}
