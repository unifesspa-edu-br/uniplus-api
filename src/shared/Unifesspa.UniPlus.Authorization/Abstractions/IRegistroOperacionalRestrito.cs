namespace Unifesspa.UniPlus.Authorization.Abstractions;

using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Registro operacional restrito das decisões de acesso: destino dedicado, fora
/// do pipeline de log comum (Console/OTLP), para que o uso de uma permissão não
/// se misture à telemetria geral da aplicação.
/// </summary>
/// <remarks>
/// <para>
/// <b>Não</b> é a trilha de auditoria da ADR-0086 — não tem append-only nem
/// código de autenticação de mensagem, e o destino durável é trabalho próprio.
/// O nome evita prometer garantia que esta fatia não entrega.
/// </para>
/// <para>
/// <b>Contrato de falha:</b> a implementação não lança. Uma falha de escrita é
/// registrada no log comum e <b>não</b> altera o veredito já decidido — nem para
/// conceder (falha aberta) nem para negar (indisponibilidade do disco viraria
/// negação de serviço).
/// </para>
/// </remarks>
public interface IRegistroOperacionalRestrito
{
    /// <summary>Registra uma decisão de acesso — permitida ou negada.</summary>
    void Registrar(RegistroDecisaoAcesso registro);
}
