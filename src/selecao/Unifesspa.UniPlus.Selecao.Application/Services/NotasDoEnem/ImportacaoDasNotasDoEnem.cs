namespace Unifesspa.UniPlus.Selecao.Application.Services.NotasDoEnem;

using Commands.NotasDoEnem;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Leiautes;
using Domain.Services;
using Domain.ValueObjects;

using DTOs.NotasDoEnem;

using Kernel.Results;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>Confere de novo a versão divulgada sob o bloqueio e registra o lote que substitui o anterior.</summary>
public sealed partial class ImportacaoDasNotasDoEnem(ResolvedorDoEscopoDeNotasDoEnem escopos,
    ILoteDeNotasDoEnemRepository lotes, IInscritosDaEdicaoDoEnem inscritos, IUserContext usuario,
    IResolvedorFusoInstitucional fuso, DetalheDoLoteDeNotasMontador detalhes, ILogger<ImportacaoDasNotasDoEnem> logger)
{
    public async Task<Result<(LoteDeNotasDoEnem Lote, bool Criado)>> ConcluirAsync(ImportarNotasDoEnemCommand command,
        ImportacaoDeNotasPreparada preparada, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(preparada);
        Result<EscopoDeNotasDoEnem> escopo = await escopos.ResolverAsync(
            command.ProcessoSeletivoId, command.EtapaProcessoId, command.Edicao, true, cancellationToken).ConfigureAwait(false);
        if (escopo.IsFailure)
        {
            return Result<(LoteDeNotasDoEnem, bool)>.Failure(escopo.Error!);
        }
        DomainError? recusa = ResolvedorDoEscopoDeNotasDoEnem.ConferirInscricoesEncerradas(escopo.Value!, agora, fuso);
        return recusa is not null ? Result<(LoteDeNotasDoEnem, bool)>.Failure(recusa)
            : await ObterOuRegistrarAsync(escopo.Value!, preparada, agora, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<(LoteDeNotasDoEnem, bool)>> ObterOuRegistrarAsync(EscopoDeNotasDoEnem escopo,
        ImportacaoDeNotasPreparada preparada, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        LoteDeNotasDoEnem? vigente = await lotes.ObterVigenteAsync(
            escopo.ProcessoSeletivoId, escopo.EtapaProcessoId, escopo.EdicaoCodigo, cancellationToken).ConfigureAwait(false);
        if (VigenciaDoLote.ArquivoJaVigente(vigente, preparada.ArquivoHash, escopo.VersaoConfiguracaoId))
        {
            LoteDeNotasDoEnem existente = (await lotes.ObterComOcorrenciasAsync(escopo.ProcessoSeletivoId,
                escopo.EtapaProcessoId, escopo.EdicaoCodigo, vigente!.Id, cancellationToken).ConfigureAwait(false))!;
            return Result<(LoteDeNotasDoEnem, bool)>.Success((existente, false));
        }
        return await RegistrarAsync(escopo, preparada, agora, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<(LoteDeNotasDoEnem, bool)>> RegistrarAsync(EscopoDeNotasDoEnem escopo,
        ImportacaoDeNotasPreparada preparada, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        IReadOnlyList<InscritoDaEdicao> daEdicao = await inscritos.ListarAsync(
            escopo.ProcessoSeletivoId, escopo.EdicaoCodigo, cancellationToken).ConfigureAwait(false);
        EditalParaApuracaoDaNota edital = EditalParaApuracaoDaNota.De(escopo.Configuracao);
        if (ConferirOfertas(daEdicao, edital) is { } recusa)
        {
            return Result<(LoteDeNotasDoEnem, bool)>.Failure(recusa);
        }
        LoteDeNotasDoEnem lote = MontarLote(escopo, preparada, agora, daEdicao, edital);
        await lotes.AdicionarAsync(lote, cancellationToken).ConfigureAwait(false);
        return Result<(LoteDeNotasDoEnem, bool)>.Success((lote, true));
    }

    private static DomainError? ConferirOfertas(IReadOnlyList<InscritoDaEdicao> inscritos, EditalParaApuracaoDaNota edital)
    {
        int foraDoEdital = inscritos.Count(i => i.OfertasConcorridas.Any(o => !edital.GrupoPorOferta.ContainsKey(o)));
        return foraDoEdital == 0 ? null : new DomainError(NotasDoEnemErrorCodes.OfertaDeInscricaoForaDoEdital,
            $"{foraDoEdital} inscrição(ões) da edição concorre(m) a um curso que não está no edital divulgado. Confira a retificação do edital e a situação dessas inscrições antes de importar.");
    }

    private LoteDeNotasDoEnem MontarLote(EscopoDeNotasDoEnem escopo, ImportacaoDeNotasPreparada preparada,
        DateTimeOffset agora, IReadOnlyList<InscritoDaEdicao> daEdicao, EditalParaApuracaoDaNota edital)
    {
        ResultadoDoCruzamento cruzamento = CruzamentoDasNotasComInscritos.Cruzar(preparada.Linhas, preparada.IndicePorCpf, daEdicao);
        IReadOnlyList<AvaliacaoApurada> avaliacoes = ApuracaoDaNotaDoEnem.ApurarDoLote(cruzamento, daEdicao, edital.GrupoPorOferta, edital.Quadro);
        return LoteDeNotasDoEnem.Registrar(escopo.ProcessoSeletivoId, escopo.EtapaProcessoId, escopo.EdicaoCodigo,
            preparada.LeiauteCodigo, preparada.LeiauteVersao, preparada.LeiauteHash, preparada.ArquivoHash,
            usuario.UserId ?? string.Empty, agora, preparada.Linhas.Length, escopo.VersaoConfiguracaoId,
            cruzamento, avaliacoes, usuario.NomeCompleto);
    }

    public async Task<Result<ImportacaoDeNotasDto>> ResponderAsync(LoteDeNotasDoEnem lote, bool criado, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lote);
        RegistrarResultadoNoLog(lote, criado);
        return Result<ImportacaoDeNotasDto>.Success(new(await detalhes.MontarAsync(lote, true, cancellationToken).ConfigureAwait(false), criado));
    }

    private void RegistrarResultadoNoLog(LoteDeNotasDoEnem lote, bool criado)
    {
        if (criado)
        {
            LogLoteImportado(lote.Id, lote.ProcessoSeletivoId, lote.TotalDeLinhas, lote.TotalDeInscritosComNota,
                lote.TotalDeInscritosSemLinha, lote.TotalDeLinhasSemInscrito, lote.TotalDeInscritosEmConflito);
            int apuradas = lote.Avaliacoes.Count(static a => a.Situacao == Domain.Enums.SituacaoDaAvaliacao.Apurada);
            LogAvaliacoesDoLote(lote.Id, apuradas, lote.Avaliacoes.Count - apuradas);
        }
        else
        {
            LogLoteJaVigente(lote.Id, lote.ProcessoSeletivoId);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Lote {LoteId} de notas do ENEM importado no processo {ProcessoId}: {Linhas} linhas, {ComNota} com nota, {SemLinha} inscritos sem linha, {SemInscrito} linhas sem inscrito, {Conflitos} em conflito")]
    private partial void LogLoteImportado(Guid loteId, Guid processoId, int linhas, int comNota, int semLinha, int semInscrito, int conflitos);

    [LoggerMessage(Level = LogLevel.Information, Message = "Lote {LoteId} de notas do ENEM: {Apuradas} avaliações apuradas e {SemNota} sem nota")]
    private partial void LogAvaliacoesDoLote(Guid loteId, int apuradas, int semNota);

    [LoggerMessage(Level = LogLevel.Information, Message = "Arquivo já é o do lote vigente {LoteId} no processo {ProcessoId}; nenhum lote novo foi criado")]
    private partial void LogLoteJaVigente(Guid loteId, Guid processoId);
}
