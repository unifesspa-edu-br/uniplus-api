namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// As invariantes de <see cref="Modalidade"/> que cruzam campos já resolvidos: origem × composição,
/// natureza × remanejamento, parâmetros por regra, regra de aplicabilidade pela natureza e autorreferência.
/// </summary>
internal static class CoerenciaDosCamposDaModalidade
{
    /// <summary>
    /// Referências que a modalidade faz a outra pelo código, com o campo e o erro de cada uma.
    /// </summary>
    private static readonly (Func<CamposResolvidosDaModalidade, string?> Ler, string Field, string Code, string Rotulo)[] ReferenciasAOutraModalidade =
    [
        (static c => c.ComposicaoOrigem, "composicaoOrigem", ModalidadeErrorCodes.ComposicaoOrigemCircular, "origem da composição"),
        (static c => c.RemanejamentoParametros.Destino, "remanejamentoDestino", ModalidadeErrorCodes.RemanejamentoDestinoCircular, "destino de remanejamento"),
        (static c => c.RemanejamentoParametros.Par, "remanejamentoPar", ModalidadeErrorCodes.RemanejamentoParCircular, "par de remanejamento"),
        (static c => c.RemanejamentoParametros.Fallback, "remanejamentoFallback", ModalidadeErrorCodes.RemanejamentoFallbackCircular, "fallback de remanejamento"),
    ];

    /// <remarks>
    /// Uma modalidade que cita a si mesma descreve uma operação que não acontece: a vaga
    /// retirada volta para quem a tinha, e o remanejamento não move nada. A integridade
    /// referencial do handler não alcança o caso, porque a própria modalidade existe viva.
    /// </remarks>
    internal static IEnumerable<FieldError> ValidarAutorreferencia(CodigoModalidade codigo, CamposResolvidosDaModalidade campos)
    {
        foreach ((Func<CamposResolvidosDaModalidade, string?> ler, string field, string code, string rotulo) in ReferenciasAOutraModalidade)
        {
            if (ler(campos) is { } referencia && string.Equals(referencia, codigo.Valor, StringComparison.Ordinal))
            {
                yield return new(field, new DomainError(
                    code,
                    $"A modalidade {codigo.Valor} não pode ser o {rotulo} de si mesma."));
            }
        }
    }

    /// <summary>
    /// As invariantes que cruzam campos entre si — origem × composição, natureza ×
    /// remanejamento e parâmetros por regra.
    /// </summary>
    /// <remarks>
    /// Cada checagem só roda quando o token de que depende foi reconhecido: sobre um token
    /// inválido a comparação usaria o valor default do enum e reportaria uma incoerência que
    /// o autor da requisição não cometeu.
    /// </remarks>
    internal static List<FieldError> ValidarCoerenciaCruzada(
        CamposResolvidosDaModalidade campos,
        TokensReconhecidosDaModalidade reconhecidos)
    {
        FieldError?[] violacoes =
        [
            reconhecidos.Composicao ? ValidarOrigemPelaComposicao(campos) : null,
            reconhecidos.Natureza && reconhecidos.Regra
                ? ValidarCoerenciaNaturezaRemanejamento(campos.NaturezaLegal, campos.RegraRemanejamento)
                : null,
            reconhecidos.Regra ? ValidarParametrosPorRegra(campos.RegraRemanejamento, campos.RemanejamentoParametros) : null,
            reconhecidos.Natureza && campos.RegraDeAplicabilidade is { } regra
                ? ValidarRegraPelaNatureza(campos.NaturezaLegal, regra)
                : null,
        ];

        return [.. violacoes.OfType<FieldError>()];
    }

    private static FieldError? ValidarOrigemPelaComposicao(CamposResolvidosDaModalidade campos)
    {
        bool retiradaDeOutraModalidade = campos.ComposicaoVagas == ComposicaoVagas.RetiradaDeOutraModalidade;
        if (retiradaDeOutraModalidade == (campos.ComposicaoOrigem is not null))
        {
            return null;
        }

        return retiradaDeOutraModalidade
            ? RecusaDaOrigem(ModalidadeErrorCodes.OrigemObrigatoriaParaRetiradaDeOutraModalidade, "Composição RETIRADA_DE_OUTRA_MODALIDADE exige o código de origem (composicao_origem).")
            : RecusaDaOrigem(ModalidadeErrorCodes.OrigemApenasParaRetiradaDeOutraModalidade, "Código de origem (composicao_origem) só é permitido na composição RETIRADA_DE_OUTRA_MODALIDADE.");
    }

    private static FieldError RecusaDaOrigem(string codigoDaRecusa, string mensagem) =>
        new("composicaoOrigem", new DomainError(codigoDaRecusa, mensagem));

