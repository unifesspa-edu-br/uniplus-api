namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Enums;

/// <summary>
/// Permissões de unidade que só existem nos hosts de teste. O catálogo do produto ainda não tem
/// nenhuma permissão cujo recurso pertença a uma unidade, e sem uma delas os cenários de
/// concessão e de pedido numa unidade não teriam perfil que os admitisse.
/// </summary>
[ModuloDePermissoes("teste")]
public enum PermissaoDeTeste
{
    /// <summary>Operar as inscrições de uma unidade.</summary>
    [Permissao(
        "teste:inscricoes:operar",
        "Operar inscrições da unidade (teste)",
        "Opera as inscrições dos processos da unidade proprietária.",
        Sensibilidade.Interna,
        EscopoContextoObrigatorio = ["unidadeProprietariaId"])]
    InscricoesOperar = 1,

    /// <summary>Analisar o atendimento especializado de uma unidade.</summary>
    [Permissao(
        "teste:atendimento:analisar",
        "Analisar atendimento especializado da unidade (teste)",
        "Analisa os pedidos de atendimento especializado dos processos da unidade proprietária.",
        Sensibilidade.Sensivel,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 11, II, a",
        EscopoContextoObrigatorio = ["unidadeProprietariaId"])]
    AtendimentoAnalisar = 2,

    /// <summary>Consultar os laudos de atendimento de uma unidade.</summary>
    [Permissao(
        "teste:laudos:consultar",
        "Consultar laudos da unidade (teste)",
        "Consulta os laudos médicos dos candidatos dos processos da unidade proprietária.",
        Sensibilidade.Sensivel,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 11, II, a",
        EscopoContextoObrigatorio = ["unidadeProprietariaId"])]
    LaudosConsultar = 3,
}
