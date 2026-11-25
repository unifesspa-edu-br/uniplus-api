namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Desfecho de uma tentativa de confirmar a instalação com o código, ou o reenvio do código.</summary>
public enum ResultadoDaTentativaDeInstalacao
{
    /// <summary>Código certo, dentro do prazo, da pessoa que iniciou.</summary>
    [CodigoDeContrato("ACEITA", "Aceita")]
    Aceita = 1,

    [CodigoDeContrato("CODIGO_ERRADO", "Código errado")]
    CodigoErrado = 2,

    /// <summary>Código informado depois do prazo: a instalação expirou.</summary>
    [CodigoDeContrato("EXPIRADA", "Expirada")]
    Expirada = 3,

    /// <summary>Quem tentou não tem instalação aguardando código.</summary>
    [CodigoDeContrato("SEM_PENDENCIA", "Sem pendência")]
    SemPendencia = 4,

    /// <summary>Código certo, mas outra instalação já deixou gestor ativo: nada foi confirmado.</summary>
    [CodigoDeContrato("JA_HAVIA_GESTOR", "Já havia gestor")]
    JaHaviaGestor = 5,

    /// <summary>Novo código enviado a quem iniciou, que invalida o anterior; não é uma tentativa de confirmar.</summary>
    [CodigoDeContrato("CODIGO_REENVIADO", "Código reenviado")]
    CodigoReenviado = 6,
}
