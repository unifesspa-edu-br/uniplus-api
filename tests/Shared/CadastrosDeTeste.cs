namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os cadastros que o teste entrega à conferência da configuração e ao painel de conformidade.</summary>
internal static class CadastrosDeTeste
{
    private static readonly ProtecaoVigenteDasExigencias SemProtecaoVigente = new(
        new Dictionary<string, ClassificacaoDeProtecao>(StringComparer.Ordinal),
        new Dictionary<Guid, ProtecaoDoDocumento>(),
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<Guid>());

    private static readonly MotivosEscolhidos SemMotivosEscolhidos = new([], numeroDaVersaoQueNasce: 1);

    /// <summary>Com calendário vigente, com os fatos de modalidade do catálogo, sem tipo de ato único, sem consolidação, sem proteção vigente e sem motivo escolhido: serve ao teste que não tem processo ou cujo processo não declara decisão que exija motivo.</summary>
    public static CadastrosDaConfiguracao Padrao { get; } = Com();

    /// <summary>Os cadastros padrão, com os motivos que cobrem as decisões que o processo declara.</summary>
    public static CadastrosDaConfiguracao Para(ProcessoSeletivo processo) =>
        Com(motivosEscolhidos: MotivosDoProcessoDeTeste.Cobrindo(processo));

    /// <summary>Os cadastros padrão, trocando só o que o cenário declara.</summary>
    public static CadastrosDaConfiguracao Com(
        ContextoDeContagemDePrazos? contagemDePrazos = null,
        FatosDeModalidade? fatosDeModalidade = null,
        TiposDeAtoUnicosPorObjeto? tiposDeAtoUnicos = null,
        CorretoresDasEtapas? corretores = null,
        ProtecaoVigenteDasExigencias? protecao = null,
        MotivosEscolhidos? motivosEscolhidos = null,
        FatosDasVersoesPublicadas? fatosDasVersoesPublicadas = null) => new(
            contagemDePrazos ?? ContagemDePrazosDeTeste.ComCalendarioVigente,
            fatosDeModalidade ?? FatosDeModalidadeDeTeste.DoCatalogo,
            tiposDeAtoUnicos ?? TiposDeAtoUnicosDeTeste.Nenhum,
            corretores ?? CorretoresDeTeste.SemConsolidacao,
            protecao ?? SemProtecaoVigente,
            motivosEscolhidos ?? SemMotivosEscolhidos,
            fatosDasVersoesPublicadas);
}
