namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Quantos valores um dado assume para um mesmo candidato. É o eixo que decide em que sentido o operador se lê: sobre um
/// único valor, <see cref="Operador.Igual"/> compara; sobre vários, significa que o conjunto inclui o valor. Só o dado de
/// opção pode ter vários valores.
/// </summary>
[DescricaoDoContrato("Quantos valores um dado assume para um mesmo candidato: um só ou vários.")]
public enum QuantidadeDeValores
{
    [CodigoDeContrato("VALOR_UNICO", "Um valor", Descricao = "O candidato tem um único valor para o dado.")]
    ValorUnico = 1,

    [CodigoDeContrato("VARIOS_VALORES", "Vários valores", Descricao = "O candidato pode ter mais de um valor para o dado.")]
    VariosValores = 2,
}
