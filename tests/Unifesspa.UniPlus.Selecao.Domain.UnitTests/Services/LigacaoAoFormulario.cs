namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O formulário e a ligação dos itens, dos grupos e dos termos a ele, para o teste que monta o que o avaliador lê
/// sem passar pelo processo: quem cria o item solto não sabe de que formulário ele é.
/// </summary>
internal static class LigacaoAoFormulario
{
    /// <summary>O formulário que cumpre o papel dado, na fase dada, com as páginas do teste.</summary>
    public static FormularioProcesso FormularioNoPapel(PapelDoFormularioDeTeste papel, Guid? faseId = null) =>
        FormularioProcesso.Criar(FormularioDeTeste.DeclaradoNoPapel(papel, faseId), new CronogramaDoProcesso([]), null, FormularioDeTeste.Paginas(papel)).Value!;

    /// <summary>O formulário que cumpre o papel dado, com as páginas dadas.</summary>
    public static FormularioProcesso FormularioNoPapel(PapelDoFormularioDeTeste papel, IReadOnlyList<PaginaDoFormulario> paginas) =>
        FormularioProcesso.Criar(FormularioDeTeste.DeclaradoNoPapel(papel, null), new CronogramaDoProcesso([]), null, paginas).Value!;

    /// <summary>A fase do cronograma que coleta a inscrição, na ordem dada.</summary>
    public static FaseCronograma FaseQueColetaAInscricao(int ordem = 1) => FaseCronograma.Criar(ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(
            ordem, Inicio: new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("INSCRICAO", PapelDaPublicacaoPrevista.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    /// <summary>O formulário que cumpre o papel dado, ligado à fase dada, com as páginas do teste.</summary>
    public static FormularioProcesso FormularioNaFase(PapelDoFormularioDeTeste papel, FaseCronograma fase) =>
        FormularioProcesso.Criar(FormularioDeTeste.DeclaradoNoPapel(papel, fase.Id), CronogramaDoProcesso.Do([fase]), null, FormularioDeTeste.Paginas(papel)).Value!;

    public static FatoColetado Em(this FatoColetado item, FormularioProcesso formulario)
    {
        item.VincularFormulario(formulario);
        return item;
    }

    public static GrupoColetado Em(this GrupoColetado grupo, FormularioProcesso formulario)
    {
        grupo.VincularFormulario(formulario);
        return grupo;
    }

    public static TermoExigidoFormulario Em(this TermoExigidoFormulario termo, FormularioProcesso formulario)
    {
        termo.VincularFormulario(formulario);
        return termo;
    }

    public static IReadOnlyList<FatoColetado> Em(this IEnumerable<FatoColetado> itens, FormularioProcesso formulario) =>
        [.. itens.Select(item => item.Em(formulario))];
}
