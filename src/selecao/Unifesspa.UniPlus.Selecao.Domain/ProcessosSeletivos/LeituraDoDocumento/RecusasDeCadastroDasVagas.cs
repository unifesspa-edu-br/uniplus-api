namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As recusas de cadastro com que a leitura do documento responde ao código de oferta, regra, referência, modalidade, condição,
/// recurso, tipo de deficiência ou base legal que não existe: o código é o das operações de cada parte, e a mensagem não repete o
/// que foi escrito.
/// </summary>
internal static class RecusasDeCadastroDasVagas
{
    private const string Distribuicao = "ConfiguracaoDistribuicaoVagas.";
    private const string Cascata = "ConfiguracaoCascataRemanejamento.";
    private const string Fada = "ConfiguracaoDoFada.";

    internal static DomainError OfertaNaoEncontrada() =>
        new(Distribuicao + "OfertaCursoNaoEncontrada", "Oferta de curso não encontrada ou não está mais viva.");

    internal static DomainError RegraDeDistribuicaoNaoEncontrada() =>
        new(Distribuicao + "RegraDistribuicaoNaoEncontrada", "Regra de distribuição não encontrada no catálogo de regras.");

    internal static DomainError RegraDeDistribuicaoDeTipoInvalido() =>
        new(Distribuicao + "RegraDistribuicaoTipoInvalido", "A regra não é do tipo regra_distribuicao_vagas.");

    internal static DomainError RegraDeAjusteNaoEncontrada() =>
        new(Distribuicao + "RegraAjusteNaoEncontrada", "Regra de ajuste não encontrada no catálogo de regras.");

    internal static DomainError RegraDeAjusteDeTipoInvalido() =>
        new(Distribuicao + "RegraAjusteTipoInvalido", "A regra não é do tipo regra_ajuste_distribuicao_vagas.");

    internal static DomainError AjusteDoQuadroDesconhecido() =>
        new(Distribuicao + "RegraAjusteTipoInvalido", "Os parâmetros do ajuste do quadro não são os de nenhuma regra de ajuste conhecida.");

    internal static DomainError ReferenciaDemograficaNaoEncontrada() =>
        new(Distribuicao + "ReferenciaDemograficaNaoEncontrada", "Referência de reserva demográfica não encontrada ou não está mais viva.");

    internal static DomainError ModalidadeNaoEncontrada() =>
        new(Distribuicao + "ModalidadeNaoEncontrada", "Modalidade não encontrada ou não está mais viva.");

    internal static DomainError RegraDeRemanejamentoNaoEncontrada() =>
        new(Cascata + "RegraNaoEncontrada", "Regra de remanejamento não encontrada no catálogo de regras.");

    internal static DomainError RegraDeRemanejamentoDeTipoInvalido() =>
        new(Cascata + "RegraTipoInvalido", "A regra informada não é uma regra de remanejamento.");

    internal static DomainError MatrizDivergenteDaRegra() =>
        new(Cascata + "MatrizDivergenteDaRegra", "O destino final ou os destinos informados divergem da declaração da regra.");

    internal static DomainError RegraDoFadaNaoEncontrada() => new(
        Fada + "RegraNaoEncontrada", "Regra do Fator de Atenuação das Desigualdades de Acesso (FADA) não encontrada no catálogo de regras.");

    internal static DomainError RegraDoFadaDeTipoInvalido() =>
        new(Fada + "RegraTipoInvalido", "A regra não é do tipo `REGRA_FADA`.");

    internal static DomainError BaseLegalNaoEncontrada() => new(
        Fada + "BaseLegalNaoEncontrada", "Base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) não encontrada ou desativada.");

    internal static DomainError CamposDoFadaObrigatorios() => new(
        Fada + "CamposObrigatorios",
        "A regra, a versão dela, o fator e a base legal são obrigatórios quando o processo aplica o Fator de Atenuação das Desigualdades de Acesso (FADA).");
}