    private static FieldError? ValidarCoerenciaNaturezaRemanejamento(
        NaturezaLegal natureza,
        RegraRemanejamento? regra)
    {
        switch (natureza)
        {
            case NaturezaLegal.CotaReservada when regra != Enums.RegraRemanejamento.SegueCascata:
                return new("regraRemanejamento", new DomainError(
                    ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente,
                    "Cota reservada exige regra de remanejamento SEGUE_CASCATA."));

            case NaturezaLegal.Ampla when regra is not null:
                return new("regraRemanejamento", new DomainError(
                    ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente,
                    "Ampla concorrência não admite regra de remanejamento."));

            case NaturezaLegal.AcaoAfirmativa
                when regra is not (Enums.RegraRemanejamento.DestinoUnico or Enums.RegraRemanejamento.Cruzado):
                return new("regraRemanejamento", new DomainError(
                    ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente,
                    "Ação afirmativa exige regra de remanejamento DESTINO_UNICO ou CRUZADO."));

            default:
                return null;
        }
    }

    private static FieldError? ValidarParametrosPorRegra(
        RegraRemanejamento? regra,
        RemanejamentoParametros parametros)
    {
        switch (regra)
        {
            case Enums.RegraRemanejamento.DestinoUnico:
                if (parametros.Destino is null)
                {
                    return new("regraRemanejamento", new DomainError(
                        ModalidadeErrorCodes.ParametrosRemanejamentoObrigatorio,
                        "Regra DESTINO_UNICO exige o parâmetro 'destino'."));
                }

                if (parametros.Par is not null || parametros.Fallback is not null)
                {
                    return new("regraRemanejamento", new DomainError(
                        ModalidadeErrorCodes.ParametrosRemanejamentoObrigatorio,
                        "Regra DESTINO_UNICO não admite os parâmetros 'par' e 'fallback'."));
                }

                return null;

            case Enums.RegraRemanejamento.Cruzado:
                if (parametros.Par is null)
                {
                    return new("regraRemanejamento", new DomainError(
                        ModalidadeErrorCodes.ParametrosRemanejamentoObrigatorio,
                        "Regra CRUZADO exige o parâmetro 'par'."));
                }

                // 'fallback' é opcional, e a ausência dele é uma declaração, não uma omissão:
                // significa que o par não tem destino final — a vaga que nenhuma das duas
                // modalidades preencher permanece ociosa. É o caso do certame isolado, que não
                // oferta ampla concorrência para receber a sobra (UNI-REQ-0096).
                if (parametros.Destino is not null)
                {
                    return new("regraRemanejamento", new DomainError(
                        ModalidadeErrorCodes.ParametrosRemanejamentoObrigatorio,
                        "Regra CRUZADO não admite o parâmetro 'destino'."));
                }

                return null;

            // SegueCascata ou sem regra: nenhum parâmetro é permitido.
            default:
                return parametros.TemAlgum
                    ? new("regraRemanejamento", new DomainError(
                        ModalidadeErrorCodes.ParametrosRemanejamentoObrigatorio,
                        "Nenhum parâmetro de remanejamento é admitido para esta regra."))
                    : null;
        }
    }

    /// <remarks>
    /// A natureza fixa a forma da regra: todo candidato concorre à ampla concorrência, e o candidato
    /// nunca concorre a cota da lei e a ação afirmativa ao mesmo tempo, então cada cláusula da cota
    /// exige a opção pelas cotas da lei e cada cláusula da ação afirmativa exige não tê-la feito.
    /// </remarks>
    private static FieldError? ValidarRegraPelaNatureza(NaturezaLegal natureza, PredicadoDnf regra) => natureza switch
    {
        NaturezaLegal.Ampla when regra.Clausulas.Count > 0 => new("regraDeAplicabilidade", new DomainError(
            ModalidadeErrorCodes.AmplaConcorrenciaComCondicao,
            "A ampla concorrência vale para todo candidato: a regra dela não tem condição.")),
        NaturezaLegal.CotaReservada when !OpcaoPelasCotasDaLei.TodaClausulaExige(regra, optou: true) => new("regraDeAplicabilidade", new DomainError(
            ModalidadeErrorCodes.CotaSemOpcaoPelasCotasDaLei,
            $"Toda cláusula da regra de uma cota da lei exige {OpcaoPelasCotasDaLei.Fato} verdadeiro.")),
        NaturezaLegal.AcaoAfirmativa when !OpcaoPelasCotasDaLei.TodaClausulaExige(regra, optou: false) => new("regraDeAplicabilidade", new DomainError(
            ModalidadeErrorCodes.AcaoAfirmativaComOpcaoPelasCotasDaLei,
            $"Toda cláusula da regra de uma ação afirmativa exige {OpcaoPelasCotasDaLei.Fato} falso: quem optou pelas cotas da lei não concorre a ela.")),
        _ => null,
    };
}
