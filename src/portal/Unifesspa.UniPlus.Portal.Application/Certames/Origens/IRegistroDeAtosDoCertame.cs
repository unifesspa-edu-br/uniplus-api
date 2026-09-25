namespace Unifesspa.UniPlus.Portal.Application.Certames.Origens;

/// <summary>
/// Leitura dos atos de um certame na consulta unificada do módulo Publicações.
/// </summary>
/// <remarks>
/// Quem implementa percorre todas as páginas da origem, até o teto configurado, e nunca deixa
/// um cursor da origem sair daqui. Falhas de transporte não escapam como exceção: viram
/// <see cref="LeituraDosAtos.Falha"/>, com os atos lidos até então. Só o cancelamento pedido
/// por quem chama se propaga.
/// </remarks>
public interface IRegistroDeAtosDoCertame
{
    /// <summary>Lê os atos vinculados ao processo seletivo.</summary>
    Task<LeituraDosAtos> LerAtosAsync(Guid processoSeletivoId, CancellationToken cancellationToken);
}
