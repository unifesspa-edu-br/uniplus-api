namespace Unifesspa.UniPlus.Selecao.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, SelecaoDomainErrorRegistration>().")]
internal sealed class SelecaoDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        new("Edital.NaoEncontrado", new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.edital.nao_encontrado", "Edital não encontrado")),
        new("Edital.JaPublicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.edital.ja_publicado", "Edital já publicado")),
        new("NumeroEdital.Invalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.numero_edital.invalido", "Número de edital inválido")),
        new("NumeroEdital.AnoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.numero_edital.ano_invalido", "Ano do edital inválido")),
        new("PeriodoInscricao.Invalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.periodo_inscricao.invalido", "Período de inscrição inválido")),
        new("FormulaCalculo.FatorInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formula_calculo.fator_invalido", "Fator de divisão inválido")),
        new("FormulaCalculo.BonusInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formula_calculo.bonus_invalido", "Bônus regional inválido")),
        new("Inscricao.StatusInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.status_invalido", "Status de inscrição inválido")),
        // ObrigatoriedadeLegal placeholder (Story #459) — campos básicos. Forma plena com
        // hash canônico/vigência/governance entra em #460 (US-F4-02), momento em que
        // novos códigos (RegraCodigoDuplicada, VigenciaInvalida, HashColisao) são adicionados.
        new("ObrigatoriedadeLegal.RegraCodigoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.regra_codigo_obrigatorio", "RegraCodigo obrigatório")),
        new("ObrigatoriedadeLegal.PredicadoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.predicado_obrigatorio", "Predicado obrigatório")),
        new("ObrigatoriedadeLegal.BaseLegalObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.base_legal_obrigatoria", "BaseLegal obrigatória")),
        new("ObrigatoriedadeLegal.DescricaoHumanaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.descricao_humana_obrigatoria", "DescricaoHumana obrigatória")),
        // Cursor.* codes vivem em Infrastructure.Core/Pagination/PaginationDomainErrorRegistration —
        // capability cross-module, registrada uma única vez via AddCursorPagination().
    ];
}
