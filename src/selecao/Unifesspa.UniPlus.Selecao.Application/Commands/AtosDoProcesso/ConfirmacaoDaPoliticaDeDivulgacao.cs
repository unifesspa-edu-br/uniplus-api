namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.Entities;
using Domain.Errors;
using Domain.ValueObjects;

using DTOs;

using Queries.ProdutosPrevistos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

/// <summary>O que a confirmação da política de divulgação concluiu: as causas e quem confirmou, quando ela foi exigida.</summary>
/// <param name="Recusas">As causas da confirmação.</param>
/// <param name="ContaId">Quem confirmou, para ficar no registro; nulo quando a confirmação não foi pedida.</param>
internal sealed record PoliticaConferida(IReadOnlyList<FieldError> Recusas, string? ContaId)
{
    public static PoliticaConferida NaoConferida { get; } = new([], null);
}

/// <summary>
/// A confirmação da política de divulgação, exigida quando o tipo do item é resultado, na publicação e na correção.
/// </summary>
internal static class ConfirmacaoDaPoliticaDeDivulgacao
{
    public const string CampoDaConfirmacao = "politicaDeDivulgacaoConfirmada";

    public static PoliticaConferida Conferir(
        TipoAtoPublicadoView tipo, bool confirmada, string? contaId, ConfiguracaoPublicada configuracaoPublicada)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        ArgumentNullException.ThrowIfNull(configuracaoPublicada);

        if (!tipo.EhResultado)
        {
            return PoliticaConferida.NaoConferida;
        }

        return confirmada ? IdentificarQuemConfirmou(contaId) : new([PoliticaNaoConfirmada(configuracaoPublicada)], null);
    }

    private static FieldError PoliticaNaoConfirmada(ConfiguracaoPublicada grafo)
    {
        ConfiguracaoDivulgacaoDto politica = ProjecaoDosProdutosPrevistos.PoliticaDeDivulgacaoDaVersao(grafo);
        string campos = string.Join(", ", ConfiguracaoDivulgacao.CamposPermitidos
            .Where(c => politica.CamposPublicos.Contains(c.Codigo, StringComparer.Ordinal))
            .Select(static c => c.Nome.ToLowerInvariant()));

        return new(CampoDaConfirmacao, new DomainError(
            AtoDoProcessoErrorCodes.PoliticaDeDivulgacaoNaoConfirmada,
            $"O tipo de ato é um resultado, e publicá-lo exige confirmar que o PDF segue a política de divulgação do certame, a da versão vigente: campos públicos — {campos}. O sistema não confere o conteúdo do PDF."));
    }

    private static PoliticaConferida IdentificarQuemConfirmou(string? contaId) =>
        string.IsNullOrWhiteSpace(contaId)
            ? new([new FieldError(null, new DomainError(
                AtoDoProcessoErrorCodes.UsuarioNaoIdentificado,
                "O token não traz o claim sub, e a confirmação da política de divulgação precisa registrar quem a fez."))], null)
            : new([], contaId);
}
