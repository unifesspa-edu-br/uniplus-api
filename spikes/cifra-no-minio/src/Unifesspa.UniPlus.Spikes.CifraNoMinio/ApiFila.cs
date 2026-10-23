namespace Unifesspa.UniPlus.Spikes.CifraNoMinio;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Wolverine.EntityFrameworkCore;

/// <summary>
/// A confirmação do envio no fluxo com fila: a API grava o arquivo como "em verificação" e publica o
/// pedido de verificação pelo outbox do Wolverine, na mesma transação, e depois consome o veredicto.
/// Nada aqui lê o objeto: a API só conhece a chave.
/// </summary>
public static class ApiFila
{
    public const string EmVerificacao = "EM_VERIFICACAO";
    private const int LoteDaReconciliacao = 500;

    public static async Task PrepararAsync(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        await using AsyncServiceScope escopo = app.Services.CreateAsyncScope();
        Banco banco = escopo.ServiceProvider.GetRequiredService<Banco>();
        await banco.Database.ExecuteSqlRawAsync("""
            CREATE SCHEMA IF NOT EXISTS poc;
            CREATE TABLE IF NOT EXISTS poc.arquivo_enviado (
                "Chave" text PRIMARY KEY,
                "Estado" text NOT NULL,
                "ConfirmadoEm" timestamptz NOT NULL,
                "PublicadoEm" timestamptz NOT NULL,
                "ETag" text NOT NULL,
                "InicioDaVerificacaoEm" timestamptz,
                "FimDaVerificacaoEm" timestamptz,
                "RegistradoEm" timestamptz,
                "VerificacaoMs" double precision,
                "Veredicto" text,
                "Sha256" text,
                "Bytes" bigint,
                "Verificador" text,
                "Veredictos" integer NOT NULL DEFAULT 0,
                "Publicacoes" integer NOT NULL DEFAULT 0);
            ALTER TABLE poc.arquivo_enviado ADD COLUMN IF NOT EXISTS "PublicadoEm" timestamptz NOT NULL DEFAULT now();
            ALTER TABLE poc.arquivo_enviado ADD COLUMN IF NOT EXISTS "ETag" text NOT NULL DEFAULT '';
            DROP INDEX IF EXISTS poc.ix_arquivo_enviado_estado;
            CREATE INDEX IF NOT EXISTS ix_arquivo_enviado_estado_publicacao ON poc.arquivo_enviado ("Estado", "PublicadoEm");
            """);
    }

    public static void Mapear(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // A confirmação fixa a versão: o candidato manda o ETag que o PUT lhe devolveu, e só essa versão
        // é verificada. Confirmar de novo o mesmo arquivo não publica outro pedido, nem quando as duas
        // confirmações chegam juntas: a chave primária barra a segunda, que devolve o estado gravado.
        // Outra versão na mesma chave é recusada: substituir o arquivo é um novo envio, com chave nova.
        app.MapPost("/confirmar/{chave}", async (string chave, string? etag, IDbContextOutbox<Banco> outbox, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(etag))
            {
                return Results.BadRequest(new { erro = "o ETag devolvido pelo PUT é obrigatório" });
            }

            if (await ConfirmacaoAsync(outbox.DbContext, chave, cancellationToken) is { } existente)
            {
                return Reconfirmacao(existente, etag);
            }

            DateTimeOffset agora = DateTimeOffset.UtcNow;
            outbox.DbContext.Arquivos.Add(new ArquivoEnviado { Chave = chave, ETag = etag, Estado = EmVerificacao, ConfirmadoEm = agora, PublicadoEm = agora, Publicacoes = 1 });
            await outbox.PublishAsync(new VerificarArquivo(chave, etag, agora));
            try
            {
                await outbox.SaveChangesAndFlushMessagesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                outbox.DbContext.ChangeTracker.Clear();
                return Reconfirmacao((await ConfirmacaoAsync(outbox.DbContext, chave, cancellationToken))!, etag);
            }

            return Results.Accepted(value: new { Estado = EmVerificacao });
        });

        app.MapGet("/arquivos/{chave}/estado", async (string chave, Banco banco, CancellationToken cancellationToken) =>
            await banco.Arquivos.AsNoTracking().FirstOrDefaultAsync(a => a.Chave == chave, cancellationToken) is { } arquivo
                ? Results.Ok(arquivo)
                : Results.NotFound());

        app.MapGet("/arquivos/resumo", async (Banco banco, CancellationToken cancellationToken) =>
            Results.Ok(await banco.Arquivos.AsNoTracking()
                .GroupBy(a => a.Estado)
                .Select(g => new { Estado = g.Key, Arquivos = g.Count(), Veredictos = g.Sum(a => a.Veredictos), Publicacoes = g.Sum(a => a.Publicacoes) })
                .ToListAsync(cancellationToken)));

