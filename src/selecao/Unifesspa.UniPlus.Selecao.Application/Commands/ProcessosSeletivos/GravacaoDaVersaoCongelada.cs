namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using AtosDoProcesso;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Publicacoes.Contracts;

public sealed class GravacaoDaVersaoCongelada(
    IProcessoSeletivoRepository processos,
    IAtoDoProcessoRepository atos,
    IRascunhoDePublicacaoRepository rascunhos,
    IRegistroDeAtoNormativo registro,
    GravacaoDaDivulgacao divulgacao,
    IUnitOfWork unitOfWork)
{
    private const string IndiceDoIdentificadorLegivelDivulgado = "ux_certames_divulgados_identificador_legivel";

    /// <summary>
    /// Grava a versão, o registro do ato, o ato no registro central, a divulgação do certame e o rascunho de
    /// publicação na mesma transação e devolve os eventos do agregado e a cópia dos documentos ao acervo como cascading
    /// messages. O Wolverine os instala no outbox dentro da transação que gravou a versão, e ou tudo existe, ou nada.
    /// A porta de escrita do registro central só rastreia o ato: a recusa dela, ou o conflito que só a gravação
    /// enxerga, devolve a recusa nomeada sem evento, e o orquestrador descarta o rastreamento (ADR-0170).
    /// </summary>
    internal async Task<(Result Resposta, IEnumerable<object> Eventos)> GravarAsync(
        ProcessoSeletivo processo,
        VersaoConfiguracao versao,
        AtoEDocumentoConferidos ato,
        DadosEdital dados,
        RetificacaoInfo? retificacao,
        CancellationToken cancellationToken)
    {
        Result<AtoDoProcesso> registroDoAto = RegistroDoAtoDaVersao.Montar(
            processo, versao, ato.Ato, ato.TipoDoAto, dados, ato.Documento.Id, retificacao?.EditalRetificadoId, retificacao?.Motivo);
        if (registroDoAto.IsFailure)
        {
            return (Result.ValidationFailure(registroDoAto.Errors), []);
        }

        AtoNormativoPublicado publicado = AtoPublicadoPeloProcesso.DaVersao(
            processo, versao, ato.Ato, ato.TipoDoAto, dados.Numero, ato.Documento.HashSha256!, retificacao);
        Result gravada = await GravarEApagarORascunhoAsync(processo, versao, registroDoAto.Value!, publicado, cancellationToken).ConfigureAwait(false);

        return gravada.IsFailure
            ? (gravada, [])
            : (Result.Success(), [.. processo.DequeueDomainEvents().Cast<object>(), new CopiarDocumentosDoAtoAoAcervo(processo.Id, versao.AtoCriadorId)]);
    }

    // Só com a gravação feita o rascunho perde a razão de existir: apagar antes do flush destruiria a transcrição do
    // operador numa recusa posterior, e o apagamento é um comando direto ao banco que não pode preceder a gravação. Todo
    // caminho que registra ato apaga, porque a raiz é soft-deletable e o cascade da chave estrangeira jamais dispara.
    private async Task<Result> GravarEApagarORascunhoAsync(
        ProcessoSeletivo processo,
        VersaoConfiguracao versao,
        AtoDoProcesso registroDoAto,
        AtoNormativoPublicado publicado,
        CancellationToken cancellationToken)
    {
        Result salva = await RastrearESalvarAsync(processo, versao, registroDoAto, publicado, cancellationToken).ConfigureAwait(false);
        if (salva.IsSuccess)
        {
            await rascunhos.ApagarDoProcessoAsync(processo.Id, cancellationToken).ConfigureAwait(false);
        }

        return salva;
    }

    // A versão, o registro do ato, o ato no registro central e a divulgação vão na mesma gravação, nesta ordem: a
    // consulta dos atos do processo lê só o registro, e o certame passa a existir para o público no mesmo commit em que o
    // ato. A porta vem antes da divulgação para que a recusa dela não deixe certame montado no rastreador.
    private async Task<Result> RastrearESalvarAsync(
        ProcessoSeletivo processo,
        VersaoConfiguracao versao,
        AtoDoProcesso registroDoAto,
        AtoNormativoPublicado publicado,
        CancellationToken cancellationToken)
    {
        await processos.AdicionarVersaoConfiguracaoAsync(versao, cancellationToken).ConfigureAwait(false);
        await atos.AdicionarAsync(registroDoAto, cancellationToken).ConfigureAwait(false);

        Result aceito = await registro.RegistrarAsync(publicado, cancellationToken).ConfigureAwait(false);
        if (aceito.IsFailure)
        {
            return aceito;
        }

        Result montada = await divulgacao.MontarDaVersaoAsync(processo, versao, registroDoAto, cancellationToken).ConfigureAwait(false);

        return montada.IsFailure ? montada : await SalvarAsync(processo, registroDoAto, cancellationToken).ConfigureAwait(false);
    }

    // Duas transições concorrentes do mesmo processo derivam o mesmo número de versão, e o índice único e o gatilho de
    // sucessão deixam passar uma só (ADR-0102): é aqui que a linearidade da cadeia é garantida. O identificador legível
    // que outro certame já divulga e a vaga ou a retificação que outra transação tomou são os outros conflitos que só a
    // gravação enxerga, e a Seleção traduz os seus antes de perguntar à porta pelos do registro central. A recusa não
    // devolve exceção, e o orquestrador descarta o rastreamento antes de o Wolverine salvar de novo (ADR-0119).
    private async Task<Result> SalvarAsync(ProcessoSeletivo processo, AtoDoProcesso registroDoAto, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (Traduzir(ex, processo, registroDoAto) is { } erro)
        {
            return Result.Failure(erro);
        }

        return Result.Success();
    }

    private DomainError? Traduzir(Exception excecao, ProcessoSeletivo processo, AtoDoProcesso registroDoAto)
    {
        string? constraint = UniqueConstraintViolation.GetViolatedConstraint(excecao);

        return VersaoConfiguracaoConstraintViolation.Traduzir(constraint)
            ?? ConflitoDoIdentificadorLegivel(constraint, processo)
            ?? ConflitoNaGravacaoDoAtoDoProcesso.Traduzir(constraint, registroDoAto)
            ?? registro.TraduzirConflitoDaGravacao(excecao);
    }

    private static DomainError? ConflitoDoIdentificadorLegivel(string? constraint, ProcessoSeletivo processo) =>
        string.Equals(constraint, IndiceDoIdentificadorLegivelDivulgado, StringComparison.Ordinal)
            ? CriarProcessoSeletivoCommandHandler.IdentificadorLegivelEmUso(processo.IdentificadorLegivel!.Value)
            : null;
}
