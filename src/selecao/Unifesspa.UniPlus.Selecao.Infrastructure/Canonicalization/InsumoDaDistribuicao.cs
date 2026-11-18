namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>As regras que a distribuição de uma oferta aplica: a da distribuição, a do ajuste e o motor do ajuste declarado.</summary>
internal readonly record struct RegrasDaDistribuicao(
    ReferenciaRegra Distribuicao,
    ReferenciaRegra? Ajuste,
    ParametrosRegraAjusteDistribuicao? AjusteDoQuadro);

/// <summary>O que o envelope congela da declaração de uma oferta, lido antes de a configuração ser remontada.</summary>
internal readonly record struct InsumoDaDistribuicao(
    Guid Oferta,
    int VagasOfertadasBase,
    decimal? PercentualDaReservaLegal,
    RegrasDaDistribuicao Regras,
    ReferenciaReservaDemograficaSnapshot? Demografica,
    (string? Codigo, string? Rotulo)? GrupoAreaEnem)
{
    internal DistribuicaoDeVagasDeclarada Declarada(IReadOnlyList<ModalidadeSelecionada> modalidades) => new()
    {
        RegraDeDistribuicao = Regras.Distribuicao,
        VagasOfertadasBase = VagasOfertadasBase,
        PercentualDaReservaLegal = PercentualDaReservaLegal,
        ReferenciaDemografica = Demografica,
        RegraDeAjuste = Regras.Ajuste,
        AjusteDoQuadro = Regras.AjusteDoQuadro,
        Modalidades = modalidades,
    };

    // O rol deriva do catálogo de regras: a leitura confere o que a regra publicada admitia.
    internal FatosDaOfertaNoCadastro FatosDoCadastro(OfertaCursoSnapshot nomeDaOferta) => new()
    {
        ModalidadesAdmitidas = RegraDistribuicaoVagasCodigo.RolFechado(Regras.Distribuicao.Codigo),
        GrupoAreaEnem = GrupoAreaEnem,
        OfertaCurso = nomeDaOferta,
    };
}
