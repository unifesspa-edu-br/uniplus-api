namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.HealthChecks;

using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Persistence;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

using static CatalogoDeTiposAtoDeclarado;

/// <summary>
/// Acusa o tipo de ato que o catálogo declara e que o cadastro não sustenta, porque a versão
/// vigente hoje nega um dos atributos declarados.
/// </summary>
/// <remarks>
/// <para>
/// Os atributos decidem o que o ato determina, e cada um nasce falso em toda linha antiga quando
/// a coluna é acrescentada. O valor neutro desliga capacidade sem recusar nada: sem
/// <c>EhResultado</c> a publicação não recebe papel preliminar e a etapa não admite recurso; sem
/// <c>CongelaConfiguracao</c> publicar e retificar param; sem <c>EfeitoIrreversivel</c> uma
/// publicação que não deveria se desfazer passa a poder.
/// </para>
/// <para>
/// A conferência é assimétrica: acusa o atributo desligado onde o catálogo o declara, e não o
/// inverso. Desligar remove uma capacidade sem que nada apareça; ligar habilita uma com efeito
/// imediato para quem configura o certame, e a decisão sobre alguns códigos é do administrador,
/// por cadastro — alarmar ali obrigaria um deploy para silenciar um ato que o produto permite.
/// </para>
/// <para>
/// O que a conferência não acusa: o código declarado que não tem versão vigente no cadastro, e o
/// cadastro <b>inteiramente vazio</b>. Neste, não houve carga, não há com que comparar, e o seed é
/// quem reporta o que falta. Havendo qualquer linha, a carga já aconteceu — mas só o atributo
/// negado por uma versão vigente é acusado, e não a ausência da versão.
/// </para>
/// <para>
/// "Hoje" é o dia civil no fuso institucional, o mesmo que fixa a versão vigente de um ato na
/// publicação: pelo relógio UTC, entre 21h e 24h locais a conferência olharia o dia seguinte.
/// Quando a base de fusos do ambiente não reconhece o fuso institucional, a conferência degrada
/// e diz que não está sendo feita.
/// </para>
/// <para>
/// Degradado, nunca indisponível: é dado de cadastro, e o ambiente precisa continuar de pé
/// justamente para que alguém possa corrigi-lo. O <c>HealthCheckService</c> registra a descrição
/// em <c>Warning</c> quando a conferência degrada, e o host deixa esse nível passar — é por ali
/// que os códigos chegam a quem opera, já que o corpo de <c>/health</c> traz só o status. Falha de
/// leitura do cadastro não é tratada aqui: a exceção sobe ao <c>HealthCheckService</c>, que a
/// converte no status de falha do registro (degradado) com a exceção anexada.
/// </para>
/// </remarks>
internal sealed class CatalogoDeTiposAtoHealthCheck(
    UniPlusDbContext dbContext,
    TimeProvider timeProvider,
    IResolvedorFusoInstitucional fuso,
    IOptions<CatalogoDeTiposAtoOptions> opcoes) : IHealthCheck
{
    private const string ConferenciaDesligada =
        "Este ambiente não declara que recebe o catálogo de tipos de ato pelo bootstrap; "
        + "a conferência contra o cadastro está desligada.";

    private const string CatalogoIlegivel =
        "O catálogo declarado de tipos de ato não pôde ser lido do assembly — "
        + "a conferência contra o cadastro não está sendo feita.";

    private const string DiaDeHojeNaoCalculado =
        "O dia de hoje no fuso institucional não pôde ser calculado; "
        + "a conferência contra o cadastro não está sendo feita.";

    private const string CadastroSemCarga = "O cadastro de tipos de ato ainda não recebeu carga; nada a conferir.";

    private static readonly (Func<LinhaDeclarada, bool> Declarado, Func<CadastroDoTipo, bool> Cadastrado, string Frase)[] Atributos =
    [
        (linha => linha.EhResultado, cadastro => cadastro.EhResultado, "determina a situação do candidato"),
        (linha => linha.CongelaConfiguracao, cadastro => cadastro.CongelaConfiguracao, "congela a configuração"),
        (linha => linha.UnicoPorObjeto, cadastro => cadastro.UnicoPorObjeto, "único por objeto"),
        (linha => linha.EfeitoIrreversivel, cadastro => cadastro.EfeitoIrreversivel, "efeito irreversível"),
        (linha => linha.PublicavelComoAvulso, cadastro => cadastro.PublicavelComoAvulso, "publicável como documento avulso"),
        (linha => linha.ExigeAtoCorrigido, cadastro => cadastro.ExigeAtoCorrigido, "exige ato corrigido"),
    ];

    private static readonly Expression<Func<TipoAtoPublicado, CadastroDoTipo>> Projecao = tipo => new CadastroDoTipo(
        tipo.Codigo,
        tipo.CongelaConfiguracao,
        tipo.UnicoPorObjeto,
        tipo.EfeitoIrreversivel,
        tipo.EhResultado,
        tipo.PublicavelComoAvulso,
        tipo.ExigeAtoCorrigido);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!opcoes.Value.Conferir)
        {
            return HealthCheckResult.Healthy(ConferenciaDesligada);
        }

        if (!IsAvailable)
        {
            return HealthCheckResult.Degraded(CatalogoIlegivel);
        }

        Result<DateOnly> hoje = fuso.DiaCivilDe(timeProvider.GetUtcNow());
        return hoje.IsFailure
            ? HealthCheckResult.Degraded($"{DiaDeHojeNaoCalculado} {hoje.Error!.Message}")
            : await ConferirNoDiaAsync(hoje.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <remarks>
    /// Cadastro inteiramente vazio é ambiente que ainda não foi semeado, e não catálogo
    /// contraditório: não há com que comparar, e quem carrega o seed descobre o que falta pelo
    /// próprio seed, que falha alto. Acusar aqui deixaria degradado todo ambiente recém-criado —
    /// inclusive o de teste — e sinal que aparece sempre treina quem opera a ignorá-lo. O caso que
    /// motiva a conferência é o catálogo carregado <b>pela metade</b>, que é o que homologação tinha.
    /// </remarks>
    private async Task<HealthCheckResult> ConferirNoDiaAsync(DateOnly hoje, CancellationToken cancellationToken)
    {
        if (!await CadastroRecebeuCargaAsync(cancellationToken).ConfigureAwait(false))
        {
            return HealthCheckResult.Healthy(CadastroSemCarga);
        }

        LinhaDeclarada[] declaradas = VigentesEm(hoje);
        string[] codigos = [.. declaradas.Select(linha => linha.Codigo)];
        Dictionary<string, CadastroDoTipo> porCodigo =
            await LerCadastroVigenteAsync(hoje, codigos, cancellationToken).ConfigureAwait(false);

        return ResultadoDaConferencia(AtributosQueOCadastroNega(declaradas, porCodigo));
    }

    /// <remarks>
    /// A contagem fica fora da frase de propósito: o rótulo do atributo não concorda com ela, e um
    /// código só é o caso mais provável — um tipo acabou de ser cadastrado errado.
    /// </remarks>
    private static HealthCheckResult ResultadoDaConferencia(List<string> partes) =>
        partes.Count == 0
            ? HealthCheckResult.Healthy("O cadastro de tipos de ato sustenta o catálogo declarado.")
            : HealthCheckResult.Degraded(
                "O cadastro de tipos de ato contradiz o catálogo declarado. Atributos que o "
                + $"cadastro nega: {string.Join("; ", partes)}.");

    /// <remarks>
    /// A pergunta é sobre a tabela inteira, e não sobre os códigos declarados: um cadastro com
    /// linhas de outros códigos já recebeu carga, e ali a ausência de um declarado é informação.
    /// </remarks>
    private Task<bool> CadastroRecebeuCargaAsync(CancellationToken cancellationToken) =>
        dbContext.TiposAtoPublicado.AsNoTracking().AnyAsync(cancellationToken);

    private async Task<Dictionary<string, CadastroDoTipo>> LerCadastroVigenteAsync(
        DateOnly hoje, string[] codigos, CancellationToken cancellationToken)
    {
        List<CadastroDoTipo> cadastrados = await dbContext.TiposAtoPublicado
            .AsNoTracking()
            .Where(TipoAtoPublicado.VigenteEm(hoje))
            .Where(tipo => codigos.Contains(tipo.Codigo))
            .Select(Projecao)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return cadastrados.ToDictionary(tipo => tipo.Codigo, StringComparer.Ordinal);
    }

    private static List<string> AtributosQueOCadastroNega(
        LinhaDeclarada[] declaradas, Dictionary<string, CadastroDoTipo> porCodigo)
    {
        List<string> partes = [];

        foreach ((Func<LinhaDeclarada, bool> declarado, Func<CadastroDoTipo, bool> cadastrado, string frase) in Atributos)
        {
            string[] divergentes = CodigosQueNegam(declaradas, porCodigo, declarado, cadastrado);

            if (divergentes.Length > 0)
            {
                partes.Add($"{frase} ({string.Join(", ", divergentes)})");
            }
        }

        return partes;
    }

    private static string[] CodigosQueNegam(
        LinhaDeclarada[] declaradas,
        Dictionary<string, CadastroDoTipo> porCodigo,
        Func<LinhaDeclarada, bool> declarado,
        Func<CadastroDoTipo, bool> cadastrado) =>
    [
        .. declaradas
            .Where(linha => declarado(linha)
                && porCodigo.TryGetValue(linha.Codigo, out CadastroDoTipo? tipo)
                && !cadastrado(tipo))
            .Select(linha => linha.Codigo)
            .Order(StringComparer.Ordinal),
    ];

    private sealed record CadastroDoTipo(
        string Codigo,
        bool CongelaConfiguracao,
        bool UnicoPorObjeto,
        bool EfeitoIrreversivel,
        bool EhResultado,
        bool PublicavelComoAvulso,
        bool ExigeAtoCorrigido);
}
