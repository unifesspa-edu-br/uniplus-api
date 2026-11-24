namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O que cada parte representa na ordem de coleta.</summary>
/// <remarks>O número desempata partes na mesma posição.</remarks>
[DescricaoDoContrato("O que cada parte da configuração representa na ordem de coleta: campo, dado do candidato, exigência de documento, seção ou termo.")]
public enum TipoDeParteDaConfiguracao
{
    [CodigoDeContrato("CAMPO", "Campo do formulário", Descricao = "Campo preenchido no formulário.")]
    Campo = 0,
    [CodigoDeContrato("DADO", "Dado do candidato", Descricao = "Dado informado ou calculado para o candidato.")]
    Dado = 1,
    [CodigoDeContrato("EXIGENCIA", "Exigência de documento", Descricao = "Exigência de apresentação de documento.")]
    Exigencia = 2,
    [CodigoDeContrato("SECAO", "Seção do formulário", Descricao = "Seção com exibição condicional.")]
    Secao = 3,
    [CodigoDeContrato("TERMO", "Termo de consentimento", Descricao = "Termo com condição de apresentação.")]
    Termo = 4,
}
