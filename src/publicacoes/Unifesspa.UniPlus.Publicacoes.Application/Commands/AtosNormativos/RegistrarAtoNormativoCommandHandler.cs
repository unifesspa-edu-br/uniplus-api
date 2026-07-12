namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

using System.Globalization;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Abstractions;
using Unifesspa.UniPlus.Publicacoes.Application.Avisos;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// Handler do <see cref="RegistrarAtoNormativoCommand"/> (convention-based
/// Wolverine): resolve o tipo vigente na data de publicação, copia por valor os
/// atributos de consequência, registra o ato append-only e computa o aviso de
/// número duplicado (AC4).
/// </summary>
public static class RegistrarAtoNormativoCommandHandler
{
    public static async Task<Result<RegistrarAtoNormativoResult>> Handle(
        RegistrarAtoNormativoCommand command,
        ITipoAtoPublicadoRepository tiposRepository,
        IAtoNormativoRepository atosRepository,
        IPublicacoesUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(tiposRepository);
        ArgumentNullException.ThrowIfNull(atosRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        // AC6: sem versão do tipo vigente na data, não há de onde copiar os
        // atributos de consequência — recusa nomeada.
        TipoAtoPublicado? tipo = await tiposRepository
            .ObterVigenteAsync(command.TipoCodigo, command.DataPublicacao, cancellationToken)
            .ConfigureAwait(false);
        if (tipo is null)
        {
            return Result<RegistrarAtoNormativoResult>.Failure(new DomainError(
                AtoNormativoErrorCodes.TipoSemVersaoVigente,
                $"Não há versão vigente do tipo de ato '{command.TipoCodigo}' na data de publicação {command.DataPublicacao:yyyy-MM-dd}."));
        }

        // AC7: o par {id, hash} é recebido por valor, completo ou ausente.
        Result<ReferenciaVersaoConfiguracao>? versaoResult = ResolverVersaoInvocada(command);
        if (versaoResult is { IsFailure: true })
        {
            return Result<RegistrarAtoNormativoResult>.Failure(versaoResult.Error!);
        }

        // ADR-0103: quando o registro emenda outro ato, as invariantes da cadeia que
        // dependem do ato retificado (existência, classe de congelamento coincidente,
        // linearidade) são verificadas aqui, com o retificado em mãos.
        DomainError? retificacaoErro = await ValidarRetificacaoAsync(
            command, tipo, atosRepository, cancellationToken).ConfigureAwait(false);
        if (retificacaoErro is not null)
        {
            return Result<RegistrarAtoNormativoResult>.Failure(retificacaoErro);
        }

        // A linhagem a que este ato pertence: ele mesmo, quando não emenda ninguém; a
        // raiz da cadeia do ato retificado, quando emenda. É a chave da vaga do objeto —
        // uma retificação não disputa a vaga que a sua própria linhagem já ocupa.
        IReadOnlyList<Guid> idsDaPropriaCadeia = command.AtoRetificadoId is { } retificadoId
            ? await atosRepository.ListarIdsDaCadeiaAsync(retificadoId, cancellationToken).ConfigureAwait(false)
            : [];

        IReadOnlyList<(string EntidadeTipo, Guid EntidadeId)> vinculos = await ResolverVinculosAsync(
            command, atosRepository, cancellationToken).ConfigureAwait(false);

        AtoNormativo ato = AtoNormativo.Registrar(
            // Caminho HTTP: quem publica é o próprio operador em Publicações, e não há
            // domínio remoto a quem pertença decidir o id — Guid.Empty faz a factory
            // gerá-lo, como sempre fez.
            Guid.Empty,
            command.Orgao,
            command.Serie,
            command.Ano,
            command.Numero,
            command.TipoCodigo,
            tipo.CongelaConfiguracao,
            tipo.EfeitoIrreversivel,
            tipo.UnicoPorObjeto,
            command.DataPublicacao,
            command.DocumentoHash,
            command.Assinante,
            timeProvider.GetUtcNow(),
            versaoResult?.Value,
            command.AtoRetificadoId,
            command.MotivoRetificacao,
            vinculos);

        Guid raizDaLinhagem = command.AtoRetificadoId is { } paraRaiz
            ? await atosRepository.ObterRaizDaCadeiaAsync(paraRaiz, cancellationToken).ConfigureAwait(false)
            : ato.Id;

        DomainError? unicidadeErro = await ReservarVagasDoObjetoAsync(
            ato, idsDaPropriaCadeia, raizDaLinhagem, atosRepository, cancellationToken).ConfigureAwait(false);
        if (unicidadeErro is not null)
        {
            return Result<RegistrarAtoNormativoResult>.Failure(unicidadeErro);
        }

        // AC4: colisão de numeração é aviso, jamais recusa. No registro o próprio ato
        // ainda não existe, então todos os que compartilham a numeração são
        // conflitantes — menos a linhagem que este ato passa a integrar: uma
        // republicação com o mesmo número dentro da cadeia de retificação não é
        // duplicata, é a mesma linhagem (ADR-0103). Exclui a cadeia inteira do ato
        // retificado, não só o pai direto. O aviso é best-effort: como o número não
        // tem unicidade, dois registros concorrentes da mesma numeração podem ambos
        // observar zero conflitos; a consulta de detalhe recomputa e enxerga ambos.
        IReadOnlyList<AvisoNumeracao> avisos = await AvisoNumeracaoCalculator
            .CalcularAsync(atosRepository, ato, idsDaPropriaCadeia, cancellationToken)
            .ConfigureAwait(false);

        await atosRepository.AdicionarAsync(ato, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsLinearidadeConflict(constraint))
        {
            // Corrida check-then-act: dois atos tentaram retificar a mesma raiz ao
            // mesmo tempo. O índice único parcial deixou passar só um; o outro vira
            // recusa nomeada, não erro 500.
            return Result<RegistrarAtoNormativoResult>.Failure(new DomainError(
                AtoNormativoErrorCodes.RaizJaRetificada,
                RaizJaRetificadaConcorrenteMensagem(command.AtoRetificadoId)));
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsLinhagemUnicaConflict(constraint))
        {
            // Corrida check-then-act: outra linhagem reservou a vaga do objeto entre a
            // consulta acima e este commit. O índice único deixou passar só uma.
            return Result<RegistrarAtoNormativoResult>.Failure(new DomainError(
                AtoNormativoErrorCodes.ObjetoJaTemAtoVivoDoTipo,
                "A vaga do objeto vinculado foi ocupada por um ato registrado concorrentemente. "
                + "O tipo admite um único ato vivo por objeto — consulte os atos da entidade "
                + "antes de reenviar."));
        }

        return Result<RegistrarAtoNormativoResult>.Success(
            new RegistrarAtoNormativoResult(ato.Id, ato.RegistradoEm, avisos));
    }

    /// <summary>
    /// Resolve os vínculos do ato: os declarados no comando, mais — quando o ato emenda
    /// outro — os do ato retificado, que a retificação <b>herda</b>.
    /// </summary>
    /// <remarks>
    /// A herança não é conveniência: sem ela, uma retificação registrada sem repetir os
    /// vínculos sumiria da consulta do certame, e a consulta passaria a exibir a versão
    /// superada escondendo a que a emenda — o oposto do que o módulo existe para fazer.
    /// Quem emenda um ato trata do mesmo objeto que ele. Acrescentar entidades novas
    /// continua permitido: o ato retificador pode passar a tratar de um objeto que o
    /// retificado não declarava.
    /// </remarks>
    private static async Task<IReadOnlyList<(string EntidadeTipo, Guid EntidadeId)>> ResolverVinculosAsync(
        RegistrarAtoNormativoCommand command,
        IAtoNormativoRepository atosRepository,
        CancellationToken cancellationToken)
    {
        // O elemento nulo já foi recusado pelo validator (422); descartá-lo aqui evita
        // que um chamador interno — Selecao via ICommandBus, sem passar pelo HTTP —
        // derrube o handler com uma referência nula.
        List<(string EntidadeTipo, Guid EntidadeId)> declarados =
            [.. (command.Vinculos ?? [])
                .Where(v => v is not null)
                .Select(v => (v.EntidadeTipo.Trim(), v.EntidadeId))];

        if (command.AtoRetificadoId is not { } retificadoId)
        {
            return Ordenados(declarados);
        }

        IReadOnlyList<(string EntidadeTipo, Guid EntidadeId)> herdados = await atosRepository
            .ListarVinculosDoAtoAsync(retificadoId, cancellationToken)
            .ConfigureAwait(false);

        HashSet<(string, Guid)> vistos = [.. declarados];
        declarados.AddRange(herdados.Where(h => vistos.Add(h)));

        return Ordenados(declarados);
    }

    /// <summary>
    /// Ordem determinística das entidades, e não estética: as vagas do objeto são
    /// reservadas nesta ordem, e duas transações que reservem o mesmo par de objetos em
    /// ordens opostas travariam uma na outra.
    /// </summary>
    private static IReadOnlyList<(string EntidadeTipo, Guid EntidadeId)> Ordenados(
        List<(string EntidadeTipo, Guid EntidadeId)> vinculos) =>
        [.. vinculos
            .OrderBy(v => v.EntidadeTipo, StringComparer.Ordinal)
            .ThenBy(v => v.EntidadeId)];

    /// <summary>
    /// Reserva, para cada entidade vinculada, a vaga do objeto em nome da linhagem do
    /// ato — quando o tipo admite um único ato vivo por objeto (ADR-0107). Devolve o
    /// <see cref="DomainError"/> da primeira colisão, ou <see langword="null"/> quando o
    /// tipo não é único por objeto, o ato não vincula entidade alguma, ou todas as vagas
    /// foram reservadas.
    /// </summary>
    /// <remarks>
    /// São duas verificações, e cada uma cobre o que a outra não vê:
    /// <list type="number">
    ///   <item>o <b>histórico de atos</b> — um ato do mesmo tipo já vinculado ao objeto,
    ///     fora desta linhagem, colide. Enxerga inclusive o ato publicado quando o tipo
    ///     ainda não era único por objeto (o catálogo é editável) e que, por isso, não
    ///     reservou vaga nenhuma;</item>
    ///   <item>a <b>vaga</b> — o índice único que o banco trava, e que fecha a corrida
    ///     entre duas transações concorrentes, invisível a qualquer consulta.</item>
    /// </list>
    /// A vaga já ocupada <i>pela própria linhagem</i> não é reservada de novo: a segunda
    /// versão de um ato vivo é uma retificação, e a vaga é da linhagem, não do ato.
    /// </remarks>
    private static async Task<DomainError?> ReservarVagasDoObjetoAsync(
        AtoNormativo ato,
        IReadOnlyList<Guid> idsDaPropriaCadeia,
        Guid raizDaLinhagem,
        IAtoNormativoRepository atosRepository,
        CancellationToken cancellationToken)
    {
        if (!ato.UnicoPorObjeto || ato.Vinculos.Count == 0)
        {
            return null;
        }

        // O próprio ato ainda não está gravado, mas já integra a linhagem: incluí-lo
        // evita que ele se veja como conflito de si mesmo em qualquer consulta futura.
        Guid[] daLinhagem = [.. idsDaPropriaCadeia, ato.Id];

        // Duas fases, e a ordem importa: nada é reservado enquanto houver um vínculo por
        // examinar. Reservar durante o exame deixaria, num ato com dois objetos em que só
        // o segundo conflita, a vaga do primeiro pendurada na unidade de trabalho — e a
        // recusa do ato inteiro não a desfaria por si só.
        List<LinhagemUnicaPorObjeto> aReservar = [];

        foreach (VinculoAtoEntidade vinculo in ato.Vinculos)
        {
            AtoNormativo? conflitante = await atosRepository
                .ObterAtoConflitanteNoObjetoAsync(
                    vinculo.EntidadeTipo, vinculo.EntidadeId, ato.TipoCodigo, daLinhagem, cancellationToken)
                .ConfigureAwait(false);
            if (conflitante is not null)
            {
                return new DomainError(
                    AtoNormativoErrorCodes.ObjetoJaTemAtoVivoDoTipo,
                    ObjetoJaTemAtoVivoMensagem(vinculo, ato.TipoCodigo, conflitante));
            }

            LinhagemUnicaPorObjeto? vaga = await atosRepository
                .ObterLinhagemDoObjetoAsync(
                    vinculo.EntidadeTipo, vinculo.EntidadeId, ato.TipoCodigo, cancellationToken)
                .ConfigureAwait(false);

            if (vaga is null)
            {
                aReservar.Add(LinhagemUnicaPorObjeto.Criar(ato, vinculo, raizDaLinhagem));
                continue;
            }

            // A vaga existe: só a própria linhagem pode reencontrá-la — a consulta ao
            // histórico já teria barrado qualquer outra, e esta é a rede de segurança.
            if (vaga.RaizId != raizDaLinhagem)
            {
                return new DomainError(
                    AtoNormativoErrorCodes.ObjetoJaTemAtoVivoDoTipo,
                    ObjetoJaTemAtoVivoPorLinhagemMensagem(vinculo, ato.TipoCodigo, vaga));
            }
        }

        foreach (LinhagemUnicaPorObjeto vaga in aReservar)
        {
            await atosRepository.AdicionarLinhagemAsync(vaga, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private static string ObjetoJaTemAtoVivoMensagem(
        VinculoAtoEntidade vinculo, string tipoCodigo, AtoNormativo conflitante) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "O tipo de ato '{0}' admite um único ato vivo por objeto, e a entidade {1}/{2} já é "
            + "tratada pelo ato {3}. Para publicar outra versão, retifique a linhagem existente.",
            tipoCodigo,
            vinculo.EntidadeTipo,
            vinculo.EntidadeId,
            conflitante.Id);

    private static string ObjetoJaTemAtoVivoPorLinhagemMensagem(
        VinculoAtoEntidade vinculo, string tipoCodigo, LinhagemUnicaPorObjeto vaga) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "O tipo de ato '{0}' admite um único ato vivo por objeto, e a entidade {1}/{2} já é "
            + "tratada pela linhagem de atos que nasce em {3}. Para publicar outra versão, "
            + "retifique essa linhagem.",
            tipoCodigo,
            vinculo.EntidadeTipo,
            vinculo.EntidadeId,
            vaga.RaizId);

    /// <summary>
    /// Verifica as invariantes da cadeia de retificação que dependem do ato
    /// retificado (ADR-0103). Devolve o <see cref="DomainError"/> da primeira
    /// violação, ou <see langword="null"/> quando o registro não é retificação ou
    /// passa em todas. A garantia dura da linearidade contra a corrida é do índice
    /// único parcial; esta consulta dá a mensagem legível no caso comum.
    /// </summary>
    private static async Task<DomainError?> ValidarRetificacaoAsync(
        RegistrarAtoNormativoCommand command,
        TipoAtoPublicado tipo,
        IAtoNormativoRepository atosRepository,
        CancellationToken cancellationToken)
    {
        if (command.AtoRetificadoId is not { } retificadoId)
        {
            return null;
        }

        AtoNormativo? retificado = await atosRepository
            .ObterPorIdParaLeituraAsync(retificadoId, cancellationToken)
            .ConfigureAwait(false);
        if (retificado is null)
        {
            return new DomainError(
                AtoNormativoErrorCodes.AtoRetificadoNaoEncontrado,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "O ato retificado {0} não corresponde a nenhum ato registrado.",
                    retificadoId));
        }

        // congela(retificador) == congela(retificado): a classe de congelamento do
        // novo ato é a do tipo vigente já resolvido; a do retificado é o snapshot que
        // ele congelou no próprio registro. É essa classe, não o rótulo do tipo, que
        // protege a integridade da configuração publicada (RN08).
        if (retificado.CongelaConfiguracao != tipo.CongelaConfiguracao)
        {
            return new DomainError(
                AtoNormativoErrorCodes.ClasseDeCongelamentoDivergente,
                "A classe de congelamento do ato que retifica deve coincidir com a do ato retificado: "
                + "um ato não congelante não emenda um congelante, nem o inverso.");
        }

        // Linearidade: se a raiz já foi retificada, a nova retificação deveria emendar
        // a cabeça da cadeia, não a raiz. Nomeia quem já a retificou.
        AtoNormativo? retificador = await atosRepository
            .ObterRetificadorAsync(retificadoId, cancellationToken)
            .ConfigureAwait(false);
        if (retificador is not null)
        {
            return new DomainError(
                AtoNormativoErrorCodes.RaizJaRetificada,
                RaizJaRetificadaMensagem(retificadoId, retificador));
        }

        return null;
    }

    private static string RaizJaRetificadaMensagem(Guid retificadoId, AtoNormativo retificador)
    {
        string numero = retificador.Numero is null ? "sem número" : "nº " + retificador.Numero;
        return string.Format(
            CultureInfo.InvariantCulture,
            "O ato {0} já foi retificado pelo ato {1} ({2} {3}/{4}). A cadeia é linear: "
            + "retifique a cabeça da cadeia, não uma raiz já retificada.",
            retificadoId,
            retificador.Id,
            retificador.Serie,
            numero,
            retificador.Ano);
    }

    private static string RaizJaRetificadaConcorrenteMensagem(Guid? retificadoId) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "O ato {0} já foi retificado por outro ato registrado concorrentemente. "
            + "A cadeia é linear: retifique a cabeça da cadeia.",
            retificadoId);

    private static Result<ReferenciaVersaoConfiguracao>? ResolverVersaoInvocada(
        RegistrarAtoNormativoCommand command)
    {
        bool temId = command.VersaoInvocadaId.HasValue;
        bool temHash = command.VersaoInvocadaHash is not null;

        if (!temId && !temHash)
        {
            return null;
        }

        if (temId != temHash)
        {
            return Result<ReferenciaVersaoConfiguracao>.Failure(new DomainError(
                AtoNormativoErrorCodes.VersaoInvocadaIncompleta,
                AtoNormativoRegras.VersaoInvocadaIncompleta));
        }

        Result<ReferenciaVersaoConfiguracao> versao = ReferenciaVersaoConfiguracao.Criar(
            command.VersaoInvocadaId!.Value, command.VersaoInvocadaHash!);

        // O validator já rejeita id vazio e hash malformado; se ainda assim a
        // factory recusar (defesa em profundidade), traduz para um código mapeado
        // em vez de vazar o erro do value object como "erro não mapeado".
        return versao.IsFailure
            ? Result<ReferenciaVersaoConfiguracao>.Failure(new DomainError(
                AtoNormativoErrorCodes.VersaoInvocadaIncompleta, versao.Error!.Message))
            : versao;
    }
}
