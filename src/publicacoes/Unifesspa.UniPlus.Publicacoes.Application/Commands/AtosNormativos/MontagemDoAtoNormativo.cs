namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// Monta o <see cref="AtoNormativo"/> de um <see cref="RegistrarAtoNormativoCommand"/>: confere o
/// que o registro exige, resolve o tipo e a versão de configuração, aplica a cadeia de
/// retificação e reserva as vagas do objeto. Não grava o ato: quem chama o adiciona e salva.
/// </summary>
public sealed class MontagemDoAtoNormativo(
    ITipoAtoPublicadoRepository tipos,
    IResolvedorFusoInstitucional fuso,
    CadeiaDeRetificacaoDoAto cadeia,
    ReservaDasVagasDoObjeto reservaDasVagas)
{
    public async Task<Result<AtoMontado>> MontarAsync(
        RegistrarAtoNormativoCommand command, DateTimeOffset publicadoEm, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<DadosConferidos> conferidos = await ConferirAsync(command, publicadoEm, cancellationToken).ConfigureAwait(false);
        if (conferidos.IsFailure)
        {
            return Result<AtoMontado>.Failure(conferidos.Error!);
        }

        AtoNormativo ato = Criar(command, conferidos.Value!, publicadoEm);
        DomainError? recusa = await ReservarAsync(command, ato, conferidos.Value!, cancellationToken).ConfigureAwait(false);

        return recusa is null
            ? Result<AtoMontado>.Success(new AtoMontado(ato, conferidos.Value!.Linhagem.IdsDaPropriaCadeia))
            : Result<AtoMontado>.Failure(recusa);
    }

    /// <summary>
    /// O ato que trata de objeto cujos atos pertencem ao domínio dele não nasce pelo endpoint:
    /// este registro não recebe o arquivo, e o ato ficaria sem documento.
    /// </summary>
    private static DomainError? VinculoDeRegistroPeloDominio(IEnumerable<string> entidadesTipo) =>
        entidadesTipo
            .Select(t => t.Trim())
            .Where(EntidadesDeRegistroPeloDominio.Contem)
            .Select(RegistroPeloDominioExigido)
            .FirstOrDefault();

    private static DomainError RegistroPeloDominioExigido(string entidadeTipo) =>
        new(
            AtoNormativoErrorCodes.VinculoExigeRegistroPeloDominio,
            $"Os atos do objeto vinculado ('{entidadeTipo}') são registrados pelo seu próprio domínio, "
            + "inclusive a retificação de um ato desse objeto: este endpoint não recebe o arquivo do "
            + "documento, e o ato ficaria sem documento. Publique o ato pelo módulo dono do objeto "
            + "(a Seleção, no caso do processo seletivo).");

    private static (string Nome, string TipoMidia, string Hash)[] AnexosConferiveis(RegistrarAtoNormativoCommand command) =>
        [.. (command.Anexos ?? []).Select(a => (a.Nome, a.TipoMidia, a.Hash))];

    /// <summary>
    /// Os anexos são conferidos antes de qualquer ida ao banco: a recusa não depende de estado
    /// gravado, e o registro por mensagem durável, que não passa pelo validator, recebe a mesma
    /// recusa nomeada. A recusa do vínculo declarado vem antes de tudo.
    /// </summary>
    private static DomainError? RecusaDaEntrada(RegistrarAtoNormativoCommand command)
    {
        if (!command.RegistroPeloDominio
            && VinculoDeRegistroPeloDominio(CadeiaDeRetificacaoDoAto.VinculosDeclarados(command).Select(v => v.EntidadeTipo))
                is { } declarado)
        {
            return declarado;
        }

        return (command.Anexos ?? []).Any(a => a is null)
            ? new DomainError(AnexoDoAtoErrorCodes.Nulo, "A lista de anexos não admite elemento nulo.")
            : AnexoDoAto.Conferir(AnexosConferiveis(command));
    }

    /// <summary>
    /// Os atributos de consequência do tipo ou vêm CONFERIDOS pelo domínio que publicou (registro
    /// por mensagem durável, ADR-0108/0061), ou se resolvem agora no catálogo (registro pelo
    /// endpoint, em que quem publica está aqui, neste instante). Quando vêm conferidos o catálogo
    /// NÃO é relido: o cadastro é editável, e reler faria um edital publicado como congelante ser
    /// registrado com um tipo que já não congela. O nome do tipo segue o mesmo caminho, porque é
    /// ele que compõe o título público do ato. A versão vigente é a do dia civil da publicação no fuso
    /// institucional: a data que o documento declara não escolhe o tipo.
    /// </summary>
    private async Task<Result<TipoResolvido>> ResolverTipoAsync(
        RegistrarAtoNormativoCommand command, DateTimeOffset publicadoEm, CancellationToken cancellationToken)
    {
        if (command.AtributosDoTipo is { } conferidos)
        {
            return Result<TipoResolvido>.Success(new TipoResolvido(command.TipoCodigo.Trim(), conferidos));
        }

        Result<DateOnly> diaDaPublicacao = fuso.DiaCivilDe(publicadoEm);
        return diaDaPublicacao.IsFailure
            ? Result<TipoResolvido>.Failure(diaDaPublicacao.Error!)
            : await ResolverNoCatalogoAsync(command.TipoCodigo, diaDaPublicacao.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<TipoResolvido>> ResolverNoCatalogoAsync(
        string tipoCodigo, DateOnly diaDaPublicacao, CancellationToken cancellationToken)
    {
        TipoAtoPublicado? tipo = await tipos
            .ObterVigenteAsync(tipoCodigo, diaDaPublicacao, cancellationToken)
            .ConfigureAwait(false);

        return tipo is null
            ? Result<TipoResolvido>.Failure(new DomainError(
                AtoNormativoErrorCodes.TipoSemVersaoVigente,
                $"Não há versão vigente do tipo de ato '{tipoCodigo}' na data da publicação ({diaDaPublicacao:yyyy-MM-dd})."))
            : Result<TipoResolvido>.Success(new TipoResolvido(tipo.Codigo, AtributosDe(tipo)));
    }

    private static AtributosDoTipoAto AtributosDe(TipoAtoPublicado tipo) =>
        new(tipo.CongelaConfiguracao, tipo.UnicoPorObjeto, tipo.EfeitoIrreversivel, tipo.Nome);

    /// <summary>
    /// O par {id, hash} é recebido por valor, completo ou ausente. Ausente devolve
    /// <see langword="null"/>: o ato não invoca configuração.
    /// </summary>
    private static Result<ReferenciaVersaoConfiguracao>? ResolverVersaoInvocada(RegistrarAtoNormativoCommand command)
    {
        bool temId = command.VersaoInvocadaId.HasValue;
        if (!temId && command.VersaoInvocadaHash is null)
        {
            return null;
        }

        return temId != (command.VersaoInvocadaHash is not null)
            ? Result<ReferenciaVersaoConfiguracao>.Failure(new DomainError(
                AtoNormativoErrorCodes.VersaoInvocadaIncompleta, AtoNormativoRegras.VersaoInvocadaIncompleta))
            : CriarReferenciaDaVersao(command.VersaoInvocadaId!.Value, command.VersaoInvocadaHash!);
    }

    /// <summary>
    /// O validator já rejeita id vazio e hash malformado; se ainda assim a factory recusar, a
    /// recusa é traduzida para um código mapeado em vez de vazar o erro do value object como
    /// "erro não mapeado".
    /// </summary>
    private static Result<ReferenciaVersaoConfiguracao> CriarReferenciaDaVersao(Guid id, string hash)
    {
        Result<ReferenciaVersaoConfiguracao> versao = ReferenciaVersaoConfiguracao.Criar(id, hash);
        return versao.IsFailure
            ? Result<ReferenciaVersaoConfiguracao>.Failure(new DomainError(
                AtoNormativoErrorCodes.VersaoInvocadaIncompleta, versao.Error!.Message))
            : versao;
    }

    private async Task<Result<DadosConferidos>> ConferirAsync(
        RegistrarAtoNormativoCommand command, DateTimeOffset publicadoEm, CancellationToken cancellationToken)
    {
        if (RecusaDaEntrada(command) is { } recusa)
        {
            return Result<DadosConferidos>.Failure(recusa);
        }

        Result<TipoResolvido> tipo = await ResolverTipoAsync(command, publicadoEm, cancellationToken).ConfigureAwait(false);
        return tipo.IsFailure
            ? Result<DadosConferidos>.Failure(tipo.Error!)
            : await ConferirVersaoAsync(command, tipo.Value!, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<DadosConferidos>> ConferirVersaoAsync(
        RegistrarAtoNormativoCommand command, TipoResolvido tipo, CancellationToken cancellationToken)
    {
        Result<ReferenciaVersaoConfiguracao>? versao = ResolverVersaoInvocada(command);
        return versao is { IsFailure: true }
            ? Result<DadosConferidos>.Failure(versao.Error!)
            : await ConferirCadeiaAsync(command, tipo, versao?.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Quando o registro emenda outro ato, as invariantes da cadeia que dependem do ato retificado
    /// são verificadas com o retificado em mãos (ADR-0103). Os vínculos herdados só se conhecem
    /// depois da resolução: uma retificação que não repete o vínculo o herda do ato retificado, e
    /// nasceria sem documento do mesmo jeito.
    /// </summary>
    private async Task<Result<DadosConferidos>> ConferirCadeiaAsync(
        RegistrarAtoNormativoCommand command,
        TipoResolvido tipo,
        ReferenciaVersaoConfiguracao? versao,
        CancellationToken cancellationToken)
    {
        DomainError? retificacao = await cadeia
            .ConferirAsync(command, tipo.Atributos.CongelaConfiguracao, cancellationToken).ConfigureAwait(false);
        if (retificacao is not null)
        {
            return Result<DadosConferidos>.Failure(retificacao);
        }

        LinhagemDoAto linhagem = await cadeia.ResolverAsync(command, cancellationToken).ConfigureAwait(false);
        return !command.RegistroPeloDominio
            && VinculoDeRegistroPeloDominio(linhagem.Vinculos.Select(v => v.EntidadeTipo)) is { } herdado
            ? Result<DadosConferidos>.Failure(herdado)
            : Result<DadosConferidos>.Success(new DadosConferidos(tipo, versao, linhagem));
    }

    /// <summary>
    /// O id vem do domínio que publicou, quando o registro chega por mensagem durável (ADR-0108).
    /// No caminho HTTP não há domínio remoto a quem ele pertença: <see cref="Guid.Empty"/> faz a
    /// factory gerá-lo.
    /// </summary>
    private static AtoNormativo Criar(
        RegistrarAtoNormativoCommand command, DadosConferidos dados, DateTimeOffset publicadoEm)
    {
        AtributosDoTipoAto atributos = dados.Tipo.Atributos;
        return AtoNormativo.Registrar(
            command.AtoId ?? Guid.Empty, command.Orgao, command.Serie, command.Ano, command.Numero,
            dados.Tipo.Codigo, atributos.CongelaConfiguracao, atributos.EfeitoIrreversivel, atributos.UnicoPorObjeto,
            command.DataDoAto, command.DocumentoHash, command.Assinante, publicadoEm, dados.Versao,
            command.AtoRetificadoId, command.MotivoRetificacao, dados.Linhagem.Vinculos, atributos.Nome,
            AnexosConferiveis(command));
    }

    private async Task<DomainError?> ReservarAsync(
        RegistrarAtoNormativoCommand command, AtoNormativo ato, DadosConferidos dados, CancellationToken cancellationToken)
    {
        Guid raiz = await cadeia
            .ObterRaizAsync(command.AtoRetificadoId, ato.Id, cancellationToken).ConfigureAwait(false);
        return await reservaDasVagas
            .ReservarAsync(ato, dados.Linhagem.IdsDaPropriaCadeia, raiz, cancellationToken).ConfigureAwait(false);
    }

    private sealed record TipoResolvido(string Codigo, AtributosDoTipoAto Atributos);

    private sealed record DadosConferidos(
        TipoResolvido Tipo,
        ReferenciaVersaoConfiguracao? Versao,
        LinhagemDoAto Linhagem);
}

/// <summary>
/// O ato montado, ainda não gravado, e os ids da cadeia de retificação que ele integra: é com
/// eles que se distingue a colisão de numeração da republicação dentro da mesma linhagem.
/// </summary>
public sealed record AtoMontado(AtoNormativo Ato, IReadOnlyList<Guid> IdsDaPropriaCadeia);
