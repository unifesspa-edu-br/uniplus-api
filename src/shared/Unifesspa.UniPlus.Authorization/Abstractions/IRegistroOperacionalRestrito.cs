namespace Unifesspa.UniPlus.Authorization.Abstractions;

using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Registro durável e somente de inclusão dos acessos a dado pessoal protegido por permissão: quem acessou, quando,
/// qual permissão e concessão autorizaram e sobre qual resource — nunca o conteúdo do dado.
/// </summary>
/// <remarks>
/// <para>
/// <b>Contrato de falha:</b> quem não consegue gravar lança <see cref="RegistroDeAcessoNaoGravadoException"/>. O acesso
/// que depende do registro só é entregue depois de gravado, então a falha é erro da requisição: nunca vira negativa
/// de permissão nem omissão do item.
/// </para>
/// <para>
/// O lote é gravado por inteiro ou não é gravado: a implementação só retorna depois que o destino confirmou o lote
/// todo. A nova tentativa dentro da mesma execução repete as mesmas <see cref="RegistroDecisaoAcesso.Chave"/>, e quem
/// lê o destino deduplica por elas; uma nova requisição do usuário é um novo acesso, com registros próprios.
/// </para>
/// <para>
/// Não é a trilha com integridade criptográfica da ADR-0086; HMAC ou encadeamento são evolução à parte.
/// </para>
/// </remarks>
public interface IRegistroOperacionalRestrito
{
    /// <summary>Grava os registros de acesso — permitidos ou negados — e só retorna depois da confirmação do destino.</summary>
    /// <exception cref="RegistroDeAcessoNaoGravadoException">O lote não foi confirmado; nada ficou gravado.</exception>
    Task RegistrarAsync(IReadOnlyList<RegistroDecisaoAcesso> registros, CancellationToken cancellationToken);
}
