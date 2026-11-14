namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>O órgão que numera o ato e a divulgação do certame, conferidos antes de qualquer escrita.</summary>
/// <param name="Orgao">Sigla da unidade administradora do processo.</param>
/// <param name="Divulgado">
/// A divulgação do certame, rastreada: de onde vêm a unidade e o identificador da chave no acervo, e onde o ato
/// publicado registra a entrada que a cópia ao acervo preencherá.
/// </param>
internal sealed record ProcessoDivulgadoConferido(string Orgao, CertameDivulgado Divulgado);

/// <summary>Tudo o que a conferência decidiu sobre o ato e que a publicação grava e manda ao Publicações.</summary>
/// <param name="ProcessoSeletivoId">O processo que publica o ato.</param>
/// <param name="Processo">O órgão e a divulgação do certame.</param>
/// <param name="Tipo">O tipo do ato como o catálogo o declarava na conferência.</param>
/// <param name="Numero">Número que o documento declara, quando o tipo o tem.</param>
/// <param name="DataDoAto">Data que o documento declara; não conta para prazo, vigência nem escolha do tipo de ato.</param>
/// <param name="PublicadoEm">O instante da divulgação, lido uma só vez pelo comando que publica.</param>
/// <param name="Assinante">Quem assina o ato.</param>
/// <param name="Documentos">O documento principal e os anexos já conferidos.</param>
/// <param name="AtoCorrigidoId">O ato que este corrige, quando é errata ou retificação.</param>
/// <param name="MotivoDaCorrecao">Motivo da correção, junto com <paramref name="AtoCorrigidoId"/>.</param>
/// <param name="ItemDoEdital">A identidade do item do edital que o ato cumpre; nula no documento avulso.</param>
/// <param name="PoliticaConfirmadaPorSub">Quem confirmou a política de divulgação; nulo quando ela não foi pedida.</param>
internal sealed record AtoAPublicar(
    Guid ProcessoSeletivoId,
    ProcessoDivulgadoConferido Processo,
    TipoAtoPublicadoView Tipo,
    string? Numero,
    DateOnly DataDoAto,
    DateTimeOffset PublicadoEm,
    string Assinante,
    DocumentosDoAtoConferidos Documentos,
    Guid? AtoCorrigidoId,
    string? MotivoDaCorrecao,
    Domain.ValueObjects.IdentidadeDoItemDoEdital? ItemDoEdital = null,
    string? PoliticaConfirmadaPorSub = null);

/// <summary>
/// O que o documento avulso e a publicação de um item do edital têm em comum: confere que o processo
/// tem página pública e, no fim, grava o registro do ato do processo e a entrada do ato na divulgação do certame, entrega
/// o ato à porta de escrita do registro central e salva tudo numa gravação só, que enfileira a cópia dos documentos ao
/// acervo na mesma transação (ADR-0170).
/// </summary>
/// <remarks>
/// <para>
/// <b>Idempotência.</b> Vem da <c>Idempotency-Key</c> do endpoint: o <c>AtoId</c> é decidido a cada
/// requisição e fica no registro do ato.
/// </para>
/// <para>
/// <b>Campos que o documento não declara.</b> O órgão é a sigla da unidade administradora do processo, a
/// série de numeração é o próprio tipo do ato e o ano é o da data que o documento declara. A unidade e
/// o identificador do processo na chave do documento no acervo são os da versão divulgada, gravados no
/// registro porque a chave não muda depois de publicada.
/// </para>
/// </remarks>
internal static class PublicacaoDoAtoDoProcesso
{
    public static async Task<Result<ProcessoDivulgadoConferido>> ConferirProcessoDivulgadoAsync(
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        Guid processoSeletivoId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);

