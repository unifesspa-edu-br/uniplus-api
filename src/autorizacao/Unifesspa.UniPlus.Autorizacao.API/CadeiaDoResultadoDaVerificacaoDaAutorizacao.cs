namespace Unifesspa.UniPlus.Autorizacao.API;

using Unifesspa.UniPlus.Autorizacao.Application.Commands;
using Unifesspa.UniPlus.Verificador.Contratos;

using Wolverine.Runtime.Handlers;

/// <summary>Identifica a cadeia do Wolverine que consome o resultado da verificação dos PDFs da Autorização.</summary>
internal static class CadeiaDoResultadoDaVerificacaoDaAutorizacao
{
    public static bool Eh(HandlerChain chain) =>
        chain.MessageType == typeof(ResultadoDaVerificacaoDeArquivo)
        && chain.Handlers.Any(h => h.HandlerType == typeof(RegistrarResultadoDaVerificacaoDoDocumentoDaAutorizacaoHandler));
}
