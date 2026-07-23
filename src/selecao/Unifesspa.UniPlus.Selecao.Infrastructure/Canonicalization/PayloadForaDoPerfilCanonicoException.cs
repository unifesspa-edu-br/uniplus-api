namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>
/// O payload contém algo que o perfil canônico recusa representar.
/// </summary>
/// <remarks>
/// <para>
/// Recusar é a alternativa a normalizar. Um perfil que “conserta” o payload — omite a chave
/// cujo valor é <c>null</c>, arredonda o número que não cabe na escala — produz bytes que
/// ninguém escreveu, e o envelope deixa de dizer o que a configuração dizia.
/// </para>
/// <para>
/// É erro de programação no caminho de <b>emissão</b> (o gate de publicação já deveria ter
/// recusado a configuração antes de canonicalizar) e envelope malformado no caminho de
/// <b>leitura</b> — onde <see cref="RegistroCodecsEnvelope"/> a converte em recusa nomeada, em
/// vez de deixar escapar como falha não tratada.
/// </para>
/// </remarks>
public sealed class PayloadForaDoPerfilCanonicoException : InvalidOperationException
{
    public PayloadForaDoPerfilCanonicoException(string message)
        : base(message)
    {
    }

    public PayloadForaDoPerfilCanonicoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public PayloadForaDoPerfilCanonicoException()
        : base("O payload viola o perfil canônico.")
    {
    }
}
