namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>O que o veredicto "verificado" produziu no perfil e, se a versão entrou em vigor, a troca que isso fez.</summary>
/// <param name="Desfecho">O que o veredicto fez ao perfil.</param>
/// <param name="Troca">A troca de vigência; só quando <paramref name="Desfecho"/> é a entrada em vigor.</param>
public sealed record ResultadoDoDocumentoVerificado(DesfechoDoDocumentoVerificado Desfecho, TrocaDeVigencia? Troca = null)
{
    public static ResultadoDoDocumentoVerificado Ignorado { get; } = new(DesfechoDoDocumentoVerificado.Ignorado);

    public static ResultadoDoDocumentoVerificado Gravado { get; } = new(DesfechoDoDocumentoVerificado.Gravado);

    public static ResultadoDoDocumentoVerificado VersaoRecusada { get; } = new(DesfechoDoDocumentoVerificado.VersaoRecusada);

    public static ResultadoDoDocumentoVerificado EntrouEmVigor(TrocaDeVigencia troca) =>
        new(DesfechoDoDocumentoVerificado.EntrouEmVigor, troca);
}
