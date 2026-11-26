namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>A recusa do tipo de etapa que o cadastro não tem ou que não está ativo, com o mesmo texto na gravação e na leitura do documento.</summary>
public static class RecusaDoTipoDeEtapa
{
    /// <param name="tipoCitado">O que identifica o tipo para quem o citou: o identificador no pedido ou o código no documento.</param>
    public static DomainError NaoEncontradoOuInativo(string tipoCitado) => new(
        ProcessoSeletivoErrorCodes.TipoEtapaNaoEncontradoOuInativo,
        $"Tipo de etapa {tipoCitado} não encontrado ou não está ativo.");
}
