namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O motivo da dependência entre partes da configuração.</summary>
/// <remarks>A ordem numérica determina a ordem das dependências no congelado (UNI-REQ-0078).</remarks>
public enum MotivoDaDependencia
{
    [CodigoDeContrato("RESPOSTA_DO_CAMPO", "Informa o dado", Descricao = "O campo informa o dado do candidato.")]
    InformaDado = 0,
    [CodigoDeContrato("EXIBICAO", "Condiciona a exibição", Descricao = "A parte condiciona a exibição de outra.")]
    CondicionaExibicao = 1,
    [CodigoDeContrato("CALCULO", "Calcula o dado", Descricao = "O dado é usado no cálculo de outro.")]
    CalculaDado = 2,
    [CodigoDeContrato("CONDICAO_DA_EXIGENCIA", "Condiciona a exigência", Descricao = "O dado condiciona a exigência de documento.")]
    CondicionaExigencia = 3,
}
