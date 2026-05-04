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
        new("Cursor.Invalido", new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.selecao.cursor_invalido", "Cursor de paginação inválido")),
        new("Cursor.Expirado", new DomainErrorMapping(StatusCodes.Status410Gone, "uniplus.selecao.cursor_expirado", "Cursor de paginação expirado")),
        new("Cursor.LimitInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.cursor_limit_invalido", "Tamanho de página inválido")),
    ];
}
