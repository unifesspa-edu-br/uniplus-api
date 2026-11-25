namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O que gerou a concessão: um pedido aprovado ou a instalação da plataforma.</summary>
public enum TipoDeOrigem
{
    /// <summary>Pedido de acesso aprovado.</summary>
    [CodigoDeContrato("PEDIDO", "Pedido")]
    Pedido = 1,

    /// <summary>Instalação da plataforma, que designa os primeiros gestores de acessos.</summary>
    [CodigoDeContrato("INSTALACAO", "Instalação")]
    Instalacao = 2,
}