        string? orgao = await processoSeletivoRepository
            .ObterSiglaDaUnidadeAdministradoraAsync(processoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (orgao is null)
        {
            return Falha($"Processo Seletivo {processoSeletivoId} não encontrado.", "ProcessoSeletivo.NaoEncontrado");
        }

        CertameDivulgado? divulgado = await certameDivulgadoRepository
            .ObterParaMutacaoAsync(processoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        return divulgado is null
            ? Falha(
                "O processo ainda não tem página pública: os atos do processo aparecem na linha do tempo do certame, que nasce com a publicação do edital de abertura.",
                AtoDoProcessoErrorCodes.CertameNaoDivulgado)
            : Result<ProcessoDivulgadoConferido>.Success(new ProcessoDivulgadoConferido(orgao, divulgado));
    }

    public static async Task<(Result<AtoPublicadoDto> Resposta, IEnumerable<object> Eventos)> PublicarAsync(
        AtoAPublicar ato,
        IAtoDoProcessoRepository atoDoProcessoRepository,
        IRegistroDeAtoNormativo registroCentralDeAtos,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ato);
        ArgumentNullException.ThrowIfNull(atoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(registroCentralDeAtos);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<AtoDoProcesso> registrado = await RegistrarAsync(ato, atoDoProcessoRepository, cancellationToken).ConfigureAwait(false);

        return registrado.IsFailure
            ? (Result<AtoPublicadoDto>.ValidationFailure(registrado.Errors), [])
            : await ConcluirPublicacaoAsync(ato, registrado.Value!, registroCentralDeAtos, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(Result<AtoPublicadoDto> Resposta, IEnumerable<object> Eventos)> ConcluirPublicacaoAsync(
        AtoAPublicar ato,
        AtoDoProcesso atoDoProcesso,
        IRegistroDeAtoNormativo registroCentralDeAtos,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        DomainError? recusa = await RegistrarEGravarAsync(ato, atoDoProcesso, registroCentralDeAtos, unitOfWork, cancellationToken).ConfigureAwait(false);

        return recusa is not null
            ? (Result<AtoPublicadoDto>.Failure(recusa), [])
            : (Result<AtoPublicadoDto>.Success(new AtoPublicadoDto(atoDoProcesso.Id)), [new CopiarDocumentosDoAtoAoAcervo(ato.ProcessoSeletivoId, atoDoProcesso.Id)]);
    }

    private static async Task<DomainError?> RegistrarEGravarAsync(
        AtoAPublicar ato,
        AtoDoProcesso atoDoProcesso,
        IRegistroDeAtoNormativo registroCentralDeAtos,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result aceito = await registroCentralDeAtos
            .RegistrarAsync(AtoPublicadoPeloProcesso.DoAtoSemVersao(ato, atoDoProcesso), cancellationToken)
            .ConfigureAwait(false);
        if (aceito.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return aceito.Error;
        }

        ato.Processo.Divulgado.AbrirEntradaDoAto(atoDoProcesso.Id, numeroVersao: null);
        return await SalvarAsync(atoDoProcesso, registroCentralDeAtos, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<DomainError?> SalvarAsync(
        AtoDoProcesso atoDoProcesso, IRegistroDeAtoNormativo registroCentralDeAtos, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception excecao) when (Traduzir(excecao, atoDoProcesso, registroCentralDeAtos) is { } recusa)
        {
            // Corrida entre duas escritas do mesmo item, do mesmo ato corrigido ou da mesma vaga do objeto. A recusa
            // não devolve exceção, e o Wolverine salva de novo depois do handler: sem descartar o rastreamento, a
            // mesma violação estoura fora do catch (ADR-0119).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return recusa;
        }
    }

    // A Seleção reconhece o que é dela, e a porta, o que é do registro central: quem não reconhece propaga.
    private static DomainError? Traduzir(Exception excecao, AtoDoProcesso atoDoProcesso, IRegistroDeAtoNormativo registroCentralDeAtos) =>
        ConflitoNaGravacaoDoAtoDoProcesso.Traduzir(ConstraintVioladaNaGravacao.NomeDaConstraintViolada(excecao), atoDoProcesso)
            ?? registroCentralDeAtos.TraduzirConflitoDaGravacao(excecao);

    private static async Task<Result<AtoDoProcesso>> RegistrarAsync(
        AtoAPublicar ato, IAtoDoProcessoRepository atoDoProcessoRepository, CancellationToken cancellationToken)
    {
        AtoDoProcesso versaoDivulgada = await VersaoDivulgadaAsync(ato, atoDoProcessoRepository, cancellationToken).ConfigureAwait(false);
        Result<AtoDoProcesso> registrado = AtoDoProcesso.Registrar(NovoAtoDe(ato, versaoDivulgada), ato.PublicadoEm);
        if (registrado.IsSuccess)
        {
            await atoDoProcessoRepository.AdicionarAsync(registrado.Value!, cancellationToken).ConfigureAwait(false);
        }

        return registrado;
    }

    private static async Task<AtoDoProcesso> VersaoDivulgadaAsync(
        AtoAPublicar ato, IAtoDoProcessoRepository atoDoProcessoRepository, CancellationToken cancellationToken)
    {
        Guid atoCriadorId = ato.Processo.Divulgado.AtoCriadorId;

        return await atoDoProcessoRepository.ObterComAnexosAsync(atoCriadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"A divulgação do processo {ato.ProcessoSeletivoId} aponta para o ato {atoCriadorId}, que não tem registro do ato do processo.");
    }

    private static NovoAtoDoProcesso NovoAtoDe(AtoAPublicar ato, AtoDoProcesso versaoDivulgada) =>
        IdentidadeDoNovoAto(ato, versaoDivulgada) with
        {
            ItemDoEdital = ato.ItemDoEdital,
            AtoCorrigidoId = ato.AtoCorrigidoId,
            MotivoDaCorrecao = ato.MotivoDaCorrecao,
            PoliticaDeDivulgacaoConfirmadaPorSub = ato.PoliticaConfirmadaPorSub,
        };

    private static NovoAtoDoProcesso IdentidadeDoNovoAto(AtoAPublicar ato, AtoDoProcesso versaoDivulgada) =>
        new(
            AtoId: Guid.CreateVersion7(ato.PublicadoEm),
            ProcessoSeletivoId: ato.ProcessoSeletivoId,
            TipoCodigo: ato.Tipo.Codigo,
            TipoNome: ato.Tipo.Nome,
            Numero: ato.Numero,
            DataDoAto: ato.DataDoAto,
            Assinante: ato.Assinante,
            SlugDaUnidadeAdministradora: versaoDivulgada.SlugDaUnidadeAdministradora,
            IdentificadorLegivelDoProcesso: versaoDivulgada.IdentificadorLegivelDoProcesso,
            DocumentoPrincipalId: ato.Documentos.Principal.Id,
            Anexos: [.. ato.Documentos.Anexos.Select(static a => new AnexoDeclarado(a.Documento.Id, a.Nome))]);

    private static Result<ProcessoDivulgadoConferido> Falha(string mensagem, string codigo) =>
        Result<ProcessoDivulgadoConferido>.Failure(new DomainError(codigo, mensagem));
}
