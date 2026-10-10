namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Globalization;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// Lê o próximo valor da sequência nativa <see cref="SelecaoDbContext.SequenciaDeNumeroDeInscricao"/>
/// (ADR-0121 não se aplica aqui: número de inscrição não é dado pessoal) e formata em 8 dígitos —
/// decisão de apresentação, não de domínio; o valor numérico cru nunca vaza sem o preenchimento.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em SelecaoInfrastructureRegistration.")]
public sealed class GeradorDeNumeroInscricao : IGeradorDeNumeroInscricao
{
    private readonly SelecaoDbContext _dbContext;

    public GeradorDeNumeroInscricao(SelecaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    // Nomes de schema e de sequência são constantes de compilação, nunca entrada externa — sem
    // risco de injeção. SqlQueryRaw (não interpolado) evita a armadilha de SqlQuery<T>
    // parametrizar um buraco de interpolação que está DENTRO de aspas simples do SQL: o texto
    // resultante vira a string literal "@p0" em vez do valor, e nextval('@p0') nunca resolve.
    private static readonly string ComandoProximoValor =
        $"SELECT nextval('{SelecaoDbContext.Schema}.{SelecaoDbContext.SequenciaDeNumeroDeInscricao}') AS \"Value\"";

    public async Task<string> ProximoAsync(CancellationToken cancellationToken = default)
    {
        long proximo = await _dbContext.Database
            .SqlQueryRaw<long>(ComandoProximoValor)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        return proximo.ToString("D8", CultureInfo.InvariantCulture);
    }
}
