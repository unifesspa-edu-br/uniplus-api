namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>O cronograma e os formulários do documento congelado, para quem lê só um trecho dele.</summary>
internal static class FormulariosDoDocumentoDeTeste
{
    internal const string CodigoDaInscricao = "INSCRICAO";

    internal static CronogramaDoProcesso SemFases { get; } = new([]);

    internal static FormulariosDoDocumento Nenhum => De([]);

    internal static FormulariosDoDocumento De(IReadOnlyList<FormularioProcesso> formularios) => new(formularios, SemFases);

    internal const string CodigoDaHabilitacao = "HABILITACAO";

    /// <summary>
    /// Um documento com uma fase que coleta inscrição e dois formulários: <c>INSCRICAO</c>, nessa fase, e <c>HABILITACAO</c>, sem fase.
    /// É o que os campos e os grupos lidos precisam para encontrar o formulário que os exige.
    /// </summary>
    internal static FormulariosDoDocumento ComFormularios()
    {
        FaseCronograma fase = Fase(coletaInscricao: true);
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([fase]);
        return new FormulariosDoDocumento(
            [Formulario(CodigoDaHabilitacao, null, cronograma), Formulario(CodigoDaInscricao, fase.Id, cronograma)],
            cronograma);
    }

    /// <summary>As páginas mínimas de um formulário: uma seção de dados e a revisão e aceite por último.</summary>
    internal static IReadOnlyList<PaginaDoFormulario> Paginas() =>
    [
        PaginaDoFormulario.Criar(new PaginaEstrutura("DADOS", 0, TipoPaginaFormulario.Secao, null), "Dados", null, null).Value!,
        PaginaDoFormulario.Criar(new PaginaEstrutura("REVISAO", 1, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite), "Revisão e aceite", null, null).Value!,
    ];

    /// <summary>O código de cada formulário do documento por identificador, como a serialização liga os itens a eles.</summary>
    internal static IReadOnlyDictionary<Guid, string> CodigosPorIdentificador(this FormulariosDoDocumento documento) =>
        documento.Formularios.ToDictionary(formulario => formulario.Id, formulario => formulario.Codigo.Valor);

    private static FormularioProcesso Formulario(string codigo, Guid? faseId, CronogramaDoProcesso cronograma) =>
        FormularioProcesso.Criar(
            new FormularioDeclarado(CodigoDoFormulario.Criar(codigo).Value!, faseId, Obrigatorio: true),
            cronograma,
            "Formulário fictício",
            Paginas()).Value!;

    /// <summary>Uma fase do cronograma que coleta a inscrição, ou, quando não coleta, a do resultado final.</summary>
    internal static FaseCronograma Fase(bool coletaInscricao) =>
        FaseCronograma.Criar(
            ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
                Guid.CreateVersion7(), coletaInscricao ? "INSCRICAO" : "RESULTADO_FINAL", OrigemDataFase.NoEdital, AgrupaEtapas: !coletaInscricao,
                PermiteComplementacao: false, ColetaInscricao: coletaInscricao, ColetaSolicitacaoIsencao: false)),
            new DecisoesDeCronogramaDaFase(
                1, Inicio: new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(
                Produtos: [ProdutoDaFase.Criar(coletaInscricao ? "INSCRICAO" : "RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)],
                EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;
}
