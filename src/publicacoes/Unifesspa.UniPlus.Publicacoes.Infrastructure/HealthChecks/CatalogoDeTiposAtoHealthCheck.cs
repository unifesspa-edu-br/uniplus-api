namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.HealthChecks;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Persistence;

using static CatalogoDeTiposAtoDeclarado;

/// <summary>
/// Acusa o tipo de ato que o catálogo declara e que o cadastro não sustenta — porque nega um dos
/// atributos declarados, ou porque não tem versão vigente nenhuma.
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
/// Ausência pesa igual: o ato declarado que não tem versão vigente não pode ser publicado, e a
/// etapa que dependeria dele fica sem recurso do mesmo jeito. O que a conferência não acusa é o
/// cadastro <b>inteiramente vazio</b> — ali não houve carga, não há com que comparar, e o seed é
/// quem reporta o que falta. Havendo qualquer linha, a carga já aconteceu, e a ausência de um
/// código declarado passa a ser informação: é o catálogo carregado pela metade que ela existe
/// para pegar.
/// </para>
/// <para>
/// Degradado, nunca indisponível: é dado de cadastro, e o ambiente precisa continuar de pé
/// justamente para que alguém possa corrigi-lo. O <c>HealthCheckService</c> registra a descrição
/// em <c>Warning</c> quando a conferência degrada, e o host deixa esse nível passar — é por ali
/// que os códigos chegam a quem opera, já que o corpo de <c>/health</c> traz só o status.
/// </para>
/// </remarks>
internal sealed class CatalogoDeTiposAtoHealthCheck(
    PublicacoesDbContext dbContext,
    TimeProvider timeProvider,
    IOptions<CatalogoDeTiposAtoOptions> opcoes) : IHealthCheck
{
    private static readonly (Func<LinhaDeclarada, bool> Declarado, Func<CadastroDoTipo, bool> Cadastrado, string Frase)[] Atributos =
    [
        (l => l.EhResultado, c => c.EhResultado, "determina a situação do candidato"),
        (l => l.CongelaConfiguracao, c => c.CongelaConfiguracao, "congela a configuração"),
        (l => l.UnicoPorObjeto, c => c.UnicoPorObjeto, "único por objeto"),
        (l => l.EfeitoIrreversivel, c => c.EfeitoIrreversivel, "efeito irreversível"),
    ];

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Conferência de diagnóstico: uma falha de leitura do cadastro precisa virar "
            + "mensagem degradada distinta, não exceção sem descrição no relatório de saúde.")]
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!opcoes.Value.Conferir)
        {
            return HealthCheckResult.Healthy(
                "Este ambiente não declara que recebe o catálogo de tipos de ato pelo bootstrap; "
                + "a conferência contra o cadastro está desligada.");
        }

        if (!IsAvailable)
        {
            return HealthCheckResult.Degraded(
                "O catálogo declarado de tipos de ato não pôde ser lido do assembly — "
                + "a conferência contra o cadastro não está sendo feita.");
        }

        DateOnly hoje = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        LinhaDeclarada[] declaradas = VigentesEm(hoje);
        string[] codigosDeclarados = [.. declaradas.Select(linha => linha.Codigo)];

        bool cadastroVazio;
        List<CadastroDoTipo> cadastrados;
        try
        {
            // A pergunta é sobre a tabela inteira, e não sobre os códigos declarados: um cadastro
            // com linhas de outros códigos já recebeu carga, e ali a ausência de um declarado é
            // informação. Vazio de tudo é ambiente que ainda não foi semeado.
            cadastroVazio = !await dbContext.TiposAtoPublicado
                .AsNoTracking()
                .AnyAsync(cancellationToken)
                .ConfigureAwait(false);

            cadastrados = await dbContext.TiposAtoPublicado
                .AsNoTracking()
                .Where(tipo => tipo.VigenciaInicio <= hoje
                    && (tipo.VigenciaFim == null || tipo.VigenciaFim > hoje)
                    && codigosDeclarados.Contains(tipo.Codigo))
                .Select(tipo => new CadastroDoTipo(
                    tipo.Codigo,
                    tipo.CongelaConfiguracao,
                    tipo.UnicoPorObjeto,
                    tipo.EfeitoIrreversivel,
                    tipo.EhResultado))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            return HealthCheckResult.Degraded(
                "Não foi possível ler o cadastro de tipos de ato para conferi-lo contra o catálogo declarado.",
                excecao);
        }

        // Cadastro inteiramente vazio é ambiente que ainda não foi semeado, e não catálogo
        // contraditório: não há com que comparar, e quem carrega o seed descobre o que falta pelo
        // próprio seed, que falha alto. Acusar aqui deixaria degradado todo ambiente recém-criado
        // — inclusive o de teste — e sinal que aparece sempre treina quem opera a ignorá-lo. O
        // caso que motiva a conferência é o catálogo carregado **pela metade**, que é o que
        // homologação tinha.
        if (cadastroVazio)
        {
            return HealthCheckResult.Healthy(
                "O cadastro de tipos de ato ainda não recebeu carga; nada a conferir.");
        }

        Dictionary<string, CadastroDoTipo> porCodigo =
            cadastrados.ToDictionary(tipo => tipo.Codigo, StringComparer.Ordinal);

        List<string> partes = [];

        foreach ((Func<LinhaDeclarada, bool> declarado, Func<CadastroDoTipo, bool> cadastrado, string frase) in Atributos)
        {
            IReadOnlyList<string> divergentes =
            [
                .. declaradas
                    .Where(linha => declarado(linha)
                        && porCodigo.TryGetValue(linha.Codigo, out CadastroDoTipo? tipo)
                        && !cadastrado(tipo))
                    .Select(linha => linha.Codigo)
                    .Order(StringComparer.Ordinal),
            ];

            if (divergentes.Count > 0)
            {
                partes.Add($"{frase} ({string.Join(", ", divergentes)})");
            }
        }

        // A contagem fica fora da frase de propósito: o rótulo do atributo não concorda com ela,
        // e um código só é o caso mais provável — um tipo acabou de ser cadastrado errado.
        return partes.Count == 0
            ? HealthCheckResult.Healthy("O cadastro de tipos de ato sustenta o catálogo declarado.")
            : HealthCheckResult.Degraded(
                "O cadastro de tipos de ato contradiz o catálogo declarado. Atributos que o "
                + $"cadastro nega: {string.Join("; ", partes)}.");
    }

    private sealed record CadastroDoTipo(
        string Codigo,
        bool CongelaConfiguracao,
        bool UnicoPorObjeto,
        bool EfeitoIrreversivel,
        bool EhResultado);
}
