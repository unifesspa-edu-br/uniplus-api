namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As recusas de cadastro com que a leitura do documento responde ao código de oferta, regra, referência, modalidade, condição,
/// recurso, tipo de deficiência ou base legal que não existe: o código é o das operações de cada parte, e a mensagem não repete o
/// que foi escrito.
/// </summary>
public static class RecusasDeCadastroDasVagas
{
    private const string Distribuicao = "ConfiguracaoDistribuicaoVagas.";
    private const string Cascata = "ConfiguracaoCascataRemanejamento.";
    private const string Fada = "ConfiguracaoDoFada.";

    public static DomainError OfertaNaoEncontrada() =>
        new(Distribuicao + "OfertaCursoNaoEncontrada", "A oferta de curso deve ser uma do cadastro de ofertas.");

    internal static DomainError RegraDeDistribuicaoNaoEncontrada() =>
        new(Distribuicao + "RegraDistribuicaoNaoEncontrada", "Regra de distribuição não encontrada no catálogo de regras.");

    internal static DomainError RegraDeDistribuicaoDeTipoInvalido() =>
        new(Distribuicao + "RegraDistribuicaoTipoInvalido", "A regra informada não é uma regra de distribuição de vagas.");

    internal static DomainError RegraDeAjusteNaoEncontrada() =>
        new(Distribuicao + "RegraAjusteNaoEncontrada", "Regra de ajuste não encontrada no catálogo de regras.");

    internal static DomainError RegraDeAjusteDeTipoInvalido() =>
        new(Distribuicao + "RegraAjusteTipoInvalido", "A regra informada não é uma regra de ajuste da distribuição de vagas.");

    internal static DomainError AjusteDoQuadroDesconhecido() =>
        new(Distribuicao + "RegraAjusteTipoInvalido", "Os parâmetros do ajuste do quadro não são os de nenhuma regra de ajuste conhecida.");

    public static DomainError ReferenciaDemograficaNaoEncontrada() =>
        new(Distribuicao + "ReferenciaDemograficaNaoEncontrada", "A referência de reserva demográfica não existe no cadastro ou foi removida.");

    public static DomainError ModalidadeNaoEncontrada() =>
        new(Distribuicao + "ModalidadeNaoEncontrada", "A modalidade não existe no cadastro ou foi removida.");

    internal static DomainError RegraDeRemanejamentoNaoEncontrada() =>
        new(Cascata + "RegraNaoEncontrada", "Regra de remanejamento não encontrada no catálogo de regras.");

    internal static DomainError RegraDeRemanejamentoDeTipoInvalido() =>
        new(Cascata + "RegraTipoInvalido", "A regra informada não é uma regra de remanejamento.");

    internal static DomainError MatrizDivergenteDaRegra() =>
        new(Cascata + "MatrizDivergenteDaRegra", "O destino final ou os destinos informados divergem da declaração da regra.");

    internal static DomainError RegraDoFadaNaoEncontrada() => new(
        Fada + "RegraNaoEncontrada", "Regra do Fator de Atenuação das Desigualdades de Acesso (FADA) não encontrada no catálogo de regras.");

    internal static DomainError RegraDoFadaDeTipoInvalido() =>
        new(Fada + "RegraTipoInvalido", "A regra informada não é uma regra do Fator de Atenuação das Desigualdades de Acesso (FADA).");

    internal static DomainError BaseLegalNaoEncontrada() => new(
        Fada + "BaseLegalNaoEncontrada", "Base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) não encontrada ou desativada.");

    internal static DomainError CamposDoFadaObrigatorios() => new(
        Fada + "CamposObrigatorios",
        "A regra, a versão dela, o fator e a base legal são obrigatórios quando o processo aplica o Fator de Atenuação das Desigualdades de Acesso (FADA).");
}