        app.MapDelete("/arquivos", async (Banco banco, CancellationToken cancellationToken) =>
            Results.Ok(await banco.Arquivos.ExecuteDeleteAsync(cancellationToken)));

        // A rede de segurança: o que está em verificação sem pedido publicado há mais de N segundos
        // ganha um novo pedido. Contar da última publicação, e não da confirmação, evita republicar a
        // cada execução o arquivo cujo pedido ainda está nas novas tentativas do verificador.
        // Em produção roda periodicamente, numa instância só e em lotes; aqui é chamada pelos roteiros
        // das provas. Um pedido publicado duas vezes não faz mal: o verificador é idempotente.
        app.MapPost("/reconciliar", async (int segundos, IDbContextOutbox<Banco> outbox, CancellationToken cancellationToken) =>
        {
            DateTimeOffset agora = DateTimeOffset.UtcNow;
            DateTimeOffset limite = agora.AddSeconds(-segundos);
            List<ArquivoEnviado> pendentes = await outbox.DbContext.Arquivos
                .Where(a => a.Estado == EmVerificacao && a.PublicadoEm <= limite)
                .OrderBy(a => a.PublicadoEm)
                .Take(LoteDaReconciliacao)
                .ToListAsync(cancellationToken);
            foreach (ArquivoEnviado arquivo in pendentes)
            {
                arquivo.Publicacoes++;
                arquivo.PublicadoEm = agora;
                await outbox.PublishAsync(new VerificarArquivo(arquivo.Chave, arquivo.ETag, arquivo.ConfirmadoEm));
            }

            await outbox.SaveChangesAndFlushMessagesAsync(cancellationToken);
            return Results.Ok(new { republicados = pendentes.Count });
        });
    }

    private static Task<ArquivoEnviado?> ConfirmacaoAsync(Banco banco, string chave, CancellationToken cancellationToken) =>
        banco.Arquivos.AsNoTracking().FirstOrDefaultAsync(a => a.Chave == chave, cancellationToken);

    private static IResult Reconfirmacao(ArquivoEnviado confirmado, string etag) =>
        string.Equals(confirmado.ETag.Trim('"'), etag.Trim('"'), StringComparison.OrdinalIgnoreCase)
            ? Results.Ok(new { confirmado.Estado })
            : Results.Conflict(new { erro = "esta chave já foi confirmada com outra versão; um novo arquivo é um novo envio, com chave nova" });
}

/// <summary>
/// O veredicto chega pela fila de resultados. Só o primeiro muda o estado; os repetidos (entrega
/// duplicada, reconciliação de um arquivo que já tinha sido verificado) só são contados. As duas
/// gravações são atômicas no banco, então dois veredictos simultâneos para a mesma chave não perdem
/// a contagem nem sobrescrevem o primeiro.
/// </summary>
public sealed class ResultadoDaVerificacaoHandler
{
    public static async Task Handle(ArquivoVerificado resultado, Banco banco, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(banco);

        await banco.Arquivos
            .Where(a => a.Chave == resultado.Chave)
            .ExecuteUpdateAsync(a => a.SetProperty(x => x.Veredictos, x => x.Veredictos + 1), cancellationToken);

        DateTimeOffset agora = DateTimeOffset.UtcNow;
        string estado = Estado(resultado.Veredicto);
        await banco.Arquivos
            .Where(a => a.Chave == resultado.Chave && a.Estado == ApiFila.EmVerificacao)
            .ExecuteUpdateAsync(
                a => a
                    .SetProperty(x => x.Estado, estado)
                    .SetProperty(x => x.Veredicto, resultado.Veredicto)
                    .SetProperty(x => x.Sha256, resultado.Sha256)
                    .SetProperty(x => x.Bytes, resultado.Bytes)
                    .SetProperty(x => x.InicioDaVerificacaoEm, resultado.InicioEm)
                    .SetProperty(x => x.FimDaVerificacaoEm, resultado.ConcluidoEm)
                    .SetProperty(x => x.VerificacaoMs, resultado.VerificacaoMs)
                    .SetProperty(x => x.Verificador, resultado.Verificador)
                    .SetProperty(x => x.RegistradoEm, agora),
                cancellationToken);
    }

    // NAO_ENCONTRADO não é recusa da verificação: a versão confirmada não chegou, foi substituída ou
    // expirou na quarentena, e o candidato precisa enviar de novo. O estado final encerra a reconciliação desse arquivo.
    private static string Estado(string veredicto) => veredicto switch
    {
        "LIMPO" => "VERIFICADO",
        "INFECTADO" or "FORA_DO_LIMITE" => "RECUSADO",
        _ => "REENVIO_NECESSARIO",
    };
}
