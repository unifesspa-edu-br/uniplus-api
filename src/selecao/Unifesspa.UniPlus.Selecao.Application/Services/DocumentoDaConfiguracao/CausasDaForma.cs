namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using Kernel.Results;

using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

/// <summary>
/// As causas de recusa da forma do documento, cada uma no ponto do documento que a provoca (JSON Pointer). A mensagem descreve
/// a regra e o que se espera, nunca o texto que a pessoa digitou.
/// </summary>
public static class CausasDaForma
{
    private const int BytesPorMebibyte = 1024 * 1024;
    private const string SemCaractereNulo = "o texto contém o caractere nulo (U+0000), que o texto guardado não aceita";
    private const string SemSubstitutoIsolado = "o texto contém metade de um par substituto UTF-16, que o texto guardado não aceita";

    public static FieldError Malformado() =>
        Causa(null, DocumentoDaConfiguracaoErrorCodes.FormaInvalida, "O documento não é um JSON válido.");

    public static FieldError RaizNaoEhObjeto() =>
        Causa(string.Empty, DocumentoDaConfiguracaoErrorCodes.TipoInvalido, "O documento deve ser um objeto JSON.");

    public static FieldError TamanhoAcimaDoLimite() =>
        Causa(
            null,
            DocumentoDaConfiguracaoErrorCodes.AcimaDoLimite,
            $"O documento excede o tamanho máximo de {LimitesDoDocumento.TamanhoMaximoEmBytes / BytesPorMebibyte} MiB.");

    public static FieldError ProfundidadeAcimaDoLimite(string ponteiro) =>
        Causa(
            ponteiro,
            DocumentoDaConfiguracaoErrorCodes.AcimaDoLimite,
            $"O documento excede a profundidade máxima de {LimitesDoDocumento.ProfundidadeMaxima} níveis.");

    public static FieldError QuantidadeDeNosAcimaDoLimite() =>
        Causa(
            string.Empty,
            DocumentoDaConfiguracaoErrorCodes.AcimaDoLimite,
            $"O documento excede o máximo de {LimitesDoDocumento.QuantidadeMaximaDeNos} valores, objetos e listas.");

    public static FieldError QuantidadeDeNosDoTextoAcimaDoLimite() =>
        Causa(
            string.Empty,
            DocumentoDaConfiguracaoErrorCodes.AcimaDoLimite,
            $"O documento excede o máximo de {LimitesDoDocumento.QuantidadeMaximaDeNosDoTextoRecebido} valores, objetos e listas.");

    public static FieldError PropriedadeRepetida(string ponteiro) =>
        Causa(ponteiro, DocumentoDaConfiguracaoErrorCodes.PropriedadeRepetida, "O objeto repete uma propriedade; cada propriedade só pode aparecer uma vez.");

    public static FieldError ChaveRepetida(string ponteiro) =>
        Causa(ponteiro, DocumentoDaConfiguracaoErrorCodes.ChaveRepetida, "A chave já é de uma parte anterior do mesmo tipo; cada parte precisa de uma chave própria.");

    public static FieldError OfertaCursoRepetida(string ponteiro) =>
        Causa(ponteiro, DistribuicaoDeVagasErrorCodes.OfertaDeCursoDuplicada, "Cada oferta de curso só pode ter uma distribuição de vagas no processo.");

    public static FieldError CampoDesconhecido(string ponteiro) =>
        Causa(ponteiro, DocumentoDaConfiguracaoErrorCodes.CampoDesconhecido, "O campo não existe neste formato de documento.");

    public static FieldError TipoInvalido(string ponteiro, string tipoEsperado) =>
        Causa(ponteiro, DocumentoDaConfiguracaoErrorCodes.TipoInvalido, $"O valor deve ser do tipo {tipoEsperado}.");

    public static FieldError ValorInvalido(string ponteiro, string regra) =>
        Causa(ponteiro, DocumentoDaConfiguracaoErrorCodes.ValorInvalido, $"O valor é inválido: {regra}.");

    public static FieldError CaractereNuloNoValor(string ponteiro) =>
        ValorInvalido(ponteiro, SemCaractereNulo);

    public static FieldError CaractereNuloNoNomeDaPropriedade(string ponteiroDoObjeto) =>
        ValorInvalido(ponteiroDoObjeto, "o nome de uma propriedade deste objeto contém o caractere nulo (U+0000), que o texto guardado não aceita");

    public static FieldError SubstitutoIsoladoNoValor(string ponteiro) =>
        ValorInvalido(ponteiro, SemSubstitutoIsolado);

    public static FieldError SubstitutoIsoladoNoNomeDaPropriedade(string ponteiroDoObjeto) =>
        ValorInvalido(ponteiroDoObjeto, "o nome de uma propriedade deste objeto contém metade de um par substituto UTF-16, que o texto guardado não aceita");

    public static FieldError CampoObrigatorioAusente(string ponteiro) =>
        Causa(ponteiro, DocumentoDaConfiguracaoErrorCodes.CampoObrigatorioAusente, "O campo é obrigatório e não foi informado.");

    public static FieldError VersaoDoFormatoNaoSuportada(string ponteiro) =>
        Causa(
            ponteiro,
            DocumentoDaConfiguracaoErrorCodes.VersaoDoFormatoNaoSuportada,
            "O documento declara um formato ou uma versão de formato que esta versão do sistema não suporta.");

    private static FieldError Causa(string? ponteiro, string codigo, string mensagem) =>
        new(ponteiro, new DomainError(codigo, mensagem));
}
