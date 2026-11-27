namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que se confere na distribuição de vagas declarada para uma oferta antes de montar o quadro: a
/// forma, o teto das vagas autorizadas no e-MEC, o rol fechado da regra, a coerência do ramo e a
/// fronteira entre a quantidade calculada e a declarada. As recusas se acumulam (ADR-0125), na ordem
/// em que as conferências estão acima. Uma regra deixa de rodar só quando um dado dela foi recusado
/// na resolução do cadastro (<see cref="DistribuicaoDeVagasDeclarada.Recusados"/>), e roda sobre as
/// modalidades aceitas.
/// </summary>
public static class ConferenciaDaDistribuicaoDeVagas
{
    private const int PercentualPrecisao = 5;
    private const int PercentualEscala = 4;
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
            .. RecusasDeFormaDaDeclaracao(declarada),
            .. RecusasDeTeto(declarada, vagasAnuaisAutorizadas),
            .. RecusasDoRolFechado(declarada, modalidadesAdmitidas),
            .. RecusasDeCoerenciaDoRamo(declarada),
            .. RecusasDeFronteiraDaQuantidade(declarada),
        ];
    }

    /// <summary>
    /// A forma que não depende de modalidade já resolvida nem da regra: as vagas ofertadas base, o percentual
    /// quando informado, a contagem das modalidades e a quantidade declarada em cada linha do quadro, na ordem
    /// em que o pedido as traz.
    /// </summary>
    /// <param name="quantidadesDoQuadro">A quantidade de cada linha do quadro, nula quando a linha não a traz.</param>
    public static IReadOnlyList<FieldError> ConferirForma(
        int vagasOfertadasBase,
        decimal? percentualDaReservaLegal,
        int quantidadeDeModalidades,
        IReadOnlyList<int?> quantidadesDoQuadro)
    {
        ArgumentNullException.ThrowIfNull(quantidadesDoQuadro);

        return [.. FormaSemQuantidades(vagasOfertadasBase, percentualDaReservaLegal, quantidadeDeModalidades), .. QuantidadesNegativas(quantidadesDoQuadro)];
    }

    /// <summary>
    /// A forma do pedido: a mesma conferência, com a quantidade que falta e a negativa recusadas juntas, na ordem das
    /// linhas do quadro, para que a pessoa corrija de cima para baixo.
    /// </summary>
    public static IReadOnlyList<FieldError> ConferirFormaDoPedido(
        int vagasOfertadasBase,
        decimal? percentualDaReservaLegal,
        int quantidadeDeModalidades,
        IReadOnlyList<int?> quantidadesDoQuadro)
    {
        ArgumentNullException.ThrowIfNull(quantidadesDoQuadro);

        return [.. FormaSemQuantidades(vagasOfertadasBase, percentualDaReservaLegal, quantidadeDeModalidades), .. QuantidadesDoQuadro(quantidadesDoQuadro)];
    }

    private static IEnumerable<FieldError> FormaSemQuantidades(int vagasOfertadasBase, decimal? percentualDaReservaLegal, int quantidadeDeModalidades) =>
        Presentes(
            VagasOfertadasBaseInvalidas(vagasOfertadasBase),
            PercentualForaDoLimite(percentualDaReservaLegal),
            PrecisaoDecimal.Recusa(percentualDaReservaLegal, PercentualPrecisao, PercentualEscala, "percentualDaReservaLegal",
                "O percentual da reserva legal deve ter no máximo 1 dígito inteiro e 4 casas decimais."),
            ModalidadesAusentes(quantidadeDeModalidades));

    /// <summary>A forma da declaração: a do pedido, lida das próprias modalidades e das quantidades que elas trazem.</summary>
    internal static IReadOnlyList<FieldError> ConferirForma(DistribuicaoDeVagasDeclarada declarada)
    {
        ArgumentNullException.ThrowIfNull(declarada);

        return ConferirForma(
            declarada.VagasOfertadasBase,
            declarada.PercentualDaReservaLegal,
            declarada.Modalidades.Count,
            [.. declarada.Modalidades.Select(static m => m.QuantidadeDeclarada)]);
    }

    // O que da forma depende da regra e das modalidades aceitas: o percentual é da Lei de Cotas, onde é
    // obrigatório, e a repetição se lê sobre as modalidades que o cadastro aceitou.
    private static IEnumerable<FieldError> RecusasDeFormaDaDeclaracao(DistribuicaoDeVagasDeclarada declarada) =>
        Presentes(PercentualObrigatorioAusente(declarada), ModalidadesRepetidas(declarada.Modalidades));

    private static FieldError? VagasOfertadasBaseInvalidas(int vagasOfertadasBase) =>
        vagasOfertadasBase <= 0
            ? Recusa("vagasOfertadasBase", DistribuicaoDeVagasErrorCodes.VagasOfertadasBaseInvalido,
                "As vagas ofertadas base devem ser maior que zero.")
            : null;

    private static FieldError? PercentualObrigatorioAusente(DistribuicaoDeVagasDeclarada declarada) =>
        declarada.PercentualDaReservaLegal is null && declarada.NoRamoFederal
            ? RecusaDoPercentual()
            : null;

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

    private static IEnumerable<FieldError> QuantidadesDoQuadro(IReadOnlyList<int?> quantidades) =>
        quantidades
            .Select(static (quantidade, indice) => quantidade is null
                ? Recusa($"quadro[{indice}].quantidade", CodigosDeValidacao.Obrigatorio, "Informe a quantidade de vagas da modalidade.")
                : RecusaDaQuantidadeNegativa(quantidade, indice))
            .OfType<FieldError>();

    private static IEnumerable<FieldError> QuantidadesNegativas(IReadOnlyList<int?> quantidades) =>
        quantidades
            .Select(static (quantidade, indice) => RecusaDaQuantidadeNegativa(quantidade, indice))
            .OfType<FieldError>();

    private static FieldError? RecusaDaQuantidadeNegativa(int? quantidade, int indice) =>
        quantidade is < 0
            ? Recusa($"quadro[{indice}].quantidade", DistribuicaoDeVagasErrorCodes.QuantidadeVagaNegativa,
                "A quantidade de vagas declarada não pode ser negativa.")
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
                $"As vagas ofertadas base excedem as {teto} vagas anuais autorizadas para a oferta.")]
            : [];

    // A regra declara o conjunto exato que reconhece, e não um teto: os dois lados do rol são a mesma
    // invariante, só o sentido da comparação muda. A modalidade que sobra do rol pode ser a que o cadastro
    // recusou; por isso a ausência só se confere quando nenhuma foi recusada.
    private static IEnumerable<FieldError> RecusasDoRolFechado(
        DistribuicaoDeVagasDeclarada declarada, IReadOnlyCollection<string>? modalidadesAdmitidas)
    {
        if (modalidadesAdmitidas is not { Count: > 0 } || declarada.CodigoDaRegra is null)
        {
            return [];
        }

        string[] codigos = [.. declarada.Modalidades.Select(static m => m.Codigo)];
        return Presentes(
            ModalidadesForaDoRol(codigos, modalidadesAdmitidas),
            declarada.Recusado(DadoDaDistribuicaoDeVagas.Modalidades) ? null : ModalidadesDoRolAusentes(codigos, modalidadesAdmitidas));
    }

    private static FieldError? ModalidadesForaDoRol(string[] codigos, IReadOnlyCollection<string> admitidas)
    {
        bool haNaoAdmitida = codigos.Any(c => !admitidas.Contains(c, StringComparer.Ordinal));
        return haNaoAdmitida
            ? Recusa("modalidades", DistribuicaoDeVagasErrorCodes.ModalidadeNaoAdmitidaPelaRegra,
                "A regra de distribuição admite apenas as modalidades do seu rol.")
            : null;
    }

    private static FieldError? ModalidadesDoRolAusentes(string[] codigos, IReadOnlyCollection<string> admitidas)
    {
        string[] ausentes = [.. admitidas.Where(c => !codigos.Contains(c, StringComparer.Ordinal))];
        return ausentes.Length > 0
            ? Recusa("modalidades", DistribuicaoDeVagasErrorCodes.ModalidadeDoRolAusente,
                $"A regra declarada exige a(s) modalidade(s) {string.Join(", ", ausentes)}.")
            : null;
    }

    private static IEnumerable<FieldError> RecusasDeCoerenciaDoRamo(DistribuicaoDeVagasDeclarada declarada) =>
        !declarada.RamoDeterminado ? []
            : declarada.NoRamoFederal ? RecusasDeCoerenciaDaLeiDeCotas(declarada)
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
            RegraDeAjusteVedada(declarada),
            ModalidadesFederaisFaltantes(declarada)),
        .. RetiradasForaDaAmplaConcorrencia(declarada.Modalidades),
    ];

    private static FieldError? ReferenciaDemograficaAusente(DistribuicaoDeVagasDeclarada declarada) =>
        declarada.ReferenciaDemografica is null && !declarada.Recusado(DadoDaDistribuicaoDeVagas.ReferenciaDemografica)
            ? Recusa("referenciaDemografica", DistribuicaoDeVagasErrorCodes.ReferenciaDemograficaObrigatoria,
                "A distribuição pelo ramo federal (Lei 12.711) exige a referência de reserva demográfica.")
            : null;

    private static FieldError? RegraDeAjusteAusente(DistribuicaoDeVagasDeclarada declarada) =>
        declarada.RegraDeAjuste is null && !declarada.Recusado(DadoDaDistribuicaoDeVagas.RegraDeAjuste)
            ? Recusa("regraDeAjuste", DistribuicaoDeVagasErrorCodes.RegraAjusteObrigatoria,
                "A distribuição pelo ramo federal (Lei 12.711) exige a regra de ajuste (AJUSTE_VAGAS_ARTIGO_11_PARAGRAFO_UNICO).")
            : null;

    // As regras de ajuste do quadro são da distribuição institucional. Na Lei de Cotas a reconciliação é a do art. 11,
    // parágrafo único, e aceitar a regra de ajuste do quadro deixaria o ajuste declarado sem rodar.
    private static FieldError? RegraDeAjusteVedada(DistribuicaoDeVagasDeclarada declarada) =>
        declarada.AjusteDoQuadro is not null
            ? Recusa("ajusteDoQuadro", DistribuicaoDeVagasErrorCodes.RegraDeAjusteVedadaNaLeiDeCotas,
                "As regras de ajuste do quadro não se aplicam à distribuição pelo ramo federal (Lei 12.711) — a reconciliação dele é a do art. 11, parágrafo único.")
            : null;

    private static FieldError? ModalidadesFederaisFaltantes(DistribuicaoDeVagasDeclarada declarada)
    {
        if (declarada.Recusado(DadoDaDistribuicaoDeVagas.Modalidades))
        {
            return null;
        }

        string[] informados = [.. declarada.Modalidades.Select(static m => m.Codigo)];
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
            .Select(static _ => Recusa("modalidades", DistribuicaoDeVagasErrorCodes.RetiradaFederalDeveSerDeAmplaConcorrencia,
                "No ramo federal, a modalidade de retirada de outra modalidade (RETIRADA_DE_OUTRA_MODALIDADE) só pode retirar da ampla concorrência (AC) — a fórmula do art. 10 desconta toda retirada de AC."));

    private static IEnumerable<FieldError> Presentes(params FieldError?[] recusas) =>
        recusas.OfType<FieldError>();

    // A fronteira entre o calculado e o declarado: na Lei de Cotas, a sub-reserva e a ampla concorrência
    // nunca aceitam quantidade fixada, e a retirada e o suplemento sempre exigem; no quadro fixado, toda
    // modalidade exige a quantidade, porque não há cálculo algum.
    private static IEnumerable<FieldError> RecusasDeFronteiraDaQuantidade(DistribuicaoDeVagasDeclarada declarada)
    {
        IEnumerable<ModalidadeSelecionada> modalidades = declarada.Modalidades.Where(m => !declarada.ModalidadesComQuantidadeRecusada.Contains(m.ModalidadeOrigemId));

        return declarada.NoRamoFederal ? [.. modalidades.Select(FronteiraDaLeiDeCotas).OfType<FieldError>()]
            : declarada.NoQuadroFixo ? [.. modalidades.Select(FronteiraDoQuadroFixado).OfType<FieldError>()]
            : [];
    }

    private static FieldError? FronteiraDoQuadroFixado(ModalidadeSelecionada modalidade) =>
        modalidade.QuantidadeDeclarada is null
            ? Recusa("modalidades", DistribuicaoDeVagasErrorCodes.QuadroModalidadeAusente,
                "Toda modalidade selecionada precisa de quantidade fixada pelo edital.")
            : null;

    private static FieldError? FronteiraDaLeiDeCotas(ModalidadeSelecionada modalidade)
    {
        bool calculada = modalidade.ComposicaoVagas is ComposicaoVagasModalidade.DentroDaReservaLegal or ComposicaoVagasModalidade.RestanteDasVagasOfertadas;
        bool exigeDeclaracao = modalidade.ComposicaoVagas is ComposicaoVagasModalidade.RetiradaDeOutraModalidade or ComposicaoVagasModalidade.SuplementarAoTotal;

        if (calculada && modalidade.QuantidadeDeclarada is not null)
        {
            return Recusa("modalidades", DistribuicaoDeVagasErrorCodes.QuantidadeCalculadaNaoInformavel,
                "A quantidade da modalidade calculada pela Lei 12.711 não pode ser fixada pelo edital.");
        }

        return exigeDeclaracao && modalidade.QuantidadeDeclarada is null
            ? Recusa("modalidades", DistribuicaoDeVagasErrorCodes.QuantidadeDeclaradaObrigatoria,
                "A modalidade de retirada ou de suplemento exige a quantidade fixada pelo edital.")
            : null;
    }

    private static FieldError Recusa(string campo, string codigo, string mensagem) =>
        new(campo, new DomainError(codigo, mensagem));
}
