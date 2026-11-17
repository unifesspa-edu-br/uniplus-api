namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.ProcessosSeletivos.Publicacao;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Canonicaliza a configuração conferida, com o calendário que a conferência já leu, e monta o pedido que o agregado
/// congela. Não lê nada: o dataset de calendário incoerente não aborta aqui, porque só importa para o processo cuja
/// contagem distingue dia útil, e quem decide isso é a conferência da configuração.
/// </summary>
public sealed class MontagemDoCongelamento(ISnapshotPublicacaoCanonicalizer canonicalizer, IUserContext userContext)
{
    internal PedidoDeCongelamento Montar(ConfiguracaoConferida conferida, DateTimeOffset instante)
    {
        EntradaCanonicalizacao entrada = conferida.Entrada;
        SnapshotCanonico canonico = canonicalizer.Canonicalizar(entrada with { CalendarioDiasUteis = conferida.Cadastros.ContagemDePrazos.CalendarioVigente });

        return new PedidoDeCongelamento
        {
            Dados = entrada.Dados,
            Documento = new DocumentoCongelado(canonico.Bytes, canonico.SchemaVersion, canonico.AlgoritmoHash, entrada.HashDocumento),
            AtorUsuarioContaId = userContext.UserId ?? "system",
            Instante = instante,
            ConfiguracaoApta = conferida.ConfiguracaoApta,
        };
    }
}
