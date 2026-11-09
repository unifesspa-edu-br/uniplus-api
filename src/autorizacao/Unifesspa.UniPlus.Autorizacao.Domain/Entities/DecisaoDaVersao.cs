namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Decisão de uma etapa da aprovação de uma versão do perfil. Só se acrescenta, nunca se altera: é evidência forense
/// (ADR-0063), sem soft delete nem auditoria de alteração. Guarda o que a decisão avaliou e se quem decidiu tem o
/// perfil, para que a trilha mostre o risco aceito pela ADR-0157.
/// </summary>
public sealed class DecisaoDaVersao : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid VersaoId { get; private init; }

    /// <summary>O ciclo de aprovação em que a decisão foi dada; sobe quando a mudança de classe descarta as aprovações.</summary>
    public int Ciclo { get; private init; }

    public EtapaDaVersao Etapa { get; private init; }

    public ResultadoDaDecisao Resultado { get; private init; }

    public string DecisorEmissor { get; private set; } = string.Empty;

    public string DecisorSub { get; private set; } = string.Empty;

    public string? DecisorNome { get; private set; }

    /// <summary>E-mail verificado do token no instante da decisão, normalizado; nulo quando o token não o trouxe.</summary>
    public string? DecisorEmail { get; private set; }

    public bool DecisorTemConcessao { get; private set; }

    /// <summary>Obrigatório na recusa; nunca vai nos avisos.</summary>
    public string? Motivo { get; private init; }

    /// <summary>Os códigos que a versão acrescentava à vigente no instante da decisão, ordenados.</summary>
    public string[] AcrescimoAvaliado { get; private init; } = [];

    public bool ExigiaProtecaoDeDados { get; private init; }

    public DateTimeOffset Instante { get; private init; }

    public Identidade Decisor => new(DecisorEmissor, DecisorSub, DecisorNome);

    // Construtor de materialização do EF Core.
    private DecisaoDaVersao()
    {
    }

    private DecisaoDaVersao(VersaoDoPerfil versao, PedidoDeDecisaoDaVersao pedida, DiferencaDaVersao diferenca)
    {
        VersaoId = versao.Id;
        Ciclo = versao.CicloDeAprovacao;
        Etapa = pedida.Etapa;
        Resultado = pedida.Resultado;
        Motivo = pedida.Resultado == ResultadoDaDecisao.Recusado ? pedida.Motivo : null;
        AcrescimoAvaliado = [.. diferenca.Acrescimo];
        ExigiaProtecaoDeDados = diferenca.ExigeProtecaoDeDados;
        Instante = pedida.Instante;
        AtribuirDecisor(pedida.Autoria);
    }

    internal static DecisaoDaVersao Registrar(
        VersaoDoPerfil versao, PedidoDeDecisaoDaVersao pedida, DiferencaDaVersao diferenca) =>
        new(versao, pedida, diferenca);

    private void AtribuirDecisor(AutoriaDaVersao autoria)
    {
        DecisorEmissor = autoria.Pessoa.Emissor;
        DecisorSub = autoria.Pessoa.Sub;
        DecisorNome = autoria.Pessoa.Nome;
        DecisorEmail = autoria.Email;
        DecisorTemConcessao = autoria.TemConcessao;
    }
}
