namespace Unifesspa.UniPlus.Portal.Application.Certames.Origens;

/// <summary>
/// Leitura do detalhe público do certame no módulo Seleção.
/// </summary>
/// <remarks>
/// Quem implementa não deixa exceção de transporte escapar: falha de rede, tempo esgotado,
/// circuito aberto e resposta fora do contrato viram <see cref="ConsultaDoCertame.Indisponivel"/>.
/// Só o cancelamento pedido por quem chama se propaga.
/// </remarks>
public interface IContratoPublicoDoCertame
{
    /// <summary>Consulta o certame pelo identificador do processo seletivo.</summary>
    Task<ConsultaDoCertame> ObterPorIdAsync(Guid processoSeletivoId, CancellationToken cancellationToken);

    /// <summary>Consulta o certame pelo identificador legível congelado na publicação.</summary>
    Task<ConsultaDoCertame> ObterPorIdentificadorAsync(string identificador, CancellationToken cancellationToken);
}
