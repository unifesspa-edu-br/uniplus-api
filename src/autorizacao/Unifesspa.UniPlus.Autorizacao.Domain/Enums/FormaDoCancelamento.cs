namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Quem retirou a versão pendente: uma pessoa, com motivo, ou a contenção, que a cancela sozinha.</summary>
public enum FormaDoCancelamento
{
    /// <summary>Quem criou a versão, ou quem mantém perfis, a retirou com um motivo.</summary>
    [CodigoDeContrato("PESSOA", "Pessoa")]
    Pessoa = 1,

    /// <summary>Uma remoção de permissões cancelou a pendente.</summary>
    [CodigoDeContrato("CONTENCAO", "Contenção")]
    Contencao = 2,
}
