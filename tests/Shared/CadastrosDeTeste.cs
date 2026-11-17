namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
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

    /// <summary>Sem calendário, com os fatos de modalidade do catálogo, sem tipo de ato único, sem consolidação e sem proteção vigente.</summary>
    public static CadastrosDaConfiguracao Padrao { get; } = Com();

    /// <summary>Os cadastros padrão, trocando só o que o cenário declara.</summary>
    public static CadastrosDaConfiguracao Com(
        ContextoDeContagemDePrazos? contagemDePrazos = null,
        FatosDeModalidade? fatosDeModalidade = null,
        TiposDeAtoUnicosPorObjeto? tiposDeAtoUnicos = null,
        CorretoresDasEtapas? corretores = null,
        ProtecaoVigenteDasExigencias? protecao = null,
        FatosDasVersoesPublicadas? fatosDasVersoesPublicadas = null) => new(
            contagemDePrazos ?? ContextoDeContagemDePrazos.SemCalendario,
            fatosDeModalidade ?? FatosDeModalidadeDeTeste.DoCatalogo,
            tiposDeAtoUnicos ?? TiposDeAtoUnicosDeTeste.Nenhum,
            corretores ?? CorretoresDeTeste.SemConsolidacao,
            protecao ?? SemProtecaoVigente,
            fatosDasVersoesPublicadas);
}
